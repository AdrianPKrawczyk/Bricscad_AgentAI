using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using CadLayout = Teigha.DatabaseServices.Layout;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class BatchImportLayoutsTool : IToolV2
    {
        private const double DefaultFuzzyThreshold = 0.72;

        private class LayoutInfo
        {
            public string Name { get; set; }
            public string NormalizedName { get; set; }
            public bool IsModel { get; set; }
            public int TabOrder { get; set; }
            public string MediaName { get; set; }
            public string PaperSize { get; set; }
        }

        private class MatchResult
        {
            public string Query { get; set; }
            public List<LayoutInfo> Matches { get; set; } = new List<LayoutInfo>();
            public string Mode { get; set; }
            public string Error { get; set; }
        }

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "BatchImportLayoutsTool",
                    Description = "Bezpiecznie importuje wiele layoutow z DWG/DWT bez Foreach. Najpierw czyta layouty w pliku zrodlowym, dopasowuje nazwy po dokladnej nazwie, fragmencie albo fuzzy, a potem importuje znalezione arkusze sekwencyjnie. Uzywaj gdy user prosi o import kilku arkuszy lub podaje niepelne/niedokladne nazwy.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "SourcePath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka do pliku zrodlowego DWG/DWT."
                                }
                            },
                            {
                                "LayoutNames", new ToolParameter
                                {
                                    Type = "array",
                                    Items = new JObject { ["type"] = "string" },
                                    Description = "Lista nazw, fragmentow nazw albo niedokladnych nazw layoutow do importu, np. [\"PW.IS.W.01\", \"PW.IS.W.02\"] albo [\"IS.W\"]."
                                }
                            },
                            {
                                "MatchMode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tryb dopasowania: Auto (domyslnie), Exact, Contains, Fuzzy. Auto: exact -> contains -> fuzzy."
                                }
                            },
                            {
                                "AllowMultipleMatches", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Jesli true (domyslnie), fragment nazwy moze zaimportowac wiele pasujacych layoutow. Przy false narzedzie wymaga pojedynczego dopasowania."
                                }
                            },
                            {
                                "IncludeModel", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy dopuszczac layout Model jako kandydat (domyslnie false)."
                                }
                            },
                            {
                                "ImportPlotSettings", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy importowac Page Setup (domyslnie true)."
                                }
                            },
                            {
                                "ImportEntities", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy importowac zawartosc/geometrie layoutu (domyslnie true)."
                                }
                            },
                            {
                                "OverwriteIfExists", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy nadpisac istniejace layouty docelowe (domyslnie false)."
                                }
                            },
                            {
                                "StopOnError", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Jesli true (domyslnie), zatrzymuje caly import po pierwszym bledzie, zeby nie kontynuowac po uszkodzonej transakcji Teigha."
                                }
                            },
                            {
                                "TargetPrefix", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny prefiks nazwy docelowej, np. 'IMP_'."
                                }
                            },
                            {
                                "TargetSuffix", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny sufiks nazwy docelowej, np. '_KOPIA'."
                                }
                            },
                            {
                                "PreviewOnly", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Jesli true, tylko pokazuje dopasowania i nie importuje layoutow."
                                }
                            }
                        },
                        Required = new List<string> { "SourcePath", "LayoutNames" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var agentUi = Bricscad_AgentAI_V2.UI.AgentControl.Instance;
            if (agentUi == null || !agentUi.IsHandleCreated)
            {
                return "BLAD BATCH IMPORT: Nie mozna bezpiecznie wykonac importu layoutow - AgentControl (UI) nie jest zainicjalizowany. Otworz panel agenta komenda 'AI' i sprobuj ponownie.";
            }

            if (agentUi.InvokeRequired)
            {
                try
                {
                    return (string)agentUi.Invoke((Func<string>)(() => ExecuteOnCadThread(doc, args)));
                }
                catch (Exception ex)
                {
                    return $"BLAD BATCH IMPORT: Nie udalo sie zdispatchowac na glowny watek UI: {ex.Message}";
                }
            }

            return ExecuteOnCadThread(doc, args);
        }

        private string ExecuteOnCadThread(Document doc, JObject args)
        {
            string sourcePath = args["SourcePath"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(sourcePath))
                return "BLAD: SourcePath jest wymagany.";

            List<string> requested = ReadStringArray(args, "LayoutNames");
            if (requested.Count == 0)
                return "BLAD: LayoutNames musi zawierac co najmniej jedna nazwe albo fragment nazwy layoutu.";

            string matchMode = args["MatchMode"]?.ToString() ?? "Auto";
            bool allowMultiple = ReadBool(args, "AllowMultipleMatches", true);
            bool includeModel = ReadBool(args, "IncludeModel", false);
            bool importPlotSettings = ReadBool(args, "ImportPlotSettings", true);
            bool importEntities = ReadBool(args, "ImportEntities", true);
            bool overwrite = ReadBool(args, "OverwriteIfExists", false);
            bool stopOnError = ReadBool(args, "StopOnError", true);
            bool previewOnly = ReadBool(args, "PreviewOnly", false);
            string targetPrefix = args["TargetPrefix"]?.ToString() ?? "";
            string targetSuffix = args["TargetSuffix"]?.ToString() ?? "";

            string resolvedPath = LayoutHelpers.ValidateAndResolvePath(sourcePath);
            if (!File.Exists(resolvedPath))
                return $"BLAD: Plik zrodlowy nie istnieje: '{resolvedPath}'.";

            List<LayoutInfo> sourceLayouts;
            string readError;
            if (!TryReadSourceLayouts(resolvedPath, includeModel, out sourceLayouts, out readError))
                return readError;

            if (sourceLayouts.Count == 0)
                return $"BLAD: W pliku '{resolvedPath}' nie znaleziono layoutow do importu.";

            var matchResults = new List<MatchResult>();
            var selected = new List<LayoutInfo>();
            var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string query in requested)
            {
                MatchResult match = ResolveQuery(query, sourceLayouts, matchMode, allowMultiple);
                matchResults.Add(match);
                if (!string.IsNullOrWhiteSpace(match.Error)) continue;

                foreach (LayoutInfo layout in match.Matches)
                {
                    if (selectedNames.Add(layout.Name))
                        selected.Add(layout);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"BATCH IMPORT LAYOUTOW z '{Path.GetFileName(resolvedPath)}'");
            sb.AppendLine($"Dostepne layouty zrodlowe: {sourceLayouts.Count}.");
            sb.AppendLine("Dopasowania:");
            foreach (MatchResult match in matchResults)
            {
                if (!string.IsNullOrWhiteSpace(match.Error))
                {
                    sb.AppendLine($"  - '{match.Query}' -> BLAD: {match.Error}");
                }
                else
                {
                    sb.AppendLine($"  - '{match.Query}' -> {string.Join(", ", match.Matches.Select(m => m.Name))} ({match.Mode})");
                }
            }

            if (selected.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("BLAD: Nie wybrano zadnego layoutu do importu.");
                sb.AppendLine("Dostepne layouty:");
                foreach (LayoutInfo layout in sourceLayouts.Take(40))
                    sb.AppendLine($"  - {layout.Name}");
                if (sourceLayouts.Count > 40)
                    sb.AppendLine($"  ... oraz {sourceLayouts.Count - 40} kolejnych.");
                return sb.ToString().TrimEnd();
            }

            sb.AppendLine();
            sb.AppendLine($"Wybrane do importu: {selected.Count}: {string.Join(", ", selected.Select(l => l.Name))}");

            if (previewOnly)
            {
                sb.AppendLine("PREVIEW ONLY: Nie wykonano importu.");
                return sb.ToString().TrimEnd();
            }

            var importer = new ImportLayoutTemplateTool();
            int successCount = 0;
            var errors = new List<string>();

            foreach (LayoutInfo layout in selected)
            {
                string targetName = targetPrefix + layout.Name + targetSuffix;
                var importArgs = new JObject
                {
                    ["SourcePath"] = resolvedPath,
                    ["SourceLayoutName"] = layout.Name,
                    ["TargetLayoutName"] = targetName,
                    ["ImportPlotSettings"] = importPlotSettings,
                    ["ImportEntities"] = importEntities,
                    ["OverwriteIfExists"] = overwrite,
                    ["SelectObject"] = false,
                    ["SuppressUI"] = true
                };

                string result = importer.Execute(doc, importArgs);
                if (result.StartsWith("SUKCES", StringComparison.OrdinalIgnoreCase))
                {
                    successCount++;
                    sb.AppendLine($"  OK: {layout.Name} -> {targetName}");
                }
                else
                {
                    string error = $"{layout.Name}: {result}";
                    errors.Add(error);
                    sb.AppendLine($"  BLAD: {error}");

                    if (stopOnError)
                    {
                        sb.AppendLine("PRZERWANO: StopOnError=true. Nie kontynuuje po bledzie importu layoutu.");
                        break;
                    }
                }
            }

            sb.AppendLine();
            if (errors.Count == 0)
            {
                sb.AppendLine($"SUKCES: Zaimportowano {successCount}/{selected.Count} layoutow.");
            }
            else
            {
                sb.AppendLine($"BLAD CZESCIOWY BATCH IMPORT: Zaimportowano {successCount}/{selected.Count} layoutow. Bledy: {errors.Count}.");
            }

            try { doc.Editor.UpdateScreen(); } catch { }
            return sb.ToString().TrimEnd();
        }

        private static bool TryReadSourceLayouts(string resolvedPath, bool includeModel, out List<LayoutInfo> layouts, out string error)
        {
            layouts = new List<LayoutInfo>();
            error = null;

            try
            {
                using (Database sourceDb = new Database(false, true))
                {
                    sourceDb.ReadDwgFile(resolvedPath, FileOpenMode.OpenForReadAndAllShare, true, "");
                    using (Transaction sourceTr = sourceDb.TransactionManager.StartTransaction())
                    {
                        DBDictionary layoutDict = sourceTr.GetObject(sourceDb.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
                        if (layoutDict == null)
                        {
                            error = "BLAD: Nie mozna odczytac slownika layoutow w pliku zrodlowym.";
                            return false;
                        }

                        foreach (DBDictionaryEntry entry in layoutDict)
                        {
                            try
                            {
                                CadLayout layout = sourceTr.GetObject(entry.Value, OpenMode.ForRead) as CadLayout;
                                if (layout == null) continue;
                                if (layout.ModelType && !includeModel) continue;

                                string size = "";
                                try
                                {
                                    size = string.Format(CultureInfo.InvariantCulture, "{0:F1}x{1:F1}mm", layout.PlotPaperSize.X, layout.PlotPaperSize.Y);
                                }
                                catch { }

                                layouts.Add(new LayoutInfo
                                {
                                    Name = layout.LayoutName ?? entry.Key,
                                    NormalizedName = Normalize(layout.LayoutName ?? entry.Key),
                                    IsModel = layout.ModelType,
                                    TabOrder = layout.TabOrder,
                                    MediaName = layout.CanonicalMediaName ?? "",
                                    PaperSize = size
                                });
                            }
                            catch { }
                        }

                        sourceTr.Commit();
                    }
                }

                layouts = layouts.OrderBy(l => l.TabOrder).ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList();
                return true;
            }
            catch (Exception ex)
            {
                error = $"BLAD ODCZYTU LAYOUTOW ZRODLOWYCH: {ex.Message}";
                return false;
            }
        }

        private static MatchResult ResolveQuery(string query, List<LayoutInfo> layouts, string matchMode, bool allowMultiple)
        {
            var result = new MatchResult { Query = query ?? "" };
            string normalizedQuery = Normalize(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                result.Error = "pusta nazwa/fragment layoutu";
                return result;
            }

            string mode = (matchMode ?? "Auto").Trim();
            bool exactOnly = mode.Equals("Exact", StringComparison.OrdinalIgnoreCase);
            bool containsOnly = mode.Equals("Contains", StringComparison.OrdinalIgnoreCase);
            bool fuzzyOnly = mode.Equals("Fuzzy", StringComparison.OrdinalIgnoreCase);

            if (!containsOnly && !fuzzyOnly)
            {
                var exact = layouts.Where(l => l.NormalizedName.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase)).ToList();
                if (exact.Count > 0)
                {
                    result.Matches = exact.Take(1).ToList();
                    result.Mode = "exact";
                    return result;
                }
            }

            if (exactOnly)
            {
                result.Error = "brak dokladnego dopasowania";
                return result;
            }

            if (!fuzzyOnly && normalizedQuery.Length >= 2)
            {
                var contains = layouts.Where(l => l.NormalizedName.Contains(normalizedQuery)).ToList();
                if (contains.Count > 0)
                {
                    if (!allowMultiple && contains.Count > 1)
                    {
                        result.Error = $"fragment pasuje do wielu layoutow: {string.Join(", ", contains.Select(l => l.Name).Take(12))}";
                        return result;
                    }

                    result.Matches = contains;
                    result.Mode = "contains";
                    return result;
                }
            }

            if (containsOnly)
            {
                result.Error = "brak dopasowania po fragmencie";
                return result;
            }

            var scored = layouts
                .Select(l => new { Layout = l, Score = Similarity(normalizedQuery, l.NormalizedName) })
                .Where(x => x.Score >= DefaultFuzzyThreshold)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Layout.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (scored.Count == 0)
            {
                result.Error = "brak dopasowania fuzzy";
                return result;
            }

            double bestScore = scored[0].Score;
            var best = scored.Where(x => bestScore - x.Score <= 0.04).Select(x => x.Layout).ToList();
            if (best.Count > 1)
            {
                result.Error = $"niejednoznaczne fuzzy ({bestScore:F2}): {string.Join(", ", best.Select(l => l.Name).Take(12))}";
                return result;
            }

            result.Matches = new List<LayoutInfo> { scored[0].Layout };
            result.Mode = $"fuzzy {scored[0].Score:F2}";
            return result;
        }

        private static List<string> ReadStringArray(JObject args, string propertyName)
        {
            var values = new List<string>();
            JToken token = args[propertyName];
            if (token == null) return values;

            if (token.Type == JTokenType.Array)
            {
                foreach (JToken item in token)
                {
                    string value = item.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
                }
            }
            else
            {
                string value = token.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
            }

            return values;
        }

        private static bool ReadBool(JObject args, string propertyName, bool defaultValue)
        {
            if (args[propertyName] == null) return defaultValue;
            try { return args[propertyName].Value<bool>(); }
            catch { return defaultValue; }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            string decomposed = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in decomposed)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return Regex.Replace(sb.ToString(), @"\s+", "");
        }

        private static double Similarity(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return 1.0;
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return 0.0;

            int distance = LevenshteinDistance(left, right);
            int maxLength = Math.Max(left.Length, right.Length);
            if (maxLength == 0) return 1.0;
            return 1.0 - ((double)distance / maxLength);
        }

        private static int LevenshteinDistance(string left, string right)
        {
            int[,] dp = new int[left.Length + 1, right.Length + 1];
            for (int i = 0; i <= left.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= right.Length; j++) dp[0, j] = j;

            for (int i = 1; i <= left.Length; i++)
            {
                for (int j = 1; j <= right.Length; j++)
                {
                    int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }

            return dp[left.Length, right.Length];
        }

        public List<string> Examples => new List<string>
        {
            "{ \"SourcePath\": \"C:/projekty/source.dwg\", \"LayoutNames\": [\"PW.IS.W.01\", \"PW.IS.W.02\"] }",
            "{ \"SourcePath\": \"C:/projekty/source.dwg\", \"LayoutNames\": [\"IS.W\"], \"MatchMode\": \"Contains\" }",
            "{ \"SourcePath\": \"C:/projekty/source.dwg\", \"LayoutNames\": [\"W01\"], \"PreviewOnly\": true }"
        };
    }
}
