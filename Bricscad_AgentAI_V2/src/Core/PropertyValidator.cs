using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Statyczna tarcza anty-halucynacyjna. Weryfikuje czy dana nazwa właściwości 
    /// faktycznie istnieje w API BricsCAD dla danej klasy obiektu.
    /// </summary>
    public static class PropertyValidator
    {
        private static Dictionary<string, HashSet<string>> _apiCache = null;
        private static readonly object _lock = new object();

        // Lista właściwości wirtualnych Agenta, które są zawsze dopuszczalne
        private static readonly HashSet<string> _virtualProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MidPoint", "Area", "Length", "Volume", "Centroid", "Angle", "Rotation", "Value", "Text", "TextString",
            "VisualColor", "VisualLinetype", "VisualLineWeight", "VisualTransparency"
        };

        /// <summary>
        /// Sprawdza, czy dana właściwość jest poprawna dla wskazanej klasy obiektu.
        /// </summary>
        public static bool IsPropertyValid(string className, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName)) return false;

            EnsureLoaded();

            // Obsługa właściwości zagnieżdżonych (np. Position.Z) - sprawdzamy główny człon
            string rootProp = propertyName.Contains(".") ? propertyName.Split('.')[0] : propertyName;

            // 1. Zawsze dopuszczaj właściwości wirtualne Agenta
            if (_virtualProperties.Contains(rootProp)) return true;

            // 2. Sprawdź w cache bazy wiedzy API
            if (_apiCache.Count == 0) return true; // Failsafe: jeśli baza nie istnieje, przepuszczamy (logika biznesowa ModifyTool i tak sprawdzi Refleksję)

            // Sprawdzamy konkretną klasę
            if (_apiCache.TryGetValue(className, out var classProps))
            {
                if (classProps.Contains(rootProp)) return true;
            }

            // Sprawdzamy klasę bazową Entity (Globalne właściwości)
            if (_apiCache.TryGetValue("Entity", out var entityProps))
            {
                if (entityProps.Contains(rootProp)) return true;
            }

            // 3. Sprawdzenie heurystyczne: czy jakakolwiek klasa to posiada?
            // (LLM czasem podaje dobre właściwości, ale myli się co do nazwy klasy w hierarchii)
            foreach (var props in _apiCache.Values)
            {
                if (props.Contains(rootProp)) return true;
            }

            return false;
        }

        private static void EnsureLoaded()
        {
            if (_apiCache != null) return;

            lock (_lock)
            {
                if (_apiCache != null) return;

                _apiCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    string assemblyPath = Assembly.GetExecutingAssembly().Location;
                    string resourceDir = Path.Combine(Path.GetDirectoryName(assemblyPath), "resources");

                    // Jeśli nie ma w bin, szukamy w folderze projektu (fallback dla dev)
                    if (!Directory.Exists(resourceDir))
                    {
                        // Zakładamy strukturę: Bricscad_AgentAI_V2/bin/Debug/net48/... -> Bricscad_AgentAI_V2/resources/
                        resourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "resources");
                    }

                    LoadFile(Path.Combine(resourceDir, "BricsCAD_API_Quick.txt"), false);
                    LoadFile(Path.Combine(resourceDir, "BricsCAD_API_V22.txt"), true);
                }
                catch
                {
                    // W razie błędu ładowania (np. brak plików) logujemy i zostawiamy pusty cache
                }
            }
        }

        private static void LoadFile(string path, bool isV22)
        {
            if (!File.Exists(path)) return;

            string content = File.ReadAllText(path, System.Text.Encoding.UTF8);
            
            // Regex wyciągający bloki Klasa|Treść
            var classMatches = Regex.Matches(content, @"\b([a-zA-Z0-9_]+)\|(.*?)(?=\b[a-zA-Z0-9_]+\||$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match m in classMatches)
            {
                string entNameRaw = m.Groups[1].Value.Trim();
                if (entNameRaw.Contains(" ") || entNameRaw.Length > 35) continue;

                // Normalizacja nazw najczęstszych klas
                string entName = NormalizeClassName(entNameRaw);

                if (!_apiCache.ContainsKey(entName)) 
                    _apiCache[entName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string propsText = m.Groups[2].Value;

                if (isV22)
                {
                    // Parsowanie formatu pełnego V22
                    var mProps = Regex.Matches(propsText, @"Właściwości \(Properties\):\s*(.*?)(?=\.\s*[A-Z]|\.$|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    foreach (Match mFull in mProps)
                    {
                        string[] props = mFull.Groups[1].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string p in props)
                        {
                            string cleanProp = p.Trim().Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                            if (cleanProp.Length > 1 && char.IsUpper(cleanProp[0]))
                                _apiCache[entName].Add(cleanProp);
                        }
                    }
                }
                else
                {
                    // Parsowanie formatu Quick
                    var mQuick = Regex.Matches(propsText, @"\b([A-Z][A-Za-z0-9_]+)\s*\(([^)]+)\)");
                    foreach (Match mq in mQuick) 
                        _apiCache[entName].Add(mq.Groups[1].Value);
                }
            }
        }

        private static string NormalizeClassName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            
            if (name.Equals("dbtext", StringComparison.OrdinalIgnoreCase)) return "DBText";
            if (name.Equals("mtext", StringComparison.OrdinalIgnoreCase)) return "MText";
            if (name.Equals("solid3d", StringComparison.OrdinalIgnoreCase)) return "Solid3d";
            if (name.Equals("mleader", StringComparison.OrdinalIgnoreCase)) return "MLeader";

            // Default Capitalization
            return char.ToUpper(name[0]) + name.Substring(1).ToLower();
        }
    }
}
