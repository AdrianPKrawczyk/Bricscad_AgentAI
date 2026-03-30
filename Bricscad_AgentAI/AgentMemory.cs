using System;
using System.Collections.Generic;

namespace BricsCAD_Agent
{
    public static class AgentMemory
    {
        // Globalny słownik przechowujący nasze zmienne
        public static Dictionary<string, string> Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Funkcja do wstrzykiwania zmiennych w locie (użyjesz jej później w TagValidator lub CommandRunnerze)
        public static string InjectVariables(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            string output = input;
            foreach (var kvp in Variables)
            {
                output = output.Replace("@" + kvp.Key, kvp.Value);
            }
            return output;
        }
    }
}