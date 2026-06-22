using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class SelectEntitiesToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            TestLogicConditionValidation();
            System.Console.WriteLine("Pomyślnie zakończono wszystkie testy SelectEntitiesTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new SelectEntitiesTool();
            var schema = tool.GetToolSchema();
            
            Debug.Assert(schema.Function.Name == "SelectEntities", "Niewłaściwa nazwa");
            Debug.Assert(schema.Function.Parameters.Required.Contains("EntityType"), "Brakuje wymaganego atrybutu");
        }

        private static void TestLogicConditionValidation()
        {
            // Typowe przypadki
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("10.5", "==", "10.5") == true, "Decimal Equals failed");
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("20", ">", "10") == true, "Decimal > failed");

            // Formatowanie spacji
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("(0, 0, 0)", "==", "(0,0,0)") == true, "Point Equals failed");

            // String Contains
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("WarstwaNośna", "contains", "Nośna") == true, "String Contains failed");

            // [KROK-CadTextProfile.3] Contains z nawiasami kwadratowymi (testowane z [STARy])
            // IndexOf NIE jest wildcardem - nawiasy [] traktowane sa literalnie.
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("[STARy] Notatka 1", "contains", "[STARy]") == true,
                "Contains '[STARy]' w '[STARy] Notatka 1' powinno zwrocic true (IndexOf nie jest wildcardem)");
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("Stara etykieta A", "contains", "[STARy]") == false,
                "Contains '[STARy]' w 'Stara etykieta A' powinno zwrocic false");
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("Projekt: %<\\AcVar \"DWGNAME\">", "contains", "%<\\AcVar") == true,
                "Contains z kodem pola (specjalne znaki) powinno zadzialac");

            // In array
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("Linia1", "in", "Linia,Linia1,Linia2") == true, "String In failed");

            // Wildcards
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("BlockReference", "==", "*BlockReference") == true, "Wildcard suffix match failed");
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("BlockReference", "==", "Block*") == true, "Wildcard prefix match failed");
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("BlockReference", "==", "*Reference") == true, "Wildcard suffix match failed 2");
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("BlockReference", "==", "*kRef*") == true, "Wildcard middle match failed");
            Debug.Assert(SelectEntitiesTool.ValidateLogicCondition("Polyline", "!=", "*BlockReference") == true, "Wildcard negative match failed");
        }
    }
}
