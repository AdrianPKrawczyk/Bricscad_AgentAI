using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class UpdateWentCadWindowTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "UpdateWentCadWindow",
                    Description = "Aktualizuje okno WATT WentCad po WindowId: wymiary, przypisanie do sciany/pomieszczenia, pewnosc i opis. Nie wymaga DLL WentCad.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "WindowId", new ToolParameter { Type = "string", Description = "Id okna do aktualizacji." } },
                            { "RoomId", new ToolParameter { Type = "string", Description = "Opcjonalny RoomId." } },
                            { "WallId", new ToolParameter { Type = "string", Description = "Opcjonalny WallId." } },
                            { "Width", new ToolParameter { Type = "number", Description = "Szerokosc w metrach." } },
                            { "Height", new ToolParameter { Type = "number", Description = "Wysokosc w metrach." } },
                            { "SillHeight", new ToolParameter { Type = "number", Description = "Wysokosc parapetu w metrach." } },
                            { "Confidence", new ToolParameter { Type = "number", Description = "Pewnosc 0..1." } },
                            { "Message", new ToolParameter { Type = "string", Description = "Uwagi diagnostyczne." } }
                        },
                        Required = new List<string> { "WindowId" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"WindowId\":\"win-...\",\"Width\":1.5,\"Height\":1.5}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            var windows = WentCadProjectStore.ObjectMap(WentCadProjectStore.Thermal(project), "Windows");
            string windowId = WentCadProjectStore.Clean(args["WindowId"]);
            var window = windows[windowId] as JObject;
            if (window == null) return "Error: Nie znaleziono WindowId.";
            foreach (string name in new[] { "RoomId", "WallId", "Message" })
            {
                if (args[name] != null) window[name] = WentCadProjectStore.Clean(args[name]) ?? "";
            }
            foreach (string name in new[] { "Width", "Height", "SillHeight", "Confidence" })
            {
                if (args[name] != null) window[name] = ToDouble(args[name], ToDouble(window[name], 0));
            }
            window["Area"] = ToDouble(window["Width"], 0) * ToDouble(window["Height"], 0);
            WentCadProjectStore.Save(doc, project);
            WentCadProjectStore.SaveContractToDwg(doc, project, true);
            return window.ToString(Formatting.Indented);
        }

        private static double ToDouble(JToken token, double fallback)
        {
            double value;
            return token != null && double.TryParse(token.ToString().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
    }
}
