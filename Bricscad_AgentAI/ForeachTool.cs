using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ForeachTool : ITool
    {
        public string ActionTag => "[ACTION:FOREACH]";
        public string Description => "Iteruje po liście elementów (rozdzielonych |), wykonując dla każdego zadaną akcję.";

        public string Execute(Document doc, string jsonArgs)
        {
            string cleanArgs = jsonArgs.Replace("\\\"", "\"");
            string rawIter = Regex.Match(cleanArgs, @"\""Iterable\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string iterableString = AgentMemory.InjectVariables(rawIter);
            string[] elementy = iterableString.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries);

            string actionName = Regex.Match(cleanArgs, @"\""Action\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string templateArgs = Regex.Match(cleanArgs, @"\""TemplateArgs\""\s*:\s*\{(.*?)\}", RegexOptions.Singleline).Groups[1].Value;

            for (int i = 0; i < elementy.Length; i++)
            {
                string taskArgs = templateArgs.Replace("$ITEM", elementy[i].Trim()).Replace("$INDEX", (i + 1).ToString());
                taskArgs = AgentMemory.InjectVariables(taskArgs);
                TrainingStudio.WykonywaczTagow(doc, $"[ACTION:{actionName.ToUpper()} {{{taskArgs}}}]");
            }
            return $"WYNIK FOREACH: Wykonano {elementy.Length} cykli.";
        }
        public string Execute(Document doc) => Execute(doc, "");
    }
}