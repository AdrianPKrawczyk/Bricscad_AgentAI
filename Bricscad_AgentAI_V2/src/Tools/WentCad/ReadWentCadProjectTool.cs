using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ReadWentCadProjectTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadWentCadProject",
                    Description = "Odczytuje kontrakt WentCad zapisany w DWG przez NOD: projekt, kondygnacje i pomieszczenia. Nie wymaga zaladowania DLL WentCad.",
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

            using (doc.LockDocument())
            {
                return WentCadContractReader.Read(doc.Database).ToString(Formatting.Indented);
            }
        }
    }
}
