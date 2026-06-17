using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class ListPlotDevicesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ListPlotDevicesTool",
                    Description = "Zwraca liste dostepnych w systemie urzadzen drukujacych (ploterow/ploterow PDF) oraz liste wspieranych formatow papieru (CanonicalMediaName) dla aktywnego rysunku. Plotery HP moga uzywac formatow UserXXX albo nazw driver'a (np. 'A4', 'B2', 'Tabloid') - sprawdz to przed ustawianiem MediaName w PageSetupTool.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Filter", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny filtr (case-insensitive). Np. 'HP', 'PDF', 'DWF'. Pusty = wszystkie urzadzenia."
                                }
                            },
                            {
                                "IncludeCanonicalMediaNames", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy dolaczyc liste CanonicalMediaName (true/false, domyslnie true). Lista moze byc dluga dla drukarek z wieloma formatami."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zmiennej do zapisu w pamieci Agenta (bez @)."
                                }
                            }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string filter = args["Filter"]?.ToString();
            bool includeMedia = true;
            if (args["IncludeCanonicalMediaNames"] != null)
            {
                try { includeMedia = args["IncludeCanonicalMediaNames"].Value<bool>(); } catch { }
            }
            string saveAs = args["SaveAs"]?.ToString();

            Database db = doc.Database;
            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        var validator = PlotSettingsValidator.Current;

                        StringCollection deviceList = null;
                        try { deviceList = validator.GetPlotDeviceList(); } catch { }
                        if (deviceList == null) deviceList = new StringCollection();

                        StringCollection mediaList = null;
                        try { mediaList = validator.GetCanonicalMediaNameList(new PlotSettings(false)); } catch { }
                        if (mediaList == null) mediaList = new StringCollection();

                        var sb = new StringBuilder();
                        sb.AppendLine($"LISTA URZADZEN DRUKUJACYCH ({deviceList.Count}):");

                        var matchedDevices = new List<string>();
                        foreach (string dev in deviceList)
                        {
                            if (string.IsNullOrEmpty(dev)) continue;
                            if (!string.IsNullOrEmpty(filter) &&
                                dev.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            matchedDevices.Add(dev);
                            sb.AppendLine($"  - {dev}");
                        }

                        if (matchedDevices.Count == 0)
                        {
                            if (!string.IsNullOrEmpty(filter))
                            {
                                sb.AppendLine();
                                sb.AppendLine($"Brak urzadzen pasujacych do filtra '{filter}'.");
                            }
                        }

                        if (includeMedia)
                        {
                            sb.AppendLine();
                            sb.AppendLine($"LISTA FORMATOW PAPIERU (CanonicalMediaName, {mediaList.Count}):");

                            string filterLc = (filter ?? "").ToLowerInvariant();
                            int mediaShown = 0;
                            foreach (string media in mediaList)
                            {
                                if (string.IsNullOrEmpty(media)) continue;
                                sb.AppendLine($"  - {media}");
                                mediaShown++;
                                if (mediaShown > 200)
                                {
                                    sb.AppendLine($"  ... i {mediaList.Count - mediaShown} wiecej (uzyj IncludeCanonicalMediaNames=false aby ukryc).");
                                    break;
                                }
                            }
                        }

                        sb.AppendLine();
                        sb.AppendLine("UWAGA DLA PLOTEROW HP:");
                        sb.AppendLine("- Plotery HP moga raportowac formaty UserXXX (np. User254, User266) ktore sa niestandardowe.");
                        sb.AppendLine("- Niektore plotery uzywaja nazw driver'a zamiast Canonical (np. 'A4', 'B2', 'Tabloid').");
                        sb.AppendLine("- Jesli nazwa z powyzszej listy nie dziala w PageSetupTool, sprawdz GUI BricsCAD: menu Format -> Plotter Setup -> lista 'Papier' (zawiera nazwy driver'a).");

                        string resultStr = sb.ToString();
                        if (!string.IsNullOrEmpty(saveAs))
                        {
                            AgentMemoryState.Variables[saveAs] = resultStr;
                        }
                        return resultStr;
                    }
                }
            }
            catch (Exception ex)
            {
                return $"BLAD LISTOWANIA PLOTEROW: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        public List<string> Examples => new List<string>
        {
            "{}",
            "{ \"Filter\": \"HP\" }",
            "{ \"Filter\": \"PDF\" }",
            "{ \"IncludeCanonicalMediaNames\": false }",
            "{ \"SaveAs\": \"AvailablePlotters\" }"
        };
    }
}