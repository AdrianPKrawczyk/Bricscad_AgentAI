using NUnit.Framework;
using Bricscad_AgentAI_V2.Core;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    [TestFixture]
    public class RpnCalculatorTests
    {
        [SetUp]
        public void Setup()
        {
            RpnCalculator.ClearStack();
        }

        [Test]
        public void TestBasicMath()
        {
            RpnCalculator.Evaluate("10 20 +");
            Assert.AreEqual("30", RpnCalculator.GetTopAsString());
        }

        [Test]
        public void TestStackOperations()
        {
            RpnCalculator.Evaluate("10 20 SWAP");
            var stack = RpnCalculator.GetStack();
            Assert.AreEqual("20", stack[stack.Count - 2]);
            Assert.AreEqual("10", stack[stack.Count - 1]);
        }

        [Test]
        public void TestUnits()
        {
            // 10m + 5m = 15m
            RpnCalculator.Evaluate("10_m 5_m +");
            Assert.AreEqual("15_m", RpnCalculator.GetTopAsString());
        }

        [Test]
        public void TestUnitConversion()
        {
            // 1m na mm
            RpnCalculator.Evaluate("1_m 'mm' CONVE");
            Assert.AreEqual("1000_mm", RpnCalculator.GetTopAsString());
        }

        [Test]
        public void TestVariables()
        {
            RpnCalculator.Evaluate("100 $TEST STO");
            RpnCalculator.Evaluate("$TEST RCL 50 +");
            Assert.AreEqual("150", RpnCalculator.GetTopAsString());
        }
    }
}
