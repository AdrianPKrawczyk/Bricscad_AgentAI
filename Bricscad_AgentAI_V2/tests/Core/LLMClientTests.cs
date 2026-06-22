using System.Collections.Generic;
using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    public static class LLMClientTests
    {
        public static void RunTests()
        {
            TestInitialization();
            TestLlamaCppToolBreakerEscapesPercentLessThan();
            TestLlamaCppToolBreakerPreservesNonMatchingText();
            TestLlamaCppToolBreakerHandlesNullAndEmpty();
            TestLlamaCppToolBreakerClonesMessages();
            TestLlamaCppToolBreakerHandlesVisionContent();
            System.Console.WriteLine("Pomyslnie zakonczono testy strukturalne LLMClient.");
        }

        private static void TestInitialization()
        {
            var orchestrator = ToolOrchestrator.Instance;
            var client = new LLMClient(orchestrator);

            Debug.Assert(client != null, "Client initialization failed");
        }

        private static void TestLlamaCppToolBreakerEscapesPercentLessThan()
        {
            string input = "Projekt: %<\\AcVar \"DWGNAME\">";
            string output = LLMClient.EscapePercentLessThan(input);

            Debug.Assert(output.Contains("\\u0025\\u003C"),
                "Escape '%<' powinien wyprodukowac '\\u0025\\u003C' w JSON. Wynik: " + output);
            Debug.Assert(!output.Contains("%<"),
                "Po escape nie powinno zostac zadnego '%<'. Wynik: " + output);
            System.Console.WriteLine("TestLlamaCppToolBreakerEscapesPercentLessThan: OK");
        }

        private static void TestLlamaCppToolBreakerPreservesNonMatchingText()
        {
            string input = "Zwykly tekst bez field codes, ale z %% i samym <.";
            string output = LLMClient.EscapePercentLessThan(input);

            Debug.Assert(output == input,
                "Tekst bez wzorca '%<' powinien zostac nietkniety. Wejscie='" + input + "', Wyjscie='" + output + "'");
            System.Console.WriteLine("TestLlamaCppToolBreakerPreservesNonMatchingText: OK");
        }

        private static void TestLlamaCppToolBreakerHandlesNullAndEmpty()
        {
            Debug.Assert(LLMClient.EscapePercentLessThan(null) == null,
                "null powinno zwrocic null");
            Debug.Assert(LLMClient.EscapePercentLessThan("") == "",
                "pusty string powinien zostac pusty");
            System.Console.WriteLine("TestLlamaCppToolBreakerHandlesNullAndEmpty: OK");
        }

        private static void TestLlamaCppToolBreakerClonesMessages()
        {
            var originalContent = "Tekst z %<\\AcVar \"X\"> w srodku";
            var history = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = originalContent },
                new ChatMessage { Role = "user", Content = "pytanie" }
            };

            var sanitized = LLMClient.SanitizeMessagesForLlamaCppToolBreaker(history);

            // Oryginalna historia NIE powinna zostac zmodyfikowana.
            Debug.Assert(((string)history[0].Content) == originalContent,
                "Oryginalna historia nie moze byc zmodyfikowana in-place. Oryginal='" + originalContent + "'");
            Debug.Assert(sanitized != history,
                "Sanitize powinien zwrocic nowa liste, nie referencje");

            // Klon powinien miec escape.
            string sanitized0 = (string)sanitized[0].Content;
            Debug.Assert(sanitized0.Contains("\\u0025\\u003C"),
                "Klon system message powinien miec escape '%<'. Wynik='" + sanitized0 + "'");

            // Drugi message bez '%<' powinien byc nietkniety.
            string sanitized1 = (string)sanitized[1].Content;
            Debug.Assert(sanitized1 == "pytanie",
                "User message bez '%<' powinien zostac nietkniety. Wynik='" + sanitized1 + "'");
            System.Console.WriteLine("TestLlamaCppToolBreakerClonesMessages: OK");
        }

        private static void TestLlamaCppToolBreakerHandlesVisionContent()
        {
            var parts = new List<VisionContentPart>
            {
                new VisionContentPart { Type = "text", Text = "Opis z %<\\AcExpr 2+2> w srodku" },
                new VisionContentPart { Type = "image_url", ImageUrl = new VisionImageUrl { Url = "data:image/png;base64,AAAA" } }
            };
            var msg = new ChatMessage { Role = "user", Content = parts };

            var sanitized = LLMClient.SanitizeMessagesForLlamaCppToolBreaker(new List<ChatMessage> { msg });
            var sanitizedParts = (List<VisionContentPart>)sanitized[0].Content;

            Debug.Assert(sanitizedParts[0].Text.Contains("\\u0025\\u003C"),
                "Text w VisionContentPart powinien miec escape '%<'. Wynik='" + sanitizedParts[0].Text + "'");
            Debug.Assert(sanitizedParts[1].ImageUrl.Url == "data:image/png;base64,AAAA",
                "Image url powinien byc nietkniety. Wynik='" + sanitizedParts[1].ImageUrl.Url + "'");
            System.Console.WriteLine("TestLlamaCppToolBreakerHandlesVisionContent: OK");
        }
    }
}
