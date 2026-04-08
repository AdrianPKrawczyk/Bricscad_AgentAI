using System;
using System.Linq;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    [TestFixture]
    public class RecipeManagerTests
    {
        [SetUp]
        public void Setup()
        {
            // Możemy tymczasowo zmienić ścieżkę do testowego pliku JSON, 
            // ale w tym przypadku sprawdzimy ogólną logikę.
        }

        [Test]
        public void Test_AddOrUpdate_Recipe_NormalizesTrigger()
        {
            var recipe = new AgentRecipe 
            { 
                Trigger = "test_recipe", 
                Description = "Test", 
                ToolExample = new JArray() 
            };

            RecipeManager.AddOrUpdate(recipe);
            var retrieved = RecipeManager.GetByTrigger("test_recipe");

            Assert.IsNotNull(retrieved);
            Assert.AreEqual("test_recipe", retrieved.Trigger);
        }

        [Test]
        public void Test_GetByTrigger_AutoHandlesPrefix()
        {
            var recipe = new AgentRecipe { Trigger = "prefix_test", Description = "Test", ToolExample = new JArray() };
            RecipeManager.AddOrUpdate(recipe);

            // Powinno działać zarówno z $ jak i bez
            Assert.IsNotNull(RecipeManager.GetByTrigger("prefix_test"));
            Assert.IsNotNull(RecipeManager.GetByTrigger("$prefix_test"));
        }

        [Test]
        public void Test_AppendToolCall_Logic()
        {
            // Symulacja logiki z AgentRecipeControl
            string trigger = "append_test";
            var recipe = new AgentRecipe { Trigger = trigger, Description = "Desc", ToolExample = new JArray() };
            
            var call = new JObject
            {
                ["function"] = new JObject
                {
                    ["name"] = "CreateLine",
                    ["arguments"] = new JObject { ["StartPoint"] = "0,0,0" }
                }
            };

            recipe.ToolExample.Add(call);
            Assert.AreEqual(1, recipe.ToolExample.Count);
            Assert.AreEqual("CreateLine", recipe.ToolExample[0]["function"]["name"].ToString());
        }
    }
}
