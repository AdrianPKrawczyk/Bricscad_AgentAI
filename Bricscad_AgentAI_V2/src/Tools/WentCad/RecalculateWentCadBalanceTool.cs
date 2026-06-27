using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class RecalculateWentCadBalanceTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "RecalculateWentCadBalance",
                    Description = "Przelicza bilans wentylacji WentCad wedlug trybow AUTO_MAX, MANUAL, HYGIENIC_ONLY i ACH_ONLY, zapisuje .wentcad oraz synchronizuje NOD/XData.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "WriteRoomXData", new ToolParameter { Type = "boolean", Description = "Czy zapisac XData WENTCAD_ROOM na obrysach z BoundaryHandle. Domyslnie true." } }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{}", "{\"WriteRoomXData\":true}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            WentCadProjectStore.Recalculate(project);
            WentCadProjectStore.Save(doc, project);
            bool writeXData = args["WriteRoomXData"] == null || args["WriteRoomXData"].Value<bool>();
            WentCadProjectStore.SaveContractToDwg(doc, project, writeXData);
            return new JObject
            {
                ["Summary"] = WentCadProjectStore.BuildSummary(project, WentCadProjectStore.GetProjectPath(doc)),
                ["Systems"] = project["Systems"]
            }.ToString(Formatting.Indented);
        }
    }
}
