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

        public static void Clear()
        {
            _blackboard.Clear();
        }

        public static Dictionary<string, string> GetAll()
        {
            return new Dictionary<string, string>(_blackboard);
        }
    }
}
