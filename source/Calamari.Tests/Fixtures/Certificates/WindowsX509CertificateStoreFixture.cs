using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using Calamari.Integration.Certificates;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers.Certificates;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Certificates
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
#pragma warning disable CA1416
    public class WindowsX509CertificateStoreFixture
    {
        [Test]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.CurrentUser, "My")]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.LocalMachine, "Foo")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.LocalMachine, "My")]
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.CurrentUser, "My")]
#endif
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.CurrentUser, "Foo")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyNoPasswordId, StoreLocation.LocalMachine, "My")]
        public void CanImportCertificate(string sampleCertificateId, StoreLocation storeLocation, string storeName)
        {
            var sampleCertificate = SampleCertificate.SampleCertificates[sampleCertificateId];

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);

            new WindowsX509CertificateStore().ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password,
                                                                     storeLocation, storeName, sampleCertificate.HasPrivateKey);

            sampleCertificate.AssertCertificateIsInStore(storeName, storeLocation);

            if (sampleCertificate.HasPrivateKey)
            {
                var certificate = sampleCertificate.GetCertificateFromStore(storeName, storeLocation);
                Assert.True(certificate.HasPrivateKey);
            }

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);
        }

        [Test]
        public void CanImportCertificateWithNoPrivateKeyForSpecificUser()
        {
            // This test cheats a little bit, using the current user 
            var user = WindowsIdentity.GetCurrent().Name;
            var storeName = "My";
            var sampleCertificate = SampleCertificate.CertWithNoPrivateKey;

            sampleCertificate.EnsureCertificateNotInStore(storeName, StoreLocation.CurrentUser);

            new WindowsX509CertificateStore().ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password,
                                                                       user, storeName, sampleCertificate.HasPrivateKey);

            sampleCertificate.AssertCertificateIsInStore(storeName, StoreLocation.CurrentUser);

            sampleCertificate.EnsureCertificateNotInStore(storeName, StoreLocation.CurrentUser);
        }
        
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
        [Test]
        public void CanImportCertificateForSpecificUser()
        {
            // This test cheats a little bit, using the current user

            var user = WindowsIdentity.GetCurrent().Name;
            var storeName = "My";
            var sampleCertificate = SampleCertificate.CapiWithPrivateKey;

            sampleCertificate.EnsureCertificateNotInStore(storeName, StoreLocation.CurrentUser);

            new WindowsX509CertificateStore().ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password,
                                                                       user, storeName, sampleCertificate.HasPrivateKey);

            sampleCertificate.AssertCertificateIsInStore(storeName, StoreLocation.CurrentUser);

            sampleCertificate.EnsureCertificateNotInStore(storeName, StoreLocation.CurrentUser);
        }

        [Test(Description = "This test proves, to a degree of certainty, the WindowsX509CertificateStore is safe for concurrent operations. We were seeing exceptions when multiple processes attempted to get/set private key ACLs at the same time.")]
        public void SafeForConcurrentOperations()
        {
            var maxTimeAllowedForTest = TimeSpan.FromSeconds(20);
            var sw = Stopwatch.StartNew();
            try
            {
                using (var cts = new CancellationTokenSource(maxTimeAllowedForTest))
                {
                    var cancellationToken = cts.Token;
                    var sampleCertificate = SampleCertificate.SampleCertificates[SampleCertificate.CngPrivateKeyId];

                    var numThreads = 20;
                    var numIterationsPerThread = 1;
                    var exceptions = new BlockingCollection<Exception>();
                    void Log(string message) => Console.WriteLine($"{sw.Elapsed} {Thread.CurrentThread.Name}: {message}");

                    new WindowsX509CertificateStore().ImportCertificateToStore(
                                                                             Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password,
                                                                             StoreLocation.LocalMachine, "My", sampleCertificate.HasPrivateKey);

                    CountdownEvent allThreadsReady = null;
                    CountdownEvent allThreadsFinished = null;
                    ManualResetEventSlim goForIt = new ManualResetEventSlim(false);

                    Thread[] CreateThreads(int number, string name, Action action) => Enumerable.Range(0, number)
                        .Select(i => new Thread(() =>
                            {
                                allThreadsReady.Signal();
                                goForIt.Wait(cancellationToken);
                                for (int j = 0; j < numIterationsPerThread; j++)
                                {
                                    try
                                    {
                                        Log($"{name} {j}");
                                        action();
                                    }
                                    catch (Exception e)
                                    {
                                        Log(e.ToString());
                                        exceptions.Add(e);
                                    }
                                }
                                allThreadsFinished.Signal();
                            })
                            {Name = $"{name}#{i}"}).ToArray();

                    var threads =
                        CreateThreads(numThreads, "ImportCertificateToStore", () =>
                            {
                                new WindowsX509CertificateStore().ImportCertificateToStore(
                                                                                         Convert.FromBase64String(sampleCertificate.Base64Bytes()),
                                                                                         sampleCertificate.Password,
                                                                                         StoreLocation.LocalMachine, "My", sampleCertificate.HasPrivateKey);
                            })
                            .Concat(CreateThreads(numThreads, "AddPrivateKeyAccessRules", () =>
                            {
                                new WindowsX509CertificateStore().AddPrivateKeyAccessRules(
                                    sampleCertificate.Thumbprint, StoreLocation.LocalMachine, "My",
                                    new List<PrivateKeyAccessRule>
                                    {
                                        new PrivateKeyAccessRule("BUILTIN\\Users", PrivateKeyAccess.FullControl)
                                    });
                            }))
                            .Concat(CreateThreads(numThreads, "GetPrivateKeySecurity", () =>
                            {
                                var unused = CryptoKeySecurityAccessRules.GetPrivateKeySecurity(
                                    sampleCertificate.Thumbprint, StoreLocation.LocalMachine, "My");
                            })).ToArray();

                    allThreadsReady = new CountdownEvent(threads.Length);
                    allThreadsFinished = new CountdownEvent(threads.Length);

                    foreach (var thread in threads)
                    {
                        thread.Start();
                    }

                    allThreadsReady.Wait(cancellationToken);
                    goForIt.Set();
                    allThreadsFinished.Wait(cancellationToken);

                    foreach (var thread in threads)
                    {
                        Log($"Waiting for {thread.Name} to join...");
                        if (!thread.Join(TimeSpan.FromSeconds(1)))
                        {
                            Log($"Aborting {thread.Name}");
                            thread.Abort();
                        }
                    }

                    sw.Stop();

                    sampleCertificate.EnsureCertificateNotInStore("My", StoreLocation.LocalMachine);

                    if (exceptions.Any())
                        throw new AssertionException(
                            $"The following exceptions were thrown during the test causing it to fail:{Environment.NewLine}{string.Join($"{Environment.NewLine}{new string('=', 80)}", exceptions.GroupBy(ex => ex.Message).Select(g => g.First().ToString()))}");

                    if (sw.Elapsed > maxTimeAllowedForTest)
                        throw new TimeoutException(
                            $"This test exceeded the {maxTimeAllowedForTest} allowed for this test to complete.");
                }

            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"This test took longer than {maxTimeAllowedForTest} to run");
            }
        }


        
        [Test]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.LocalMachine, "Foo")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.LocalMachine, "Foo")]
        public void ImportExistingCertificateShouldNotOverwriteExistingPrivateKeyRights(string sampleCertificateId,
            StoreLocation storeLocation, string storeName)
        {
            var sampleCertificate = SampleCertificate.SampleCertificates[sampleCertificateId];

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);

            new WindowsX509CertificateStore().ImportCertificateToStore(
                Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password,
                storeLocation, storeName, sampleCertificate.HasPrivateKey);

            new WindowsX509CertificateStore().AddPrivateKeyAccessRules(sampleCertificate.Thumbprint, storeLocation, storeName,
                new List<PrivateKeyAccessRule>
                {
                    new PrivateKeyAccessRule("BUILTIN\\Users", PrivateKeyAccess.FullControl)
                });

            new WindowsX509CertificateStore().ImportCertificateToStore(
                Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password,
                storeLocation, storeName, sampleCertificate.HasPrivateKey);

            var privateKeySecurity = CryptoKeySecurityAccessRules.GetPrivateKeySecurity(sampleCertificate.Thumbprint,
                                                                                        storeLocation, storeName);
            AssertHasPrivateKeyRights(privateKeySecurity, "BUILTIN\\Users", CryptoKeyRights.GenericAll);

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);
        }
        void AssertHasPrivateKeyRights(CryptoKeySecurity privateKeySecurity, string identifier, CryptoKeyRights right)
        {
            var accessRules = privateKeySecurity.GetAccessRules(true, false, typeof(NTAccount));

            var found = accessRules.Cast<CryptoKeyAccessRule>()
                                   .Any(x => x.IdentityReference.Value == identifier && x.CryptoKeyRights.HasFlag(right));

            Assert.True(found, "Private-Key right was not set");
        }
