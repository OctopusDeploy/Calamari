using Calamari.AzureAppService.Azure;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests
{
    public class AzureResourceFixture
    {
        private const string WebAppSlotsType = "microsoft.web/sites/slots";
        private const string WebAppType = "microsoft.web/sites";

        [Test]
        [TestCase(WebAppSlotsType, true)]
        [TestCase(WebAppType, false)]
        [TestCase("NotAValidType", false)]
        public void IsSlot_ReturnsCorrectValue_WhenTypeIsSetToDifferentValues(string type, bool expectedIsSlotValue)
        {
            var resource = new AzureResource
            {
                Type = type
            };

            resource.IsSlot.Should().Be(expectedIsSlotValue);
        }

        [Test]
        [TestCase("SomeParentName/MySlotName", true, "MySlotName")]
        [TestCase("SomeCrazy/ParentName", false, null)]
        [TestCase("SomeCrazyParentNameWithNoSlot", true, "")]
        [TestCase("SomeCrazyParentNameWithNoSlot", false, null)]
        public void SlotName_ShouldReturnCorrectName_IfResourceIsSlot(string name, bool isSlot, string expectedSlotName)
        {
            var resource = new AzureResource
            {
                Type = isSlot ? WebAppSlotsType : WebAppType,
                Name = name
            };

            resource.SlotName.Should().Be(expectedSlotName);
        }

        [Test]
        [TestCase("SomeParentName/MySlotName", true, "SomeParentName")]
        [TestCase("SomeCrazy/ParentName", false, null)]
        [TestCase("SomeCrazyParentNameWithNoSlot", true, "SomeCrazyParentNameWithNoSlot")]
        [TestCase("SomeCrazyParentNameWithNoSlot", false, null)]
        public void ParentName_ShouldReturnCorrectName_IfResourceIsSlot(string name, bool isSlot,
            string expectedParentName)
        {
            var resource = new AzureResource
            {
                Type = isSlot ? WebAppSlotsType : WebAppType,
                Name = name
            };

            resource.ParentName.Should().Be(expectedParentName);
        }
    }
}