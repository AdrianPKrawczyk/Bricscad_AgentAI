using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Tools;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class CadTextProfileTests
    {
        private const string PromptFileRelative = @"prompts\system_prompt_text.txt";
        private static readonly string[] ExpectedMutatingTools = new[]
        {
            "TextEditTool", "ManageFields", "ModifyProperties", "Foreach", "manage_lisps"
        };

        private static readonly string[] ExpectedReadTools = new[]
        {
            "ReadTextSampleTool", "ReadFields", "ReadXData", "FindXData",
            "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool"
        };

        private static readonly string[] ExpectedInfraTools = new[]
        {
            "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard",
            "RequestAdditionalTools", "UserInput", "UserChoice"
        };

        private static readonly string[] ForbiddenTools = new[]
        {
            "CreateObject", "ManageLayers",
            "DimensionEditTool", "CreateBlock", "InsertBlock", "EditBlock",
            "EditAttributes", "ListBlocks", "WriteXData", "BatchWriteXData"
        };

        public static void RunTests()
        {
            TestPromptFileExists();
            TestDefaultSystemPromptFileMapping();
            TestProfileRegisteredInConfig();
            TestAllowedToolsContainExpected();
            TestAllowedToolsExcludeGeometryAndBlockTools();
            TestAllowedTagsContainTextAndFields();
            TestToolTaggingForText();
            TestToolTaggingForFields();
            TestToolTaggingForXDataContext();
            TestPromptContainsKeyRules();
            TestTextEditToolSchema();
            TestManageFieldsToolSchema();
            TestReadTextSampleToolSchema();
            TestIsToolActiveWithTextTag();
            TestIsToolActiveRejectsLeakedTools();
            Console.WriteLine("Pomyślnie zakończono testy CadTextProfile.");
        }

        private static void TestPromptFileExists()
        {
            string path = ToolConfigManager.GetRuntimePromptPath(PromptFileRelative);
            Debug.Assert(File.Exists(path), $"Plik promptu nie istnieje: {path}");
            Console.WriteLine("TestPromptFileExists: OK - plik promptu CadTextProfile znaleziony.");
        }

        private static void TestDefaultSystemPromptFileMapping()
        {
            string mapped = ToolConfigManager.GetDefaultSystemPromptFile("CadTextProfile");
            Debug.Assert(mapped == PromptFileRelative,
                $"Mapping powinien wskazywac na '{PromptFileRelative}', a wskazuje na '{mapped}'");
            Console.WriteLine("TestDefaultSystemPromptFileMapping: OK - CadTextProfile mapuje na wlasciwy plik.");
        }

        private static void TestProfileRegisteredInConfig()
        {
            var profiles = ToolConfigManager.GetProfiles();
            Debug.Assert(profiles.ContainsKey("CadTextProfile"),
                "Profil CadTextProfile nie jest zarejestrowany w konfiguracji.");

            var profile = profiles["CadTextProfile"];
            Debug.Assert(profile.SystemPromptFile == PromptFileRelative,
                $"SystemPromptFile powinno byc '{PromptFileRelative}', a jest '{profile.SystemPromptFile}'");
            Console.WriteLine("TestProfileRegisteredInConfig: OK - profil zarejestrowany z poprawnym plikiem.");
        }

        private static void TestAllowedToolsContainExpected()
        {
            var profile = ToolConfigManager.GetProfiles()["CadTextProfile"];

            foreach (string tool in ExpectedMutatingTools.Concat(ExpectedReadTools).Concat(ExpectedInfraTools))
            {
                Debug.Assert(profile.AllowedTools.Contains(tool, StringComparer.OrdinalIgnoreCase),
                    $"Brak oczekiwanego narzedzia w AllowedTools CadTextProfile: '{tool}'");
            }
            Console.WriteLine("TestAllowedToolsContainExpected: OK - wszystkie wymagane narzedzia sa w AllowedTools.");
        }

        private static void TestAllowedToolsExcludeGeometryAndBlockTools()
        {
            var profile = ToolConfigManager.GetProfiles()["CadTextProfile"];

            foreach (string tool in ForbiddenTools)
            {
                Debug.Assert(!profile.AllowedTools.Contains(tool, StringComparer.OrdinalIgnoreCase),
                    $"Narzadzie '{tool}' NIE powinno byc w AllowedTools CadTextProfile (granica kompetencji).");
            }
            Console.WriteLine("TestAllowedToolsExcludeGeometryAndBlockTools: OK - brak wycieku narzedzi z innych profili.");
        }

        private static void TestAllowedTagsContainTextAndFields()
        {
            var profile = ToolConfigManager.GetProfiles()["CadTextProfile"];

            Debug.Assert(profile.AllowedTags.Contains("#text", StringComparer.OrdinalIgnoreCase),
                "AllowedTags powinno zawierac '#text'");
            Debug.Assert(profile.AllowedTags.Contains("#fields", StringComparer.OrdinalIgnoreCase),
                "AllowedTags powinno zawierac '#fields' dla dostepu do ManageFieldsTool/ReadFieldsTool");
            Console.WriteLine("TestAllowedTagsContainTextAndFields: OK - tagi #text i #fields obecne.");
        }

        private static void TestToolTaggingForText()
        {
            var settings = ToolConfigManager.GetAllSettings();
            Debug.Assert(settings.ContainsKey("TextEditTool"), "Brak TextEditTool w rejestrze narzedzi.");
            Debug.Assert(settings.ContainsKey("ReadTextSampleTool"), "Brak ReadTextSampleTool w rejestrze.");

            string textEditTags = settings["TextEditTool"].Tags ?? "";
            string sampleTags = settings["ReadTextSampleTool"].Tags ?? "";

            Debug.Assert(textEditTags.IndexOf("#text", StringComparison.OrdinalIgnoreCase) >= 0,
                $"TextEditTool powinien miec tag #text. Aktualne tagi: '{textEditTags}'");
            Debug.Assert(sampleTags.IndexOf("#text", StringComparison.OrdinalIgnoreCase) >= 0,
                $"ReadTextSampleTool powinien miec tag #text. Aktualne tagi: '{sampleTags}'");
            Console.WriteLine("TestToolTaggingForText: OK - TextEditTool i ReadTextSampleTool maja tag #text.");
        }

        private static void TestToolTaggingForFields()
        {
            var settings = ToolConfigManager.GetAllSettings();
            Debug.Assert(settings.ContainsKey("ManageFields"), "Brak ManageFields w rejestrze.");
            Debug.Assert(settings.ContainsKey("ReadFields"), "Brak ReadFields w rejestrze.");

            string mfTags = settings["ManageFields"].Tags ?? "";
            string rfTags = settings["ReadFields"].Tags ?? "";

            Debug.Assert(mfTags.IndexOf("#fields", StringComparison.OrdinalIgnoreCase) >= 0,
                $"ManageFields powinien miec tag #fields. Aktualne tagi: '{mfTags}'");
            Debug.Assert(rfTags.IndexOf("#fields", StringComparison.OrdinalIgnoreCase) >= 0,
                $"ReadFields powinien miec tag #fields. Aktualne tagi: '{rfTags}'");
            Console.WriteLine("TestToolTaggingForFields: OK - ManageFields i ReadFields maja tag #fields.");
        }

        private static void TestToolTaggingForXDataContext()
        {
            var settings = ToolConfigManager.GetAllSettings();
            Debug.Assert(settings.ContainsKey("ReadXData"), "Brak ReadXData w rejestrze.");
            Debug.Assert(settings.ContainsKey("FindXData"), "Brak FindXData w rejestrze.");

            string rxTags = settings["ReadXData"].Tags ?? "";
            string fxTags = settings["FindXData"].Tags ?? "";

            Debug.Assert(rxTags.IndexOf("#text", StringComparison.OrdinalIgnoreCase) >= 0,
                $"ReadXData powinien miec tag #text (kontekst). Aktualne tagi: '{rxTags}'");
            Debug.Assert(fxTags.IndexOf("#text", StringComparison.OrdinalIgnoreCase) >= 0,
                $"FindXData powinien miec tag #text (kontekst). Aktualne tagi: '{fxTags}'");
            Console.WriteLine("TestToolTaggingForXDataContext: OK - ReadXData/FindXData maja tag #text.");
        }

        private static void TestPromptContainsKeyRules()
        {
            string prompt = ToolConfigManager.LoadSystemPromptText(PromptFileRelative);

            string[] requiredPhrases = new[]
            {
                "CadTextProfile",
                "DBText",
                "MText",
                "ReadTextSampleTool",
                "ReplaceWith",
                "ManageFieldsTool",
                "FieldCode",
                "AllowFieldOverride",
                "ForeachTool",
                "IterateSelection=true",
                "manage_lisps",
                "replace_text_in_layers",
                "ActiveSelection",
                "FormatHighlight",
                "ClearFormatting",
                "\\fArial|b1;",
                "\\C1;",
                "\\P",
                "BinaryField",
                "%<\\_FldIdx",
                "EvaluateAll",
                "PULAPKI",
                "wylistuj",
                "GetPropertiesTool",
                "BackgroundFill",
                "ShowBorders",
                "UseBackgroundColor"
            };

            foreach (string phrase in requiredPhrases)
            {
                Debug.Assert(prompt.Contains(phrase),
                    $"Prompt system_prompt_text.txt powinien zawierac fraze: '{phrase}'");
            }
            Console.WriteLine($"TestPromptContainsKeyRules: OK - prompt zawiera wszystkie {requiredPhrases.Length} kluczowych fraz.");
        }

        private static void TestTextEditToolSchema()
        {
            var tool = new TextEditTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema.Function.Name == "TextEditTool", "Niewlasciwa nazwa TextEditTool");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Mode"), "Brak parametru Mode");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("FindText"), "Brak parametru FindText");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ReplaceWith"), "Brak parametru ReplaceWith");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("AllowFieldOverride"), "Brak parametru AllowFieldOverride");

            string modeDesc = schema.Function.Parameters.Properties["Mode"].Description ?? "";
            string[] expectedModes = { "Append", "Prepend", "Replace", "FormatHighlight", "ClearFormatting" };
            foreach (string m in expectedModes)
            {
                Debug.Assert(modeDesc.Contains(m),
                    $"Opis Mode powinien wymieniac tryb '{m}'");
            }
            Console.WriteLine("TestTextEditToolSchema: OK - TextEditTool ma wszystkie tryby i parametry.");
        }

        private static void TestManageFieldsToolSchema()
        {
            var tool = new ManageFieldsTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema.Function.Name == "ManageFields", "Niewlasciwa nazwa ManageFields");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Action"), "Brak parametru Action");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("FieldCode"), "Brak parametru FieldCode");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Targets"), "Brak parametru Targets");
            Debug.Assert(schema.Function.Parameters.Required.Contains("Action"), "Action powinno byc wymagane");
            Console.WriteLine("TestManageFieldsToolSchema: OK - ManageFields ma Action, FieldCode, Targets.");
        }

        private static void TestReadTextSampleToolSchema()
        {
            var tool = new ReadTextSampleTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema.Function.Name == "ReadTextSampleTool", "Niewlasciwa nazwa ReadTextSampleTool");
            Debug.Assert(schema.Function.Description.Contains("DBText") || schema.Function.Description.Contains("MText"),
                "Opis ReadTextSampleTool powinien wspomniec o DBText/MText");
            Console.WriteLine("TestReadTextSampleToolSchema: OK - ReadTextSampleTool opisuje DBText/MText.");
        }

        private static void TestIsToolActiveWithTextTag()
        {
            Debug.Assert(ToolConfigManager.IsToolActive("TextEditTool", new[] { "#text" }),
                "IsToolActive powinien zwrocic true dla TextEditTool z tagiem #text");
            Debug.Assert(ToolConfigManager.IsToolActive("ReadTextSampleTool", new[] { "#text" }),
                "IsToolActive powinien zwrocic true dla ReadTextSampleTool z tagiem #text");
            Debug.Assert(ToolConfigManager.IsToolActive("ManageFields", new[] { "#text", "#fields" }),
                "IsToolActive powinien zwrocic true dla ManageFields z tagami #text/#fields");
            Console.WriteLine("TestIsToolActiveWithTextTag: OK - tag #text aktywuje narzedzia tekstowe.");
        }

        private static void TestIsToolActiveRejectsLeakedTools()
        {
            Debug.Assert(!ToolConfigManager.IsToolActive("CreateObject", new[] { "#text" }),
                "CreateObject NIE powinien byc aktywny dla profilu #text (granica kompetencji)");
            Debug.Assert(!ToolConfigManager.IsToolActive("EditBlock", new[] { "#text" }),
                "EditBlock NIE powinien byc aktywny dla profilu #text (to domena blokow)");
            Debug.Assert(!ToolConfigManager.IsToolActive("BatchWriteXData", new[] { "#text" }),
                "BatchWriteXData NIE powinien byc aktywny dla profilu #text (zapis XData = MetadataProfile)");
            Console.WriteLine("TestIsToolActiveRejectsLeakedTools: OK - profil tekstowy nie widzi narzedzi innych domen.");
        }
    }
}
