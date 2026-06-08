using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Globalny magazyn przechowujący zmienne sesji Agenta (@zmienna) z automatycznym
        /// lustrzanym odbiciem na Blackboardzie (dla architektury Multi-Agent).
        /// </summary>
        public static readonly VariableStore Variables = new VariableStore();

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
