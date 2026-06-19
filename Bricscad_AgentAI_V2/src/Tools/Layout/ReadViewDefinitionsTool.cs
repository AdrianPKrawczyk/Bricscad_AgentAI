using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class ReadViewDefinitionsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "read_view_definitions",
                    Description = "Odczytuje ustrukturyzowane definicje zakresów i widoków (np. Rzuty, Przekroje) z menedżera Druk Widoki. Użyj tego, aby uzyskać instrukcje i granice generowania arkuszy.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>()
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";

            var viewsArray = new JArray();

            using (var doclock = doc.LockDocument())
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForRead);
                    if (nod.Contains("BIELIK_DRUK_WIDOKI"))
                    {
                        var dict = (DBDictionary)tr.GetObject(nod.GetAt("BIELIK_DRUK_WIDOKI"), OpenMode.ForRead);
                        foreach (var entry in dict)
                        {
                            var xrec = tr.GetObject(entry.Value, OpenMode.ForRead) as Xrecord;
                            if (xrec != null)
                            {
                                foreach (TypedValue tv in xrec.Data)
                                {
                                    if (tv.TypeCode == (short)DxfCode.Text)
                                    {
                                        try
                                        {
                                            var jToken = JToken.Parse((string)tv.Value);
                                            viewsArray.Add(jToken);
                                        }
                                        catch { }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
            }

            var resultObj = new JObject();
            resultObj["Views"] = viewsArray;
            return resultObj.ToString(Formatting.Indented);
        }
    }
}
