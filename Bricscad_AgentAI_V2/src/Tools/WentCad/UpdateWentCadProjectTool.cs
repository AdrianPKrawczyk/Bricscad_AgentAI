using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class UpdateWentCadProjectTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "UpdateWentCadProject",
                    Description = "Aktualizuje ustawienia projektu WentCad w pliku .wentcad i synchronizuje kontrakt NOD w DWG. Nie wymaga referencji do DLL WentCad.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "ProjectName", new ToolParameter { Type = "string", Description = "Opcjonalna nazwa projektu." } },
                            { "DetectionMapping", new ToolParameter { Type = "object", Description = "Opcjonalne mapowanie skanowania: BoundaryLayer, TagLayer, NumberAttribute, NameAttribute, HeightAttribute, AreaAttribute." } },
                            { "SyncToDwg", new ToolParameter { Type = "boolean", Description = "Czy po zapisie synchronizowac NOD/XData. Domyslnie true." } }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"ProjectName\":\"Wentylacja SP107\",\"DetectionMapping\":{\"BoundaryLayer\":\"WC_TEST_OBRYSY\",\"TagLayer\":\"WC_TEST_METKI\"}}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();

            var project = WentCadProjectStore.LoadOrCreate(doc);
            string name = WentCadProjectStore.Clean(args["ProjectName"]);
            if (!string.IsNullOrWhiteSpace(name)) project["ProjectName"] = name;
            if (args["DetectionMapping"] is JObject mapping)
            {
                project["DetectionMapping"] = mapping.DeepClone();
            }

            WentCadProjectStore.Save(doc, project);
            if (args["SyncToDwg"] == null || args["SyncToDwg"].Value<bool>())
            {
                WentCadProjectStore.SaveContractToDwg(doc, project, true);
            }

            return WentCadProjectStore.BuildSummary(project, WentCadProjectStore.GetProjectPath(doc)).ToString(Formatting.Indented);
        }
    }
}
