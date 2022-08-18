using System;
using Calamari.AzureWebApp.Util;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AzureWebApp.Tests
{
    [TestFixture]
    public class AzureWebAppHelperFixture
    {
        string site;
        string slot;

        [SetUp]
        public void Setup()
        {
            site = $"site-{Guid.NewGuid().ToString()}";
            slot = $"slot-{Guid.NewGuid().ToString()}";
        }

        [Test]
        public void LegacySiteWithSlot()
        {
            var siteAndMaybeSlotName = $"{site}({slot})";
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndMaybeSlotName, string.Empty);
            targetSite.HasSlot.Should().BeTrue();
            targetSite.Site.Should().Be(site);
            targetSite.RawSite.Should().Be(siteAndMaybeSlotName);
            targetSite.Slot.Should().Be(slot);
        }

        [Test]
        public void SiteWithSlotSlashFormat()
        {
            var siteAndMaybeSlotName = $"{site}/{slot}";
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndMaybeSlotName, string.Empty);
            targetSite.HasSlot.Should().BeTrue();
            targetSite.Site.Should().Be(site);
            targetSite.RawSite.Should().Be(siteAndMaybeSlotName);
            targetSite.Slot.Should().Be(slot);
        }

        [Test]
        public void SiteWithNoSlot()
        {
            var targetSite = AzureWebAppHelper.GetAzureTargetSite($"{site}", string.Empty);
            targetSite.HasSlot.Should().BeFalse();
            targetSite.Site.Should().Be(site);
            targetSite.RawSite.Should().Be(site);
            targetSite.Slot.Should().BeNullOrEmpty();
        }

        [Test]
        public void LegacySiteWithSlotInSiteNameAndSlotInProperty()
        {
            var overrideSlot = Guid.NewGuid().ToString();
            var siteAndMaybeSlotName = $"{site}({slot})";
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndMaybeSlotName, overrideSlot);
            targetSite.HasSlot.Should().BeTrue();
            targetSite.Site.Should().Be(site);
            targetSite.RawSite.Should().Be(siteAndMaybeSlotName);
            targetSite.Slot.Should().Be(slot);
        }

        [Test]
        public void SiteWithSlotInSiteNameAndSlotInProperty()
        {
            var overrideSlot = Guid.NewGuid().ToString();
            var siteAndMaybeSlotName = $"{site}/{slot}";
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndMaybeSlotName, overrideSlot);
            targetSite.HasSlot.Should().BeTrue();
            targetSite.Site.Should().Be(site);
            targetSite.RawSite.Should().Be(siteAndMaybeSlotName);
            targetSite.Slot.Should().Be(slot);
        }

        [Test]
        public void SiteAndSlotInProperty()
        {
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(site, slot);
            targetSite.HasSlot.Should().BeTrue();
            targetSite.Site.Should().Be(site);
            targetSite.RawSite.Should().Be(site);
            targetSite.Slot.Should().Be(slot);
            targetSite.SiteAndSlot.Should().Be($"{site}/{slot}");
        }

        [Test]
        public void PoorlyFormedLegacySite()
        {
            var targetSite = AzureWebAppHelper.GetAzureTargetSite($"{site}({slot}", string.Empty);
            targetSite.HasSlot.Should().BeTrue();
            targetSite.Site.Should().Be(site);
            targetSite.Slot.Should().Be(slot);
        }
    }
}