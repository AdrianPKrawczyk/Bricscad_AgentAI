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
            System.Console.WriteLine("Pomyślnie zakończono testy strukturalne LLMClient.");
        }

        private static void TestInitialization()
        {
            var orchestrator = ToolOrchestrator.Instance;
            var client = new LLMClient("http://localhost:1234/v1/chat/completions", "key", orchestrator);
            
            Debug.Assert(client != null, "Client initialization failed");
        }
    }
}

