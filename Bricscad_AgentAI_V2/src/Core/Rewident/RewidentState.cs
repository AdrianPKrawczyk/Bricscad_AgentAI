using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Core.Rewident
{
    /// <summary>
    /// Stan statyczny Agenta Rewidenta (profil RewidentProfile).
    /// Wydzielony z AgentMemoryState w v2.35.0 (refaktor Auditor -> Rewident).
    /// Stary profil AuditorProfile zostaje dla QA/audytu kodu.
    /// </summary>
    public static class RewidentState
    {
        // ============== FILAR 6: Master kill switch ==============
        // Gdy true - WSZYSTKIE mechanizmy Rewidenta sa wylaczone (Auto-Inject, CB,
        // rollback detection, Filar A heurystyczny, Filar B LLM). Worker dziala jak w v2.28.x.
        // Domyslnie false - user musi swiadomie wylaczyc w UI.
        public static bool GloballyDisabled { get; set; } = false;

        // ============== FILAR 2: Chain of Evidence ==============
        public static bool EvidenceEnabled { get; set; } = false;

        // ============== FILAR 5: Auto-Inject Properties ==============
        public static bool AutoInjectPropertiesEnabled { get; set; } = true;
        public static int AutoInjectSamplePercent { get; set; } = 2;
        public const int AutoInjectMaxSamples = 20;

        // ============== FILAR 3: LLM Auditor ==============
        // Domyślnie OFF - opt-in, bo LLM Auditor halucynuje (patrz v2.34.20-22).
        public static bool AuditorEnabled { get; set; } = false;
        public static bool AuditorPrewarmEnabled { get; set; } = false;

        // ============== FILAR 4: Circuit Breaker ==============
        // Domyslnie ON - opt-out, zawsze chcemy ochrony przed Agent Death Loop.
        public static bool CircuitBreakerEnabled { get; set; } = true;
        public static int CircuitBreakerThreshold { get; set; } = 3;
        public const int MaxEvidenceHandles = 64;

        // ============== Detektory mutacji (wspoldzielone) ==============
        // Kolekcje sa Concurrent* dla bezpieczenstwa wielowatkowego: EngineTracer
        // odpala handlery zdarzen bazy DWG z kontekstu Teigha (innego niz watek UI),
        // a ToolOrchestrator wykonuje snapshoty z kontekstu WPF Dispatcher.
        // Uzywamy ConcurrentBag/Interlocked zamiast lock() - lock na 500 obiektow
        // w ManageLayers moglby zablokowac caly system na kilkaset ms.
        private static readonly ConcurrentBag<ObjectId> _modifiedEntities = new ConcurrentBag<ObjectId>();
        private static int _mutationCounter;
        private static int _rollbackCounter;

        /// <summary>
        /// Licznik mutacji w sesji (inkrementowany przez EngineTracer w ObjectAppended/ObjectModified).
        /// Uzywany do taniej walidacji heurystycznej Wariantu A.
        /// </summary>
        public static int MutationCount => Interlocked.CompareExchange(ref _mutationCounter, 0, 0);

        /// <summary>
        /// Licznik rollbackow (inkrementowany przez EngineTracer w TransactionAborted).
        /// </summary>
        public static int RollbackCount => Interlocked.CompareExchange(ref _rollbackCounter, 0, 0);

        /// <summary>
        /// Zwraca NIEmodyfikowalna kopie ModifiedEntities (do enumeracji bezpiecznej w ReadFromBlackboard).
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

        /// <summary>
        /// Reset stanu (np. na poczatku sesji agenta).
        /// </summary>
        public static void BeginSession()
        {
            while (_modifiedEntities.TryTake(out _)) { }
            Interlocked.Exchange(ref _mutationCounter, 0);
            Interlocked.Exchange(ref _rollbackCounter, 0);
        }
    }
}
