using System.Collections.Generic;
using System;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ManageWentCadRoomsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageWentCadRooms",
                    Description = "Tworzy, aktualizuje albo usuwa pomieszczenia WentCad. Pozwala wypelniac pola zakladek Pomieszczenia i Bilans jak uzytkownik: numer, nazwe, systemy, powierzchnie, wysokosc, tryb obliczen, osoby, dawke, ACH, nawiew/wywiew i transfery.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Action", new ToolParameter { Type = "string", Description = "Operacja.", Enum = new List<string> { "upsert", "delete" } } },
                            { "RoomId", new ToolParameter { Type = "string", Description = "Id pomieszczenia. Przy upsert moze byc puste - wtedy narzedzie szuka po FloorId+Number albo tworzy nowe." } },
                            { "FloorId", new ToolParameter { Type = "string", Description = "Id kondygnacji." } },
                            { "Number", new ToolParameter { Type = "string", Description = "Numer pomieszczenia." } },
                            { "Name", new ToolParameter { Type = "string", Description = "Nazwa pomieszczenia." } },
                            { "ActivityType", new ToolParameter { Type = "string", Description = "Typ aktywnosci uzywany do domyslnego ACH." } },
                            { "BoundaryHandle", new ToolParameter { Type = "string", Description = "Handle obrysu DWG." } },
                            { "TagHandle", new ToolParameter { Type = "string", Description = "Handle metki DWG." } },
                            { "SupplySystemId", new ToolParameter { Type = "string", Description = "System nawiewny." } },
                            { "ExhaustSystemId", new ToolParameter { Type = "string", Description = "System wywiewny." } },
                            { "Area", new ToolParameter { Type = "number", Description = "Powierzchnia pomieszczenia." } },
                            { "Height", new ToolParameter { Type = "number", Description = "Wysokosc pomieszczenia." } },
                            { "Occupants", new ToolParameter { Type = "integer", Description = "Liczba osob." } },
                            { "DosePerOccupant", new ToolParameter { Type = "number", Description = "Dawka m3/h na osobe." } },
                            { "IsTargetAchManual", new ToolParameter { Type = "boolean", Description = "Czy ACH jest recznie nadpisane." } },
                            { "ManualTargetAch", new ToolParameter { Type = "number", Description = "Reczne ACH." } },
                            { "TargetAch", new ToolParameter { Type = "number", Description = "Docelowe ACH." } },
                            { "CalculationMode", new ToolParameter { Type = "string", Description = "Tryb obliczen.", Enum = new List<string> { "AUTO_MAX", "MANUAL", "HYGIENIC_ONLY", "ACH_ONLY" } } },
                            { "ManualSupply", new ToolParameter { Type = "number", Description = "Reczny nawiew." } },
                            { "ManualExhaust", new ToolParameter { Type = "number", Description = "Reczny wywiew." } },
                            { "TransferIn", new ToolParameter { Type = "number", Description = "Transfer do pomieszczenia." } },
                            { "TransferOut", new ToolParameter { Type = "number", Description = "Transfer z pomieszczenia." } }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"Action\":\"upsert\",\"FloorId\":\"...\",\"Number\":\"0.01\",\"Name\":\"Wiatrolap\",\"Area\":12,\"Height\":3,\"CalculationMode\":\"AUTO_MAX\",\"Occupants\":2,\"SupplySystemId\":\"N1\",\"ExhaustSystemId\":\"W1\"}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            string action = (WentCadProjectStore.Clean(args["Action"]) ?? "upsert").ToLowerInvariant();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            JObject result;

            if (action == "delete")
            {
                bool deleted = WentCadProjectStore.DeleteItem(project, "Rooms", WentCadProjectStore.Clean(args["RoomId"]));
                result = new JObject { ["Deleted"] = deleted, ["RoomId"] = args["RoomId"] };
            }
            else
            {
                try
                {
                    result = WentCadProjectStore.UpsertRoom(project, args);
                }
                catch (InvalidOperationException ex)
                {
                    return "Error: " + ex.Message;
                }
            }

            WentCadProjectStore.Recalculate(project);
            WentCadProjectStore.Save(doc, project);
            WentCadProjectStore.SaveContractToDwg(doc, project, true);
            return new JObject
            {
                ["Result"] = result,
                ["Summary"] = WentCadProjectStore.BuildSummary(project, WentCadProjectStore.GetProjectPath(doc))
            }.ToString(Formatting.Indented);
        }
    }
}
