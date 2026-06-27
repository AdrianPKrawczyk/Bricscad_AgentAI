using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ManageWentCadFloorsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageWentCadFloors",
                    Description = "Tworzy, aktualizuje albo usuwa kondygnacje WentCad w .wentcad. Obsluguje rzedna, wysokosci, punkt bazowy i powiazanie z zakresem Bielik.DrukWidoki.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Action", new ToolParameter { Type = "string", Description = "Operacja.", Enum = new List<string> { "upsert", "delete" } } },
                            { "FloorId", new ToolParameter { Type = "string", Description = "Id kondygnacji. Przy upsert moze byc puste - wtedy zostanie utworzone." } },
                            { "Name", new ToolParameter { Type = "string", Description = "Nazwa kondygnacji, np. Parter." } },
                            { "Order", new ToolParameter { Type = "integer", Description = "Kolejnosc kondygnacji." } },
                            { "Elevation", new ToolParameter { Type = "number", Description = "Rzedna kondygnacji." } },
                            { "HeightNet", new ToolParameter { Type = "number", Description = "Wysokosc netto." } },
                            { "HeightTotal", new ToolParameter { Type = "number", Description = "Wysokosc calkowita/strop-strop." } },
                            { "BasePointX", new ToolParameter { Type = "number", Description = "Wspolrzedna X punktu bazowego tej kondygnacji." } },
                            { "BasePointY", new ToolParameter { Type = "number", Description = "Wspolrzedna Y punktu bazowego tej kondygnacji." } },
                            { "BasePointZ", new ToolParameter { Type = "number", Description = "Wspolrzedna Z punktu bazowego tej kondygnacji." } },
                            { "BasePointDescription", new ToolParameter { Type = "string", Description = "Opis punktu bazowego, najlepiej z koordynatami, np. Przeciecie osi A/1 (-3, 77)." } },
                            { "DrukWidokiViewId", new ToolParameter { Type = "string", Description = "Opcjonalny identyfikator zakresu z Bielik.DrukWidoki." } }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"Action\":\"upsert\",\"Name\":\"Parter\",\"Elevation\":0,\"HeightNet\":3,\"HeightTotal\":3.5,\"BasePointX\":-3,\"BasePointY\":-3,\"BasePointDescription\":\"Przeciecie osi A/1 (-3, -3)\"}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            string action = (WentCadProjectStore.Clean(args["Action"]) ?? "upsert").ToLowerInvariant();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            JObject result;

            if (action == "delete")
            {
                bool deleted = WentCadProjectStore.DeleteItem(project, "Floors", WentCadProjectStore.Clean(args["FloorId"]));
                result = new JObject { ["Deleted"] = deleted, ["FloorId"] = args["FloorId"] };
            }
            else
            {
                result = WentCadProjectStore.UpsertFloor(project, args);
            }

            WentCadProjectStore.Save(doc, project);
            WentCadProjectStore.SaveContractToDwg(doc, project, false);
            return new JObject
            {
                ["Result"] = result,
                ["Summary"] = WentCadProjectStore.BuildSummary(project, WentCadProjectStore.GetProjectPath(doc))
            }.ToString(Formatting.Indented);
        }
    }
}
