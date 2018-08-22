#if AZURE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Azure.Accounts;
using Calamari.Azure.Commands;
using Calamari.Azure.Deployment;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Integration;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Serialization;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Tests.AzureFixtures
{
    [TestFixture, Explicit]
    public class BlobStorageFixture
    {
        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task UploadPackage1()
        {
            var fileSelections = new List<string> {"Page1.html"};
            var globSelections = new List<string> {"scripts/**/*"};
            var substitutions = new List<string>();

            await Execute("Package1",
                    new List<string> {"Page1.html", "scripts/Script1.js"}, fileSelections, globSelections,
                    substitutions)
                .ConfigureAwait(false);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task UploadPackage2WithSubstitution()
        {
            var BOMMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

            var fileSelections = new List<string> {"Page1.html"};
            var globSelections = new List<string> {"scripts/**/*"};
            var substitutions = new List<string> {"scripts/**/*"};

            await Execute("Package2",
                new List<string> {"Page1.html", "scripts/replace.txt"},
                fileSelections,
                globSelections, substitutions, variables => variables.Set("ReplaceValue", "Hello World"), async blob =>
                {
                    if (blob.Name == "scripts/replace.txt")
                    {
                        var text = await blob.DownloadTextAsync(Encoding.UTF8, null, null, null).ConfigureAwait(false);
                        text.Replace(BOMMarkUtf8, String.Empty).Should().Be("Test=Hello World");
                    }
                }).ConfigureAwait(false);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task UploadPackage3AsAPackage()
        {
            await Execute("Package3",
                    new List<string> {"Package3.1.0.0.zip"},
                    extraVariables: variables =>
                        variables.Set(AzureSpecialVariables.BlobStorage.UploadPackage, Boolean.TrueString))
                .ConfigureAwait(false);
        }

        private async Task Execute(string packageName, IEnumerable<string> filesExist,
            List<string> fileSelections = null,
            List<string> globSelections = null,
            List<string> substitutions = null, Action<VariableDictionary> extraVariables = null,
            Func<CloudBlockBlob, Task> callback = null)
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set(AzureSpecialVariables.BlobStorage.UploadPackage, Boolean.FalseString);
            var enrichedSerializerSettings = GetEnrichedSerializerSettings();
            variables.Set(AzureSpecialVariables.BlobStorage.GlobsSelection,
                JsonConvert.SerializeObject(globSelections, enrichedSerializerSettings));
            variables.Set(AzureSpecialVariables.BlobStorage.FileSelections,
                JsonConvert.SerializeObject(fileSelections, enrichedSerializerSettings));
            variables.Set(AzureSpecialVariables.BlobStorage.SubstitutionPatterns,
                JsonConvert.SerializeObject(substitutions, enrichedSerializerSettings));

            variables.Set(SpecialVariables.Account.AccountType, AzureAccountTypes.ServicePrincipalAccountType);
            variables.Set(SpecialVariables.Action.Azure.SubscriptionId,
                Environment.GetEnvironmentVariable("Azure_OctopusAPITester_SubscriptionId"));
            variables.Set(SpecialVariables.Action.Azure.ClientId,
                Environment.GetEnvironmentVariable("Azure_OctopusAPITester_ClientId"));
            variables.Set(SpecialVariables.Action.Azure.TenantId,
                Environment.GetEnvironmentVariable("Azure_OctopusAPITester_TenantId"));
            variables.Set(SpecialVariables.Action.Azure.Password,
                Environment.GetEnvironmentVariable("Azure_OctopusAPITester_Password"));
            variables.Set(SpecialVariables.Action.Azure.StorageAccountName, "calamaritests");
            variables.Set(SpecialVariables.Action.Azure.ResourceGroupName, "calamaritests");

            extraVariables?.Invoke(variables);

            variables.Save(variablesFile);

            var account = AccountFactory.Create(variables) as AzureServicePrincipalAccount;
            var storageAccountPrimaryKey = await UploadToBlobStorage.GetStorageAccountPrimaryKey(account,
                variables.Get(SpecialVariables.Action.Azure.StorageAccountName),
                variables.Get(SpecialVariables.Action.Azure.ResourceGroupName)).ConfigureAwait(false);
            var cloudStorage =
                new CloudStorageAccount(
                    new StorageCredentials(variables.Get(SpecialVariables.Action.Azure.StorageAccountName),
                        storageAccountPrimaryKey), DefaultVariables.StorageEndpointSuffix, true);

            var blobClient = cloudStorage.CreateCloudBlobClient();
            var containerName = $"{Guid.NewGuid():N}";

            var container = blobClient.GetContainerReference(containerName);
            try
            {
                await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, new BlobRequestOptions(),
                    new OperationContext()).ConfigureAwait(false);

                var packageDirectory = TestEnvironment.GetTestPath("AzureFixtures", "BlobStorage", packageName);
                using (var package =
                    new TemporaryFile(PackageBuilder.BuildSimpleZip(packageName, "1.0.0", packageDirectory)))
                using (new TemporaryFile(variablesFile))
                {
                    var command = new UploadToBlobStorageCommand();
                    command.Execute(new[]
                    {
                        "--package", $"{package.FilePath}", "--variables", $"{variablesFile}", "--container",
                        containerName
                    });
                }

                foreach (var blobName in filesExist)
                {
                    var blob = container.GetBlockBlobReference(blobName);
                    var result = await blob.ExistsAsync().ConfigureAwait(false);
                    result.Should().BeTrue();

                    if (callback != null)
                    {
                        await callback(blob).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                await container.DeleteIfExistsAsync().ConfigureAwait(false);
            }
        }

        private static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            return JsonSerialization.GetDefaultSerializerSettings()
                .Tee(x => { x.ContractResolver = new CamelCasePropertyNamesContractResolver(); });
        }
    }
}
#endif