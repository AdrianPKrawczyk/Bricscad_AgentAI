using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;

namespace BricsCAD_Agent
{
    public class UserInputTool : ITool
    {
        public string ActionTag => "[ACTION:USER_INPUT]";
        public string Description => "Prosi użytkownika o wpisanie tekstu w konsoli lub wskazanie punktu/punktów na ekranie CAD.";

        public string Execute(Document doc, string jsonArgs)
        {
            string type = "String";
            Match mType = Regex.Match(jsonArgs, @"\""Type\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mType.Success) type = mType.Groups[1].Value;

            string promptMsg = "Podaj wartość:";
            Match mPrompt = Regex.Match(jsonArgs, @"\""Prompt\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mPrompt.Success) promptMsg = mPrompt.Groups[1].Value;

            string saveAs = "";
            Match mSave = Regex.Match(jsonArgs, @"\""SaveAs\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mSave.Success) saveAs = mSave.Groups[1].Value;

            Editor ed = doc.Editor;

            // 1. ZWYKŁY TEKST Z KONSOLI
            if (type.Equals("String", StringComparison.OrdinalIgnoreCase))
            {
                PromptStringOptions pso = new PromptStringOptions($"\n[Pytanie od AI] {promptMsg}: ");
                pso.AllowSpaces = true;
                PromptResult pr = ed.GetString(pso);

                if (pr.Status == PromptStatus.OK)
                {
                    if (!string.IsNullOrEmpty(saveAs)) AgentMemory.Variables[saveAs] = pr.StringResult;
                    return $"WYNIK: Użytkownik wpisał: {pr.StringResult}" + (!string.IsNullOrEmpty(saveAs) ? $" (Zapisano jako @{saveAs})" : "");
                }
                return "WYNIK: Użytkownik anulował wprowadzanie (ESC).";
            }
            // 2. POJEDYNCZY PUNKT Z EKRANU
            else if (type.Equals("Point", StringComparison.OrdinalIgnoreCase))
            {
                PromptPointOptions ppo = new PromptPointOptions($"\n[Prośba od AI] {promptMsg}: ");
                PromptPointResult ppr = ed.GetPoint(ppo);

                if (ppr.Status == PromptStatus.OK)
                {
                    string czystyPunkt = FormatPoint(ppr.Value);
                    if (!string.IsNullOrEmpty(saveAs)) AgentMemory.Variables[saveAs] = czystyPunkt;
                    return $"WYNIK: Użytkownik wskazał punkt: {czystyPunkt}" + (!string.IsNullOrEmpty(saveAs) ? $" (Zapisano jako @{saveAs})" : "");
                }
                return "WYNIK: Użytkownik anulował wskazywanie punktu.";
            }
            // 3. WIELE PUNKTÓW Z EKRANU
            else if (type.Equals("Points", StringComparison.OrdinalIgnoreCase))
            {
                List<string> punkty = new List<string>();
                bool getting = true;
                int i = 1;

                while (getting)
                {
                    PromptPointOptions ppo = new PromptPointOptions($"\n[Prośba od AI] {promptMsg} [Punkt nr {i}] (lub ENTER by zakończyć): ");
                    ppo.AllowNone = true;
                    PromptPointResult ppr = ed.GetPoint(ppo);

                    if (ppr.Status == PromptStatus.OK)
                    {
                        punkty.Add(FormatPoint(ppr.Value));
                        i++;
                    }
                    else getting = false;
                }

                if (punkty.Count > 0)
                {
                    if (!string.IsNullOrEmpty(saveAs)) AgentMemory.Variables[saveAs] = string.Join(" | ", punkty);
                    return $"WYNIK: Użytkownik wskazał punkty ({punkty.Count}): {string.Join(", ", punkty)}" + (!string.IsNullOrEmpty(saveAs) ? $" (Zapisano jako @{saveAs})" : "");
                }
                return "WYNIK: Użytkownik nie wskazał żadnych punktów.";
            }

            return $"WYNIK: BŁĄD - Nieznany typ interakcji '{type}'. Obsługiwane to String, Point, Points.";
        }

        public string Execute(Document doc) => Execute(doc, "");

        // --- Funkcja pomocnicza wymuszająca kropkę dziesiętną ---
        private string FormatPoint(Teigha.Geometry.Point3d pt)
        {
            // Format "0.####" upewnia się, że ucięte zostaną niepotrzebne zera na końcu, a InvariantCulture wymusza kropkę
            string x = pt.X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            string y = pt.Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            string z = pt.Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

            return $"({x}, {y}, {z})";
        }
    }
}