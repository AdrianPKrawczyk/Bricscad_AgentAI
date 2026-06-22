using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Blackboard (Współdzielony Stan) – pamięć globalna współdzielona pomiędzy Supervisor'em a Worker'ami.
    /// Zapobiega utracie kontekstu pomiędzy przełączeniami agentów.
    /// </summary>
    public static class SharedMemoryState
    {
        // Używamy ConcurrentDictionary dla bezpieczeństwa wątkowego, jeśli agenci będą kiedyś działać równolegle.
        private static readonly ConcurrentDictionary<string, string> _blackboard = new ConcurrentDictionary<string, string>();

        public static void Write(string key, string value)
        {
            _blackboard[key] = value;
        }

        public static string Read(string key)
        {
            if (_blackboard.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }

        /// <summary>
        /// Sprawdza czy klucz istnieje w Blackboard. Rozni sie od Read tym,
        /// ze nie alokuje stringa wynikowego - do szybkich testow obecnosci
        /// (np. czy para Chain of Evidence 'before' zostala juz zapisana).
        /// </summary>
        public static bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _blackboard.ContainsKey(key);
        }

        public static void Clear()
        {
            _blackboard.Clear();
        }

        public static void LoadFromDictionary(Dictionary<string, string> source)
        {
            _blackboard.Clear();
            if (source != null)
            {
                foreach (var kvp in source)
                {
                    _blackboard[kvp.Key] = kvp.Value;
                }
            }
        }

        public static Dictionary<string, string> GetAll()
        {
            return new Dictionary<string, string>(_blackboard);
        }
    }
}
