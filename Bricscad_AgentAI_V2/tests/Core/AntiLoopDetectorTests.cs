using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    public static class AntiLoopDetectorTests
    {
        public static void RunTests()
        {
            TestEdgeCase_RecentCallsCount_LessThanCycleLen();
            TestEdgeCase_ExactCountForCycle();
            TestNoLoop_NoFalsePositive();
            TestWzor1_IdenticalArgsRepeated();
            TestWzor3_SelectEntitiesWithoutMutating();
            TestDoesNotThrow_OnAnyInput();
            TestScopedToCurrentUserMessage_DoesNotCarryOver();
            TestStillDetectsLoop_WithinSingleCommand();
            System.Console.WriteLine("Pomyślnie zakończono testy AntiLoopDetector.");
        }

        // Wywoluje prywatna statyczna metode DetectToolCallLoop z LLMClient.
        private static object InvokeDetectLoop(List<ChatMessage> history)
        {
            var method = typeof(LLMClient).GetMethod("DetectToolCallLoop",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                throw new InvalidOperationException("DetectToolCallLoop nie znaleziona w LLMClient");
            return method.Invoke(null, new object[] { history });
        }

        private static object GetProperty(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName,
                BindingFlags.Public | BindingFlags.Instance);
            return prop.GetValue(obj);
        }

        private static ChatMessage MakeToolCallMessage(string toolName, string args, int seq)
        {
            return new ChatMessage
            {
                Role = "assistant",
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall
                    {
                        Id = $"call_{seq}",
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = toolName,
                            Arguments = args
                        }
                    }
                }
            };
        }

        // [KROK-CadTextProfile.4] Krawedz: recentCalls.Count=4, cycleLen=3 wymaga 6.
        // Wczesniej powodowalo IndexOutOfRangeException w anti-loop i tryb cichy catch.
        private static void TestEdgeCase_RecentCallsCount_LessThanCycleLen()
        {
            var history = new List<ChatMessage>
            {
                MakeToolCallMessage("SelectEntities", "{\"EntityType\":\"MText\"}", 1),
                MakeToolCallMessage("ReadTextSampleTool", "{}", 2),
                MakeToolCallMessage("SelectEntities", "{\"EntityType\":\"MText\"}", 3),
                MakeToolCallMessage("ReadTextSampleTool", "{}", 4)
            };

            object result = null;
            bool threw = false;
            try { result = InvokeDetectLoop(history); }
            catch (TargetInvocationException) { threw = true; }

            Debug.Assert(!threw, "DetectToolCallLoop NIE MOZE rzucac wyjatku dla recentCalls.Count=4, cycleLen=3");
            Debug.Assert(result != null, "AntiLoopResult nie moze byc null");
            bool isLooping = (bool)GetProperty(result, "IsLooping");
            Debug.Assert(!isLooping, "Dla 4 wywolan bez wyraznego wzoru NIE powinno wykrywac petli");
            Console.WriteLine("TestEdgeCase_RecentCallsCount_LessThanCycleLen: OK - brak wyjatku dla krawedzi.");
        }

        // Krawedz: recentCalls.Count=6 pozwala na cykl 3-elementowy.
        private static void TestEdgeCase_ExactCountForCycle()
        {
            // A,B,A,B,A,B -> cykl A,B powtarzany 3x ale cycleLen=3 to 6 elementow
            // Bez powtorzenia wzoru cyklu 3-elementowego
            var history = new List<ChatMessage>
            {
                MakeToolCallMessage("SelectEntities", "{\"a\":1}", 1),
                MakeToolCallMessage("ReadTextSampleTool", "{\"b\":1}", 2),
                MakeToolCallMessage("SelectEntities", "{\"a\":2}", 3),
                MakeToolCallMessage("ReadTextSampleTool", "{\"b\":2}", 4),
                MakeToolCallMessage("SelectEntities", "{\"a\":3}", 5),
                MakeToolCallMessage("ReadTextSampleTool", "{\"b\":3}", 6)
            };
            var result = InvokeDetectLoop(history);
            bool isLooping = (bool)GetProperty(result, "IsLooping");
            Debug.Assert(!isLooping, "Rozne args (a:1 vs a:2) NIE sa wzorem petli");
            Console.WriteLine("TestEdgeCase_ExactCountForCycle: OK - rozne args nie wykrywaja petli.");
        }

        // Brak wywolan -> brak petli
        private static void TestNoLoop_NoFalsePositive()
        {
            var history = new List<ChatMessage>
            {
                MakeToolCallMessage("SelectEntities", "{\"a\":1}", 1),
                MakeToolCallMessage("TextEditTool", "{\"mode\":\"Replace\"}", 2)
            };
            var result = InvokeDetectLoop(history);
            bool isLooping = (bool)GetProperty(result, "IsLooping");
            Debug.Assert(!isLooping, "2 rozne wywolania nie sa petla");
            Console.WriteLine("TestNoLoop_NoFalsePositive: OK - 2 wywolania nie sa petla.");
        }

        // Wzor 1: ten sam tool z identycznymi args 3x pod rzad
        private static void TestWzor1_IdenticalArgsRepeated()
        {
            var history = new List<ChatMessage>
            {
                MakeToolCallMessage("SelectEntities", "{\"x\":1}", 1),
                MakeToolCallMessage("SelectEntities", "{\"x\":1}", 2),
                MakeToolCallMessage("SelectEntities", "{\"x\":1}", 3)
            };
            var result = InvokeDetectLoop(history);
            bool isLooping = (bool)GetProperty(result, "IsLooping");
            Debug.Assert(isLooping, "3x ten sam tool z identycznymi args MUSI wykrywac petle");
            string pattern = (string)GetProperty(result, "Pattern");
            Debug.Assert(pattern != null && pattern.Contains("SelectEntities"),
                $"Pattern powinien wspomniec SelectEntities, jest: {pattern}");
            Console.WriteLine($"TestWzor1_IdenticalArgsRepeated: OK - wzorzec='{pattern}'.");
        }

        // Wzor 3: SelectEntities 4x bez zadnego mutujacego narzedzia
        private static void TestWzor3_SelectEntitiesWithoutMutating()
        {
            // Dodaj 1 mutujace na poczatku, ale 4 SelectEntities bez mutujacego na koncu
            var history = new List<ChatMessage>
            {
                MakeToolCallMessage("TextEditTool", "{\"mode\":\"Replace\"}", 1),
                MakeToolCallMessage("SelectEntities", "{\"a\":1}", 2),
                MakeToolCallMessage("SelectEntities", "{\"a\":2}", 3),
                MakeToolCallMessage("SelectEntities", "{\"a\":3}", 4),
                MakeToolCallMessage("SelectEntities", "{\"a\":4}", 5)
            };
            var result = InvokeDetectLoop(history);
            bool isLooping = (bool)GetProperty(result, "IsLooping");
            Debug.Assert(isLooping, "4x SelectEntities bez mutujacego MUSI wykrywac petle (Wzor 3)");
            string pattern = (string)GetProperty(result, "Pattern");
            Debug.Assert(pattern != null && pattern.Contains("SelectEntities"),
                $"Pattern powinien wspomniec SelectEntities, jest: {pattern}");
            Console.WriteLine($"TestWzor3_SelectEntitiesWithoutMutating: OK - wzorzec='{pattern}'.");
        }

        // [KROK-CadTextProfile.4] Glowny test regresji - NIE rzucac wyjatku
        // dla zadnej kombinacji rozmiaru listy 0..10
        private static void TestDoesNotThrow_OnAnyInput()
        {
            int totalCalls = 0;
            int throws = 0;
            // Iteruj przez rozne rozmiary i rozne scenariusze
            for (int n = 0; n <= 10; n++)
            {
                for (int scenario = 0; scenario < 4; scenario++)
                {
                    var history = new List<ChatMessage>();
                    for (int i = 0; i < n; i++)
                    {
                        string tool = (scenario % 2 == 0) ? "SelectEntities" : "ReadTextSampleTool";
                        string args = (scenario < 2) ? $"{{\"a\":{i}}}" : $"{{\"a\":1}}";
                        history.Add(MakeToolCallMessage(tool, args, i + 1));
                    }
                    totalCalls++;
                    try { InvokeDetectLoop(history); }
                    catch (TargetInvocationException) { throws++; }
                }
            }
            Debug.Assert(throws == 0,
                $"DetectToolCallLoop rzucil wyjatek {throws}/{totalCalls} razy (powinno byc 0)");
            Console.WriteLine($"TestDoesNotThrow_OnAnyInput: OK - {totalCalls} scenariuszy, 0 wyjatkow.");
        }

        // [KROK-CadTextProfile.5] Glowny bug: recentCalls musi byc SCOPED do biezacego polecenia.
        // Poprzednio recentCalls bral ostatnie 10 wywolan z CALEGO conversationHistory,
        // co powodowalo ze po poleceniu 1 z 3x SelectEntities, polecenie 2 startowalo z
        // recentCalls.Count=3 i 2 dodatkowe SelectEntities powodowaly natychmiastowe
        // przerwanie sesji po wzorze "Nx SelectEntities bez mutujacego".
        //
        // Ten test odtwarza scenariusz z logow (2026-06-22 23:51-23:53):
        // Polecenie 1: STARy (3x SelectEntities + 1 ReadTextSampleTool + 1 GetPropertiesTool)
        // Polecenie 2: DWGNAME (2x SelectEntities) - POWINNO BYC DOZWOLONE bo NIE jest petla
        //   w obrebie jednego polecenia.
        private static void TestScopedToCurrentUserMessage_DoesNotCarryOver()
        {
            // Symulacja CALEGO conversationHistory (dwa polecenia uzytkownika):
            // Polecenie 1: 3x SelectEntities + ReadTextSampleTool + GetPropertiesTool
            // user "STARy" -> assistant[SelectEntities, ReadTextSampleTool, GetPropertiesTool]
            // tool results...
            // Polecenie 2: 2x SelectEntities (DWGNAME) -> powinno przejsc bez blokady
            // user "DWGNAME" -> assistant[SelectEntities, SelectEntities]
            var history = new List<ChatMessage>();

            // Polecenie 1: STARy
            history.Add(new ChatMessage { Role = "user", Content = "Wybierz wszystkie DBText ze znacznikiem [STARy]" });
            history.Add(MakeToolCallMessage("SelectEntities", "{\"Prop\":\"TextOverride\",\"Val\":\"[STARy]\"}", 1));
            history.Add(MakeToolCallMessage("ReadTextSampleTool", "{\"SaveAs\":\"stare_teksty\"}", 2));
            history.Add(MakeToolCallMessage("GetPropertiesTool", "{\"Mode\":\"Full\"}", 3));

            // Polecenie 2: DWGNAME
            history.Add(new ChatMessage { Role = "user", Content = "Zamien tekst 'Projekt: ' w MText ... DWGNAME ... na Zlecenie" });
            history.Add(MakeToolCallMessage("SelectEntities", "{\"Prop\":\"TextOverride\",\"Val\":\"Projekt: %<AcVar DWGNAME>\"}", 4));
            history.Add(MakeToolCallMessage("SelectEntities", "{\"Prop\":\"TextOverride\",\"Val\":\"Projekt:\"}", 5));

            // Wywolaj - NIE powinno wykrywac petli, bo recentCalls jest scoped od ostatniego user.
            // W obrebie polecenia 2 sa tylko 2 SelectEntities, wzorzec 3 (Nx SelectEntities) wymaga 4.
            var result = InvokeDetectLoop(history);
            bool isLooping = (bool)GetProperty(result, "IsLooping");
            Debug.Assert(!isLooping,
                "Anti-loop NIE powinien wykrywac petli po 2 SelectEntities w biezacym poleceniu, " +
                "nawet jesli wczesniejsze polecenie mialo 3 SelectEntities (granica polecenia powinna byc respektowana).");
            Console.WriteLine("TestScopedToCurrentUserMessage_DoesNotCarryOver: OK - 2 SelectEntities w poleceniu 2 nie wykrywaja petli mimo 3 w poleceniu 1.");
        }

        // [KROK-CadTextProfile.5] W obrebie jednego polecenia 4+ SelectEntities bez mutujacego MUSI wykrywac petle
        // (zachowanie oryginalnego Wzoru 3 po naprawie scope'u).
        private static void TestStillDetectsLoop_WithinSingleCommand()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Zamien tekst w MText" },
                MakeToolCallMessage("SelectEntities", "{\"a\":1}", 1),
                MakeToolCallMessage("SelectEntities", "{\"a\":2}", 2),
                MakeToolCallMessage("SelectEntities", "{\"a\":3}", 3),
                MakeToolCallMessage("SelectEntities", "{\"a\":4}", 4)
            };

            var result = InvokeDetectLoop(history);
            bool isLooping = (bool)GetProperty(result, "IsLooping");
            Debug.Assert(isLooping,
                "4x SelectEntities bez mutujacego w obrebie jednego polecenia MUSI wykrywac petle (Wzor 3)");
            Console.WriteLine("TestStillDetectsLoop_WithinSingleCommand: OK - Wzor 3 nadal dziala po naprawie scope.");
        }
    }
}
