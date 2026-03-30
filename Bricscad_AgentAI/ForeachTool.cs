using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ForeachTool : ITool
    {
        public string ActionTag => "[ACTION:FOREACH]";
        public string Description => "Wykonuje akcję dla każdego elementu z listy (rozdzielonej | lub , ).";

        public string Execute(Document doc, string jsonArgs)
        {
            string cleanArgs = jsonArgs.Replace("\\\"", "\"");

            string rawIter = Regex.Match(cleanArgs, @"\""Iterable\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string iterableString = AgentMemory.InjectVariables(rawIter);

            // POPRAWKA: Rozdzielamy po | LUB po przecinku, o ile nie jest on wewnątrz nawiasów punktu
            // Uproszczona wersja: dzielimy po " | " lub "), ("
            string[] elementy = iterableString.Split(new[] { " | ", "), (" }, StringSplitOptions.RemoveEmptyEntries);

            // Jeśli split po "), (" zadziałał, musimy naprawić nawiasy
            for (int i = 0; i < elementy.Length; i++)
            {
                if (!elementy[i].StartsWith("(")) elementy[i] = "(" + elementy[i];
                if (!elementy[i].EndsWith(")")) elementy[i] = elementy[i] + ")";
            }

            string actionName = Regex.Match(cleanArgs, @"\""Action\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string templateArgs = Regex.Match(cleanArgs, @"\""TemplateArgs\""\s*:\s*\{(.*?)\}", RegexOptions.Singleline).Groups[1].Value;

            for (int i = 0; i < elementy.Length; i++)
            {
                string taskArgs = templateArgs.Replace("$ITEM", elementy[i].Trim())
                                              .Replace("$INDEX", (i + 1).ToString());

                taskArgs = AgentMemory.InjectVariables(taskArgs);
                TrainingStudio.WykonywaczTagow(doc, $"[ACTION:{actionName.ToUpper()} {{{taskArgs}}}]");
            }
            return $"WYNIK FOREACH: Wykonano {elementy.Length} cykli.";
        }
        public string Execute(Document doc) => Execute(doc, "");
    }
}   