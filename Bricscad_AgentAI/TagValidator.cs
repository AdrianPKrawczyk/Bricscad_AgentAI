using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace BricsCAD_Agent
{
    public static class TagValidator
    {
        private static Dictionary<string, HashSet<string>> apiCache = null;

        // Ładowanie bazy wiedzy do pamięci
        public static void LoadApiCache()
        {
            if (apiCache != null && apiCache.Count > 0) return;
            apiCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string folderDir = Path.GetDirectoryName(assemblyPath);

            LoadApiFile(Path.Combine(folderDir, "BricsCAD_API_Quick.txt"), false);
            LoadApiFile(Path.Combine(folderDir, "BricsCAD_API_V22.txt"), true);
        }

        private static void LoadApiFile(string path, bool isV22)
        {
            if (!File.Exists(path)) return;

            string content = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var matchesClasses = Regex.Matches(content, @"\b([a-zA-Z0-9_]+)\|(.*?)(?=\b[a-zA-Z0-9_]+\||$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match m in matchesClasses)
            {
                string entName = m.Groups[1].Value.Trim();
                if (entName.Contains(" ") || entName.Length > 35) continue;

                if (entName.Equals("dbtext", StringComparison.OrdinalIgnoreCase)) entName = "DBText";
                else if (entName.Equals("mtext", StringComparison.OrdinalIgnoreCase)) entName = "MText";
                else if (entName.Equals("solid3d", StringComparison.OrdinalIgnoreCase)) entName = "Solid3d";
                else entName = char.ToUpper(entName[0]) + entName.Substring(1).ToLower();

                if (!apiCache.ContainsKey(entName)) apiCache[entName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string propsText = m.Groups[2].Value;

                if (isV22)
                {
                    var mProps = Regex.Matches(propsText, @"Właściwości \(Properties\):\s*(.*?)(?=\.\s*[A-Z]|\.$|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    foreach (Match mFull in mProps)
                    {
                        string[] props = mFull.Groups[1].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string p in props)
                        {
                            string cleanProp = p.Trim().Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                            if (cleanProp.Length > 1 && char.IsUpper(cleanProp[0])) apiCache[entName].Add(cleanProp);
                        }
                    }
                }
                else
                {
                    var mQuick = Regex.Matches(propsText, @"\b([A-Z][A-Za-z0-9_]+)\s*\(([^)]+)\)");
                    foreach (Match mq in mQuick) apiCache[entName].Add(mq.Groups[1].Value);
                }
            }
        }

        public static List<string> ValidateSequence(string text)
        {
            LoadApiCache();
            List<string> errors = new List<string>();

            // --- NOWOŚĆ: Zabezpieczenie przed brakiem plików API ---
            if (apiCache.Count == 0)
            {
                errors.Add("[BŁĄD KRYTYCZNY] Nie udało się załadować bazy wiedzy API! Upewnij się, że pliki BricsCAD_API_Quick.txt oraz BricsCAD_API_V22.txt znajdują się w folderze wtyczki.");
                return errors; // Przerywamy sprawdzanie, żeby nie rzucać fałszywych halucynacji
            }

            // 1. Walidacja globalna nawiasów
            int openSquare = text.Split('[').Length - 1;
            int closeSquare = text.Split(']').Length - 1;
            int openCurly = text.Split('{').Length - 1;
            int closeCurly = text.Split('}').Length - 1;

            if (openSquare != closeSquare) errors.Add($"[SKŁADNIA] Niezgodna liczba nawiasów kwadratowych [ ]. Otwartych: {openSquare}, Zamkniętych: {closeSquare}.");
            if (openCurly != closeCurly) errors.Add($"[SKŁADNIA] Niezgodna liczba nawiasów klamrowych {{ }}. Otwartych: {openCurly}, Zamkniętych: {closeCurly}.");

            // Oczyszczamy tekst do łatwiejszego przetwarzania JSONa
            string cleanText = text.Replace("\\\"", "\"").Replace("\\n", "\n");

            // 2. Walidacja narzędzia SELECT (Dodano \s* przed \] dla bezpieczeństwa spacji)
            var selectMatches = Regex.Matches(cleanText, @"\[SELECT:\s*(\{.*?\})\s*\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match m in selectMatches)
            {
                string json = m.Groups[1].Value;

                string entType = "Entity";
                Match mEnt = Regex.Match(json, @"""EntityType""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (mEnt.Success) entType = mEnt.Groups[1].Value;

                if (!apiCache.ContainsKey(entType) && !entType.Equals("Entity", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"[HALUCYNACJA - SELECT] Nieznana klasa obiektu: '{entType}'.");
                }

                var mCond = Regex.Match(json, @"""Conditions""\s*:\s*\[(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (mCond.Success)
                {
                    var propMatches = Regex.Matches(mCond.Groups[1].Value, @"""Property""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    foreach (Match pm in propMatches)
                    {
                        CheckProperty(entType, pm.Groups[1].Value, "[SELECT]", errors);
                    }
                }
            }

            // 3. Walidacja Narzędzi Akcji (ACTION) (Dodano \s* przed \])
            var actionMatches = Regex.Matches(cleanText, @"\[ACTION:([A-Z_]+)\s*(\{.*?\})\s*\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match m in actionMatches)
            {
                string actionName = m.Groups[1].Value.ToUpper();
                string json = m.Groups[2].Value;

                if (actionName == "SET_PROPERTIES")
                {
                    var mProps = Regex.Match(json, @"""Properties""\s*:\s*\{(.*?)\}", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (mProps.Success)
                    {
                        var keysMatches = Regex.Matches(mProps.Groups[1].Value, @"""([^""]+)""\s*:");
                        foreach (Match km in keysMatches) CheckProperty("Entity", km.Groups[1].Value, $"[{actionName}]", errors);
                    }
                }
                else if (actionName == "READ_PROPERTY")
                {
                    var pm = Regex.Match(json, @"""Property""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    if (pm.Success) CheckProperty("Entity", pm.Groups[1].Value, $"[{actionName}]", errors);
                }
                else if (actionName == "LIST_UNIQUE")
                {
                    var tm = Regex.Match(json, @"""Target""\s*:\s*""Property""", RegexOptions.IgnoreCase);
                    if (tm.Success)
                    {
                        var pm = Regex.Match(json, @"""Property""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                        if (pm.Success) CheckProperty("Entity", pm.Groups[1].Value, $"[{actionName}]", errors);
                    }
                }
                else if (actionName == "USER_CHOICE")
                {
                    var ft = Regex.Match(json, @"""FetchTarget""\s*:\s*""Property""", RegexOptions.IgnoreCase);
                    if (ft.Success)
                    {
                        var pm = Regex.Match(json, @"""FetchProperty""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                        if (pm.Success) CheckProperty("Entity", pm.Groups[1].Value, $"[{actionName}]", errors);
                    }
                }
                // --- NOWOŚĆ: Walidacja dla nowego narzędzia zarządzania warstwami ---
                else if (actionName == "MANAGE_LAYERS")
                {
                    var keysMatches = Regex.Matches(json, @"""([^""]+)""\s*:");
                    string[] validLayerKeys = { "Mode", "Layer", "Layers", "NewName", "Color", "LineWeight", "Linetype", "Transparency", "IsOff", "IsFrozen", "IsLocked", "SourceLayers", "TargetLayer" };
                    HashSet<string> validKeys = new HashSet<string>(validLayerKeys, StringComparer.OrdinalIgnoreCase);

                    foreach (Match km in keysMatches)
                    {
                        string key = km.Groups[1].Value;
                        if (!validKeys.Contains(key))
                        {
                            errors.Add($"[{actionName}] Nieznany parametr (Narzędzie tego nie obsługuje): '{key}'.");
                        }
                    }
                }
            }

            return errors;
        }

        private static void CheckProperty(string entType, string prop, string context, List<string> errors)
        {
            string[] visualProps = { "VisualColor", "VisualLinetype", "VisualLineWeight", "VisualTransparency", "MidPoint", "Volume", "Centroid", "Area", "Length", "Angle", "Rotation" };
            foreach (string vp in visualProps) if (prop.Equals(vp, StringComparison.OrdinalIgnoreCase)) return;
            if (prop.Contains(".")) return;

            bool found = false;
            if (apiCache.ContainsKey(entType) && apiCache[entType].Contains(prop)) found = true;
            else if (apiCache.ContainsKey("Entity") && apiCache["Entity"].Contains(prop)) found = true;
            else
            {
                foreach (var kvp in apiCache)
                {
                    if (kvp.Value.Contains(prop)) { found = true; break; }
                }
            }

            if (!found)
            {
                errors.Add($"{context} Nieznana właściwość (API jej nie zawiera): '{prop}'.");
            }
        }
    }
}