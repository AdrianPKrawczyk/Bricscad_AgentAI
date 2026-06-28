using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ScanWentCadRoomsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ScanWentCadRooms",
                    Description = "Nieinteraktywnie skanuje pomieszczenia WentCad w regionie kondygnacji: zamkniete polilinie obrysow i bloki metek z atrybutami. Uzupelnia BoundaryHandle, TagHandle, numer, nazwe, powierzchnie i wysokosc.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FloorId", new ToolParameter { Type = "string", Description = "Id kondygnacji. Wymagane, jesli nie podano FloorName." } },
                            { "FloorName", new ToolParameter { Type = "string", Description = "Nazwa kondygnacji, np. Parter. Uzywana, gdy FloorId jest puste." } },
                            { "BoundaryLayer", new ToolParameter { Type = "string", Description = "Warstwa obrysow. Domyslnie DetectionMapping.BoundaryLayer." } },
                            { "TagLayer", new ToolParameter { Type = "string", Description = "Warstwa metek. Domyslnie DetectionMapping.TagLayer." } },
                            { "NumberAttribute", new ToolParameter { Type = "string", Description = "Atrybut numeru. Domyslnie DetectionMapping.NumberAttribute." } },
                            { "NameAttribute", new ToolParameter { Type = "string", Description = "Atrybut nazwy. Domyslnie DetectionMapping.NameAttribute." } },
                            { "HeightAttribute", new ToolParameter { Type = "string", Description = "Atrybut wysokosci. Domyslnie DetectionMapping.HeightAttribute." } },
                            { "AreaAttribute", new ToolParameter { Type = "string", Description = "Atrybut powierzchni. Domyslnie DetectionMapping.AreaAttribute." } },
                            { "WriteContractToDwg", new ToolParameter { Type = "boolean", Description = "Czy po skanie zapisac NOD/XData w DWG. Domyslnie true. Dla testow stabilnosci mozna ustawic false, wtedy zapisany bedzie tylko plik .wentcad." } },
                            { "RegionPoints", new ToolParameter { Type = "array", Description = "Opcjonalny region skanowania [{X,Y}, ...] albo [[x,y], ...], gdy kondygnacja nie ma Region.", Items = new JObject { ["type"] = "object" } } },
                            { "MinX", new ToolParameter { Type = "number", Description = "Opcjonalne minimalne X regionu prostokatnego." } },
                            { "MinY", new ToolParameter { Type = "number", Description = "Opcjonalne minimalne Y regionu prostokatnego." } },
                            { "MaxX", new ToolParameter { Type = "number", Description = "Opcjonalne maksymalne X regionu prostokatnego." } },
                            { "MaxY", new ToolParameter { Type = "number", Description = "Opcjonalne maksymalne Y regionu prostokatnego." } }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"FloorName\":\"Parter\"}", "{\"FloorName\":\"Parter\",\"MinX\":-3,\"MinY\":-3,\"MaxX\":63,\"MaxY\":38}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            var floors = WentCadProjectStore.ObjectMap(project, "Floors");

            string floorId = WentCadProjectStore.Clean(args["FloorId"]);
            string floorName = WentCadProjectStore.Clean(args["FloorName"]);
            JObject floor = null;
            if (!string.IsNullOrWhiteSpace(floorId))
            {
                floor = floors[floorId] as JObject;
            }
            if (floor == null && !string.IsNullOrWhiteSpace(floorName))
            {
                foreach (var prop in floors.Properties())
                {
                    var candidate = prop.Value as JObject;
                    if (string.Equals(WentCadProjectStore.Clean(candidate?["Name"]), floorName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        floor = candidate;
                        floorId = WentCadProjectStore.Clean(candidate["FloorId"]);
                        break;
                    }
                }
            }
            if (floor == null) return "Error: Nie znaleziono kondygnacji po FloorId/FloorName.";

            var scan = WentCadGeometryTools.ScanRooms(doc, project, floor, args);
            WentCadProjectStore.Recalculate(project);
            WentCadProjectStore.Save(doc, project);
            bool writeContract = args["WriteContractToDwg"] == null || args["WriteContractToDwg"].Value<bool>();
            if (writeContract)
            {
                WentCadProjectStore.SaveContractToDwg(doc, project, true);
            }

            return new JObject
            {
                ["FloorId"] = floorId,
                ["FloorName"] = floor["Name"],
                ["Boundaries"] = scan.Boundaries,
                ["Tags"] = scan.Tags,
                ["Matched"] = scan.Matched,
                ["UnmatchedBoundaries"] = scan.UnmatchedBoundaries,
                ["UnmatchedTags"] = scan.UnmatchedTags,
                ["Messages"] = new JArray(scan.Messages),
                ["Rooms"] = new JArray(scan.Rooms),
                ["ContractWriteSkipped"] = !writeContract,
                ["Summary"] = WentCadProjectStore.BuildSummary(project, WentCadProjectStore.GetProjectPath(doc))
            }.ToString(Formatting.Indented);
        }
    }
}
