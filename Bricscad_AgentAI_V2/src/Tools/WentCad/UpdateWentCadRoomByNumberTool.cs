using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class UpdateWentCadRoomByNumberTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "UpdateWentCadRoomByNumber",
                    Description = "Bezpiecznie aktualizuje istniejace pomieszczenie WentCad po FloorId/FloorName + Number. Nie tworzy nowych rekordow, dzieki czemu nie powstaja pomieszczenia-sieroty bez kondygnacji.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FloorId", new ToolParameter { Type = "string", Description = "Id kondygnacji. Wymagane, jesli nie podano FloorName." } },
                            { "FloorName", new ToolParameter { Type = "string", Description = "Nazwa kondygnacji, np. Parter albo Pietro_1." } },
                            { "Number", new ToolParameter { Type = "string", Description = "Numer istniejacego pomieszczenia." } },
                            { "Name", new ToolParameter { Type = "string", Description = "Nazwa pomieszczenia." } },
                            { "ActivityType", new ToolParameter { Type = "string", Description = "Typ aktywnosci uzywany do domyslnego ACH." } },
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
                        Required = new List<string> { "Number" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string>
        {
            "{\"FloorName\":\"Parter\",\"Number\":\"0.03\",\"CalculationMode\":\"HYGIENIC_ONLY\",\"Occupants\":4,\"DosePerOccupant\":30}",
            "{\"FloorName\":\"Pietro_1\",\"Number\":\"1.05\",\"SupplySystemId\":\"N2\",\"ExhaustSystemId\":\"W2\",\"ActivityType\":\"Pomieszczenie socjalne\"}"
        };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            var project = WentCadProjectStore.LoadOrCreate(doc);

            JObject result;
            try
            {
                result = WentCadProjectStore.UpdateExistingRoomByNumber(project, args);
            }
            catch (InvalidOperationException ex)
            {
                return "Error: " + ex.Message;
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
