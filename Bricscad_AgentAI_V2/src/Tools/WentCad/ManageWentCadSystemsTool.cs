using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ManageWentCadSystemsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageWentCadSystems",
                    Description = "Tworzy, aktualizuje albo usuwa systemy wentylacyjne WentCad oraz przelicza sumy systemow.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Action", new ToolParameter { Type = "string", Description = "Operacja.", Enum = new List<string> { "upsert", "delete" } } },
                            { "SystemId", new ToolParameter { Type = "string", Description = "Id systemu, np. N1 albo W1." } },
                            { "Name", new ToolParameter { Type = "string", Description = "Nazwa systemu." } },
                            { "Type", new ToolParameter { Type = "string", Description = "Typ systemu.", Enum = new List<string> { "SUPPLY", "EXHAUST" } } },
                            { "ColorIndex", new ToolParameter { Type = "integer", Description = "Kolor ACI do wizualizacji." } }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"Action\":\"upsert\",\"SystemId\":\"N2\",\"Name\":\"Nawiew pietro\",\"Type\":\"SUPPLY\",\"ColorIndex\":4}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            string action = (WentCadProjectStore.Clean(args["Action"]) ?? "upsert").ToLowerInvariant();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            JObject result;

            if (action == "delete")
            {
                bool deleted = WentCadProjectStore.DeleteSystem(project, WentCadProjectStore.Clean(args["SystemId"]));
                result = new JObject { ["Deleted"] = deleted, ["SystemId"] = args["SystemId"] };
            }
            else
            {
                result = WentCadProjectStore.UpsertSystem(project, args);
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
