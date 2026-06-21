using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Circuit Breaker (Filar 4 Agenta Rewidenta) - zabezpiecza system przed
    /// "Agent Death Loop": sytuacja, w ktorej Worker w kolko generuje ten sam
    /// blad mimo progresywnego naprowadzania, lub Auditor odrzuca prace w nieskonczonosc.
    ///
    /// Po N kolejnych odrzuceniach (domyslnie 3) profil jest blokowany
    /// (IsTripped=true) i kolejne DelegateTask do niego natychmiast zwraca
    /// Failure z prosba o precyzowanie promptu przez uzytkownika.
    ///
    /// Reset: kazdy Accept (sukces Workera zaakceptowany przez Auditora/Validator)
    /// zeruje licznik bledow. Circuit Breaker per-profil (np. CadProfile ma swoj,
    /// CadBlocksProfile ma swoj - moga wpasc w petle niezaleznie).
    /// </summary>
    public static class CircuitBreakerState
    {
        private static readonly ConcurrentDictionary<string, int> _failuresPerProfile
            = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, long> _lastFailureTicks
            = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        // Domyślny prog - zgodny z ustaleniem Q4: po 3 probach bez poprawy
        // model zazwyczaj wpada w "pulapke deterministyczna".
        public const int DefaultThreshold = 3;

        /// <summary>
        /// Sprawdza czy profil jest zablokowany (IsTripped). Jesli tak, DelegateTask
        /// powinien natychmiast zwrocic Failure bez proby delegowania.
        /// </summary>
        public static bool IsTripped(string profileName)
        {
            if (string.IsNullOrEmpty(profileName)) return false;
            if (!AgentMemoryState.CircuitBreakerEnabled) return false;

            int threshold = AgentMemoryState.CircuitBreakerThreshold > 0
                ? AgentMemoryState.CircuitBreakerThreshold
                : DefaultThreshold;

            int failures = _failuresPerProfile.TryGetValue(profileName, out var f) ? f : 0;
            return failures >= threshold;
        }

        /// <summary>
        /// Zwraca aktualny stan (do UI/diagnostyki).
        /// </summary>
        public static CircuitBreakerSnapshot GetSnapshot(string profileName)
        {
            int failures = _failuresPerProfile.TryGetValue(profileName, out var f) ? f : 0;
            long lastTicks = _lastFailureTicks.TryGetValue(profileName, out var t) ? t : 0;
            int threshold = AgentMemoryState.CircuitBreakerThreshold > 0
                ? AgentMemoryState.CircuitBreakerThreshold
                : DefaultThreshold;
            return new CircuitBreakerSnapshot
            {
                ProfileName = profileName,
                Failures = failures,
                Threshold = threshold,
                IsTripped = failures >= threshold,
                LastFailureUtcTicks = lastTicks
            };
        }

        /// <summary>
        /// Rejestruje nieudana probe - zwieksza licznik bledow dla profilu.
        /// Wywolywane z DelegateTaskTool po AuditorReport.Reject / Reject z WorkValidator.
        /// </summary>
        public static void RecordFailure(string profileName, string reason = null)
        {
            if (string.IsNullOrEmpty(profileName)) return;
            int newCount = _failuresPerProfile.AddOrUpdate(profileName, 1, (k, old) => old + 1);
            _lastFailureTicks[profileName] = DateTime.UtcNow.Ticks;

            if (newCount >= (AgentMemoryState.CircuitBreakerThreshold > 0
                ? AgentMemoryState.CircuitBreakerThreshold
                : DefaultThreshold))
            {
                BielikLogger.LogWarn(
                    $"[CIRCUIT BREAKER] TRIPPED dla profilu '{profileName}' po {newCount} awariach. " +
                    $"Powod ostatniej awarii: {reason ?? "(brak)"}. " +
                    $"Profil zablokowany do czasu Reset() lub restartu aplikacji.");
            }
        }

        /// <summary>
        /// Zapisuje ze profil wykonal poprawna prace - resetuje licznik.
        /// Wywolywane z DelegateTaskTool po AuditorReport.Accept + WorkValidator.Accept.
        /// </summary>
        public static void Reset(string profileName)
        {
            if (string.IsNullOrEmpty(profileName)) return;
            int old = _failuresPerProfile.TryGetValue(profileName, out var v) ? v : 0;
            if (old > 0)
            {
                _failuresPerProfile.TryRemove(profileName, out _);
                BielikLogger.LogInfo(
                    $"[CIRCUIT BREAKER] RESET dla profilu '{profileName}' (bylo {old} awarii). Profil odblokowany.");
            }
        }

        /// <summary>
        /// Reczny reset z UI / kodu - wymusza odblokowanie profilu.
        /// Przydatne gdy uzytkownik poprawi prompt lub zmieni model.
        /// </summary>
        public static void ForceReset(string profileName)
        {
            if (string.IsNullOrEmpty(profileName)) return;
            _failuresPerProfile.TryRemove(profileName, out _);
            _lastFailureTicks.TryRemove(profileName, out _);
            BielikLogger.LogInfo($"[CIRCUIT BREAKER] FORCE RESET dla profilu '{profileName}'.");
        }

        /// <summary>
        /// Resetuje wszystkie profile (np. przy restarcie sesji agenta).
        /// </summary>
        public static void ResetAll()
        {
            _failuresPerProfile.Clear();
            _lastFailureTicks.Clear();
            BielikLogger.LogInfo("[CIRCUIT BREAKER] RESET ALL - wszystkie profile odblokowane.");
        }

        /// <summary>
        /// Zwraca raport wszystkich sledzonych profilow (do UI / diagnostyki).
        /// </summary>
        public static System.Collections.Generic.List<CircuitBreakerSnapshot> GetAllSnapshots()
        {
            var result = new System.Collections.Generic.List<CircuitBreakerSnapshot>();
            foreach (var kvp in _failuresPerProfile)
            {
                result.Add(GetSnapshot(kvp.Key));
            }
            return result;
        }
    }

    /// <summary>
    /// Snapshot stanu Circuit Breaker dla jednego profilu.
    /// </summary>
    public class CircuitBreakerSnapshot
    {
        public string ProfileName { get; set; }
        public int Failures { get; set; }
        public int Threshold { get; set; }
        public bool IsTripped { get; set; }
        public long LastFailureUtcTicks { get; set; }

        public DateTime? LastFailureUtc
        {
            get
            {
                if (LastFailureUtcTicks == 0) return null;
                return new DateTime(LastFailureUtcTicks, DateTimeKind.Utc);
            }
        }
    }
}
