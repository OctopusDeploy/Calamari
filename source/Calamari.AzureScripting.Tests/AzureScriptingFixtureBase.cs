using System;
using Calamari.Testing;
using NUnit.Framework;

namespace Calamari.AzureScripting.Tests
{
    abstract class AzureScriptingFixtureBase
    {
        protected string? ClientId;
        protected string? ClientSecret;
        protected string? TenantId;
        protected string? SubscriptionId;
        
        [OneTimeSetUp]
        public void Setup()
        {
            ClientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            ClientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            TenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            SubscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
        }
    }
}