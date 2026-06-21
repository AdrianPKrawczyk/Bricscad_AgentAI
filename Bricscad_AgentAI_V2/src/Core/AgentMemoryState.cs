using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Przechowuje pamięć podręczną operacji dokonywanych w obrębie sesji Agenta AI.
    /// Zapobiega kolizjom stanu podczas asynchronicznych modyfikacji i pozwala
    /// na składowanie wskaźników referencyjnych dla narzędzi.
    /// </summary>
    public static class AgentMemoryState
    {
        private static ObjectId[] _activeSelection = new ObjectId[0];
        private static bool _selectionScopeLocked;

        public static bool EarlyExitEnabled { get; set; } = true;

        // Flaga wlacza Chain of Evidence (Filar 2 Auditor). Domyslnie false -
        // opt-in jak reszta flag Agenta Rewidenta. Wlaczana z UI checkboxem
        // lub z kodu przez Supervisor przed delegacja do ryzykownego zadania.
        public static bool EvidenceEnabled { get; set; } = false;

        // Filar 3 (Auditor): czy w ogole uruchamiac audyt (Wariant A + B).
        // Domyslnie false - opt-in. UI checkbox 'chkAuditorEnabled' w AgentControl.
        public static bool AuditorEnabled { get; set; } = false;

        // Filar 3 (Auditor): czy rozgrzewac KV cache Auditora w tle (Task.Run)
        // PRZED wywolaniem Workera. Wymaga srodowiska Multi-GPU lub duzego VRAM,
        // bo oznacza rownolegle trzymanie w pamieci modelu Workera i Auditora.
        // Domyslnie false - dla pojedynczej karty lepiej sekwencyjnie.
        public static bool AuditorPrewarmEnabled { get; set; } = false;

        // Filar 4 (Auditor Circuit Breaker): czy w ogole blokowac profile.
        // Domyslnie true - zawsze chcemy chronic sie przed Agent Death Loop.
        // UI checkbox 'chkCircuitBreaker' w AgentControl.
        public static bool CircuitBreakerEnabled { get; set; } = true;

        // Filar 4: prog awarii (zgodny z ustaleniem Q4 - 3 proby). Mozna zwiekszyc
        // dla upartych modeli lub zmniejszyc dla szybszego failover.
        public static int CircuitBreakerThreshold { get; set; } = 3;

        // Twardy limit Handle'ow na sesje - zabezpiecza kontekst Rewidenta
        // przed eksplozja przy operacjach masowych (np. ManageLayers na 500 obiektach).
        public const int MaxEvidenceHandles = 64;

        /// <summary>
        /// Globalny magazyn przechowujący zmienne sesji Agenta (@zmienna) z automatycznym
        /// lustrzanym odbiciem na Blackboardzie (dla architektury Multi-Agent).
        /// </summary>
        public static readonly VariableStore Variables = new VariableStore();

        // ============== CHAIN OF EVIDENCE (Filar 2 - Auditor) ==============
        // Kolekcje sa Concurrent* dla bezpieczenstwa wielowatkowego: EngineTracer
        // odpala handlery zdarzen bazy DWG z kontekstu Teigha (innego niz watek UI),
        // a ToolOrchestrator wykonuje snapshoty z kontekstu WPF Dispatcher.
        // Uzywamy ConcurrentBag/ConcurrentQueue zamiast lock() - lock na 500 obiektow
        // w ManageLayers moglby zablokowac caly system na kilkaset ms.
        private static readonly ConcurrentBag<ObjectId> _modifiedEntities = new ConcurrentBag<ObjectId>();
        private static int _mutationCounter;
        private static int _rollbackCounter;
        private static long _sessionMarkerTicks;

        /// <summary>
        /// Licznik mutacji w sesji (inkrementowany przez EngineTracer w ObjectAppended/ObjectModified).
        /// Uzywany do taniej walidacji heurystycznej Wariantu A (Filar C).
        /// </summary>
        public static int MutationCount => Interlocked.CompareExchange(ref _mutationCounter, 0, 0);

        /// <summary>
        /// Licznik rollbackow (inkrementowany przez EngineTracer w TransactionAborted).
        /// </summary>
        public static int RollbackCount => Interlocked.CompareExchange(ref _rollbackCounter, 0, 0);

        /// <summary>
        /// Marker czasu (UTC ticks) poczatku sesji - uzywany przez CountMutationsSinceSession.
        /// </summary>
        public static long SessionMarkerTicks => Interlocked.Read(ref _sessionMarkerTicks);

        /// <summary>
        /// Zwraca NIEmodyfikowalna kopie ModifiedEntities (do enumeracji bezpiecznej w ReadFromBlackboard).
        /// Wewnetrzny ConcurrentBag jest wspoldzielony miedzy watkami.
        /// </summary>
        public static ObjectId[] GetModifiedEntitiesSnapshot()
        {
            var ids = _modifiedEntities.ToArray();
            Array.Sort(ids, (a, b) => a.Handle.Value.CompareTo(b.Handle.Value));
            return ids;
        }

        public static void RecordMutation(ObjectId id)
        {
            if (id.IsNull) return;
            _modifiedEntities.Add(id);
            Interlocked.Increment(ref _mutationCounter);
        }

        public static void RecordRollback()
        {
            Interlocked.Increment(ref _rollbackCounter);
        }

        public static void BeginSession()
        {
            while (_modifiedEntities.TryTake(out _)) { }
            Interlocked.Exchange(ref _mutationCounter, 0);
            Interlocked.Exchange(ref _rollbackCounter, 0);
            Interlocked.Exchange(ref _sessionMarkerTicks, DateTime.UtcNow.Ticks);
        }

        // ===================================================================

        /// <summary>
        /// Zbiór referencji do aktualnie wyizolowanych (lub zaznaczonych) obiektów w dokumencie.
        /// </summary>
        public static ObjectId[] ActiveSelection => _activeSelection;

        public static bool IsSelectionScopeLocked => _selectionScopeLocked;

        public static void LockSelectionScope()
        {
            _selectionScopeLocked = true;
        }

        public static void UnlockSelectionScope()
        {
            _selectionScopeLocked = false;
        }

        /// <summary>
        /// Funkcja zastępująca wzorce @zmienna wartościami ze słownika Variables.
        /// </summary>
        public static string InjectVariables(string input)
        {
            if (string.IsNullOrEmpty(input) || Variables == null || Variables.Count == 0) return input;
            string output = input;
            var keys = Variables.Keys.OrderByDescending(k => k.Length).ToList();
            foreach (var key in keys)
            {
                output = output.Replace("@" + key, Variables[key]);
            }
            return output;
        }

        /// <summary>
        /// Całkowicie zastępuje aktualną pamięć zaznaczenia nową tablicą ID.
        /// </summary>
        public static void Update(ObjectId[] ids)
        {
            _activeSelection = ids ?? new ObjectId[0];
        }

        /// <summary>
        /// Dołącza nowe IDki do istniejącego zbioru zaznaczenia, upewniając się, że wartości są unikalne.
        /// </summary>
        public static void Append(ObjectId[] ids)
        {
            if (ids == null || ids.Length == 0) return;
            var currentList = _activeSelection.ToList();
            currentList.AddRange(ids);
            _activeSelection = currentList.Distinct().ToArray();
        }

        /// <summary>
        /// Usuwa wybrane IDki z aktualnego zbioru zaznaczenia.
        /// </summary>
        public static void Remove(ObjectId[] ids)
        {
            if (ids == null || ids.Length == 0) return;
            var currentList = _activeSelection.ToList();
            var toRemove = new HashSet<ObjectId>(ids);
            currentList.RemoveAll(id => toRemove.Contains(id));
            _activeSelection = currentList.ToArray();
        }

        /// <summary>
        /// Czyści całkowicie pamięć zestawu zaznaczenia.
        /// </summary>
        public static void Clear()
        {
            _activeSelection = new ObjectId[0];
            _selectionScopeLocked = false;
        }
    }

    /// <summary>
    /// Klasa owijająca słownik zmiennych Agenta, automatycznie synchronizująca wpisy
    /// z globalną tablicą ogłoszeń (Blackboard) dla architektury wieloagentowej.
    /// </summary>
    public class VariableStore
    {
        private readonly Dictionary<string, string> _dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string this[string key]
        {
            get
            {
                return _dict.TryGetValue(key, out var val) ? val : null;
            }
            set
            {
                _dict[key] = value;
                try
                {
                    SharedMemoryState.Write(key, value);
                }
                catch { }
            }
        }

        public void Clear()
        {
            _dict.Clear();
            try
            {
                SharedMemoryState.Clear();
            }
            catch { }
        }

        public bool ContainsKey(string key)
        {
            return _dict.ContainsKey(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return _dict.TryGetValue(key, out value);
        }

        public ICollection<string> Keys => _dict.Keys;
        public int Count => _dict.Count;
    }
}
