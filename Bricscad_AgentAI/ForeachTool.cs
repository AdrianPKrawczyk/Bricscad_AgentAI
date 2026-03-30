using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ForeachTool : ITool
    {
        public string ActionTag => "[ACTION:FOREACH]";
        public string Description => "Iteruje po listach (np. @Pkt, @Dlugosci). Używaj $ITEM1, $ITEM2, $INDEX.";

        public string Execute(Document doc, string jsonArgs)
        {
            string cleanArgs = jsonArgs.Replace("\\\"", "\"");
            string rawIter = Regex.Match(cleanArgs, @"\""Iterable\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

            string[] iterNames = rawIter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            List<string[]> arrays = new List<string[]>();

            foreach (var name in iterNames)
            {
                string resolved = AgentMemory.InjectVariables(name.Trim());
                string[] elements = resolved.Split(new[] { " | ", "), (" }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < elements.Length; i++)
                {
                    elements[i] = elements[i].Trim();
                    if (elements[i].Contains(",") && !elements[i].StartsWith("(")) elements[i] = "(" + elements[i];
                    if (elements[i].Contains(",") && !elements[i].EndsWith(")")) elements[i] = elements[i] + ")";
                }
                arrays.Add(elements);
            }

            string actionName = Regex.Match(cleanArgs, @"\""Action\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string templateArgs = Regex.Match(cleanArgs, @"\""TemplateArgs\""\s*:\s*\{(.*?)\}", RegexOptions.Singleline).Groups[1].Value;

            int maxLen = arrays.Count > 0 ? arrays[0].Length : 0;
            for (int i = 0; i < maxLen; i++)
            {
                string taskArgs = templateArgs.Replace("$INDEX", (i + 1).ToString());

                if (arrays.Count == 1)
                {
                    taskArgs = taskArgs.Replace("$ITEM1", arrays[0][i]).Replace("$ITEM", arrays[0][i]);
                }
                else
                {
                    for (int a = 0; a < arrays.Count; a++)
                    {
                        if (i < arrays[a].Length) taskArgs = taskArgs.Replace($"$ITEM{a + 1}", arrays[a][i]);
                    }
                }

                taskArgs = AgentMemory.InjectVariables(taskArgs);

                // --- UJAWNIANIE BŁĘDÓW Z WNĘTRZA PĘTLI ---
                string wynikIteracji = TrainingStudio.WykonywaczTagow(doc, $"[ACTION:{actionName.ToUpper()} {{{taskArgs}}}]");
                if (wynikIteracji.StartsWith("BŁĄD"))
                {
                    doc.Editor.WriteMessage($"\n[Agent AI - Błąd w cyklu {i + 1}]: {wynikIteracji}");
                }
            }
            return $"WYNIK FOREACH: Wykonano {maxLen} cykli.";
        }
        public string Execute(Document doc) => Execute(doc, "");
    }
}