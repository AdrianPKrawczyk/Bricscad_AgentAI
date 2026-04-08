using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class InspectEntityTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "InspectEntity",
                    Description = "Pobiera szczegółowe właściwości geonetryczne i atrybuty wskazanego obiektu (lub pierwszego z aktualnego zaznaczenia).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "EntityHandle", new ToolParameter { Type = "string", Description = "Opcjonalnie: Handle obiektu do inspekcji. Jeśli brak, użyty zostanie pierwszy element z ActiveSelection." } }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            Database db = doc.Database;
            ObjectId id = ObjectId.Null;

            string handleStr = args["EntityHandle"]?.ToString();
            if (!string.IsNullOrEmpty(handleStr))
            {
                try { id = db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); } catch { }
            }

            if (id.IsNull)
            {
                var selection = AgentMemoryState.ActiveSelection;
                if (selection != null && selection.Length > 0)
                {
                    id = selection[0];
                }
            }

            if (id.IsNull) return "BŁĄD: Nie wybrano obiektu do inspekcji (ActiveSelection jest puste).";

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) return "BŁĄD: Obiekt nie jest encją.";

                    var props = new JObject();
                    props["Handle"] = ent.Handle.ToString();
                    props["Type"] = ent.GetType().Name;
                    props["Layer"] = ent.Layer;
                    props["Color"] = ent.Color.ToString();
                    props["Linetype"] = ent.Linetype;

                    // Specyficzne dla typu
                    if (ent is Line line)
                    {
                        props["StartPoint"] = line.StartPoint.ToString();
                        props["EndPoint"] = line.EndPoint.ToString();
                        props["Length"] = line.Length;
                    }
                    else if (ent is Circle circle)
                    {
                        props["Center"] = circle.Center.ToString();
                        props["Radius"] = circle.Radius;
                        props["Area"] = circle.Area;
                    }
                    else if (ent is DBText txt)
                    {
                        props["Text"] = txt.TextString;
                        props["Position"] = txt.Position.ToString();
                        props["Height"] = txt.Height;
                    }

                    return props.ToString(Newtonsoft.Json.Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD INSPEKCJI: {ex.Message}";
            }
        }
        public List<string> Examples => null;
    }
}

