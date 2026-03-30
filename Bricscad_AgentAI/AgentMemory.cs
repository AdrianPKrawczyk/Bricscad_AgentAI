using System;
using System.Collections.Generic;
using System.Linq;

namespace BricsCAD_Agent
{
    public static class AgentMemory
    {
        // Globalny słownik przechowujący nasze zmienne
        public static Dictionary<string, string> Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Funkcja do wstrzykiwania zmiennych w locie
        public static string InjectVariables(string input)
        {
            if (string.IsNullOrEmpty(input) || Variables == null) return input;

            string output = input;

            // Sortowanie kluczy od najdłuższego do najkrótszego zapobiega nadpisywaniu podobnych nazw
            var keys = Variables.Keys.OrderByDescending(k => k.Length).ToList();

            foreach (var key in keys)
            {
                output = output.Replace("@" + key, Variables[key]);
            }

            return output;
        }
    }
}