#endif

        [Test]
        [TestCase(SampleCertificate.CertificateChainId, "2E5DEC036985A4028351FD8DF3532E49D7B34049", "CC7ED077F0F292595A8166B01709E20C0884A5F8", StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.CertificateChainId, "2E5DEC036985A4028351FD8DF3532E49D7B34049", "CC7ED077F0F292595A8166B01709E20C0884A5F8", StoreLocation.CurrentUser, "My")]
        [TestCase(SampleCertificate.CertificateChainId, "2E5DEC036985A4028351FD8DF3532E49D7B34049", "CC7ED077F0F292595A8166B01709E20C0884A5F8", StoreLocation.LocalMachine, "Foo")]
        [TestCase(SampleCertificate.CertificateChainId, "2E5DEC036985A4028351FD8DF3532E49D7B34049", "CC7ED077F0F292595A8166B01709E20C0884A5F8", StoreLocation.CurrentUser, "Foo")]
        [TestCase(SampleCertificate.ChainSignedByLegacySha1RsaId, null, "A87058F92D01C0B7D4ED21F83D12DD270E864F50", StoreLocation.LocalMachine, "My")]
        public void CanImportCertificateChain(string sampleCertificateId, string intermediateAuthorityThumbprint, string rootAuthorityThumbprint, StoreLocation storeLocation, string storeName)
        {
            var sampleCertificate = SampleCertificate.SampleCertificates[sampleCertificateId];

            // intermediate and root authority certificates are always imported to LocalMachine
            var rootAuthorityStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            rootAuthorityStore.Open(OpenFlags.ReadWrite);
            var intermediateAuthorityStore = new X509Store(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
            intermediateAuthorityStore.Open(OpenFlags.ReadWrite);

            RemoveChainCertificatesFromStore(rootAuthorityStore, intermediateAuthorityStore, "CC7ED077F0F292595A8166B01709E20C0884A5999", intermediateAuthorityThumbprint);

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);

            new WindowsX509CertificateStore().ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password,
                storeLocation, storeName, sampleCertificate.HasPrivateKey);

            sampleCertificate.AssertCertificateIsInStore(storeName, storeLocation);

            // Assert chain certificates were imported
            if (!string.IsNullOrEmpty(intermediateAuthorityThumbprint))
                AssertCertificateInStore(intermediateAuthorityStore, intermediateAuthorityThumbprint);

            AssertCertificateInStore(rootAuthorityStore, rootAuthorityThumbprint);

            var certificate = sampleCertificate.GetCertificateFromStore(storeName, storeLocation);
            Assert.True(certificate.HasPrivateKey);

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);

            RemoveChainCertificatesFromStore(rootAuthorityStore, intermediateAuthorityStore, rootAuthorityThumbprint, intermediateAuthorityThumbprint);
        }

        void RemoveChainCertificatesFromStore(X509Store rootAuthorityStore, X509Store intermediateAuthorityStore, string rootAuthorityThumbprint, string intermediateAuthorityThumbprint)
        {
            new WindowsX509CertificateStore().RemoveCertificateFromStore(rootAuthorityThumbprint, StoreLocation.LocalMachine, rootAuthorityStore.Name);

            if (!string.IsNullOrEmpty(intermediateAuthorityThumbprint))
                new WindowsX509CertificateStore().RemoveCertificateFromStore(intermediateAuthorityThumbprint, StoreLocation.LocalMachine, intermediateAuthorityStore.Name);
        }

        private static void AssertCertificateInStore(X509Store store, string thumbprint)
        {
            Thread.Sleep(TimeSpan.FromSeconds(2)); //Lets try this for the hell of it and see if the test gets less flakey
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            Assert.AreEqual(1, found.Count);
        }


    }
#pragma warning restore CA1416
}
