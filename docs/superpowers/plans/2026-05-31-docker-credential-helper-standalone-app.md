# Docker Credential Helper Standalone App — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-implement the Docker credential helper as a standalone executable (`docker-credential-octopus`) that Docker invokes directly, removing the invasive Calamari startup/logging changes.

**Architecture:** Extract the credential store + AES crypto into `Calamari.Common`. A new `Calamari.DockerCredentialHelper` exe speaks Docker's credential protocol over its own stdin/stdout (no Calamari host). The package downloader locates that binary next to Calamari, puts it on `PATH`, and wires `config.json`. The binary is published per-RID and overlaid into Calamari's publish folder.

**Tech Stack:** .NET 8, NUnit, NSubstitute, Nuke (build), `AesEncryption.ForScripts`, `System.Text.Json`.

**Context:** This plan operates on top of branch `mattr/docker-credential-helper-refactor` (which already contains PR #1542's changes). The plan refactors that work — extracting shared code, adding the standalone exe, rewiring the downloader, and reverting the invasive plumbing.

**Spec:** `docs/superpowers/specs/2026-05-31-docker-credential-helper-standalone-app-design.md`

---

## File Structure

**Created:**
- `source/Calamari.Common/Features/Docker/DockerCredentialStore.cs` — shared credential store + AES crypto + `DockerCredential`.
- `source/Calamari.DockerCredentialHelper/Calamari.DockerCredentialHelper.csproj` — the standalone exe project.
- `source/Calamari.DockerCredentialHelper/Program.cs` — thin entry point (env + Console wiring).
- `source/Calamari.DockerCredentialHelper/DockerCredentialProtocol.cs` — testable protocol handler.
- `source/Calamari.Tests/Fixtures/Docker/DockerCredentialStoreFixture.cs` — store unit tests.
- `source/Calamari.Tests/Fixtures/Docker/DockerCredentialProtocolFixture.cs` — protocol unit tests.

**Modified:**
- `source/Calamari.Shared/Integration/Packages/Download/DockerCredentialHelper.cs` — trimmed to orchestration only, delegates to `DockerCredentialStore`, locates the binary, no scripts.
- `source/Calamari.Shared/Integration/Packages/Download/DockerImagePackageDownloader.cs` — fallback retries on any non-zero login exit (string match is diagnostic only).
- `source/Calamari/Program.cs` — revert `DeferredLogger` wiring.
- `source/Calamari.Common/CalamariFlavourProgram.cs` — revert `protected` → private `log`.
- `source/Calamari.Tests/Calamari.Tests.csproj` — add project reference to the helper.
- `build/Build.PackageCalamariProjects.cs` — overlay the helper into Calamari's publish dir.
- `source/Calamari.sln` — add the new project.

**Deleted:**
- `source/Calamari/Commands/DockerCredentialCommand.cs`
- `source/Calamari.Common/Plumbing/Logging/DeferredLogger.cs`
- `source/Calamari.Common/Commands/IWantCustomHandlingOfDeferredLogs.cs`
- `source/Calamari.Shared/Integration/Packages/Download/Scripts/docker-credential-octopus.ps1`
- `source/Calamari.Shared/Integration/Packages/Download/Scripts/docker-credential-octopus.sh`
- `source/Calamari.Tests/Fixtures/Integration/Packages/DockerCredentialScriptsFixture.cs`

**Retained from the PR (per spec decision):** `InMemoryCommandOutputSink.cs` and the `CommandLineRunner` optional-sink change (the diagnostic string match still needs login output).

---

## Task 1: Extract `DockerCredentialStore` into `Calamari.Common`

**Files:**
- Create: `source/Calamari.Common/Features/Docker/DockerCredentialStore.cs`
- Test: `source/Calamari.Tests/Fixtures/Docker/DockerCredentialStoreFixture.cs`

- [ ] **Step 1: Write the failing test**

Create `source/Calamari.Tests/Fixtures/Docker/DockerCredentialStoreFixture.cs`:

```csharp
using System.IO;
using Calamari.Common.Features.Docker;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Docker
{
    [TestFixture]
    public class DockerCredentialStoreFixture
    {
        string configPath;

        [SetUp]
        public void SetUp()
        {
            configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(configPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(configPath))
                Directory.Delete(configPath, true);
        }

        [Test]
        public void StoreThenGet_RoundTripsCredentials()
        {
            var store = new DockerCredentialStore();
            store.Store("https://index.docker.io/v1/", "alice", "s3cret", "password123", configPath);

            var result = store.Get("https://index.docker.io/v1/", "password123", configPath);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Username, Is.EqualTo("alice"));
            Assert.That(result.Secret, Is.EqualTo("s3cret"));
        }

        [Test]
        public void Get_WithWrongPassword_ReturnsNull()
        {
            var store = new DockerCredentialStore();
            store.Store("https://example.com", "bob", "pw", "password123", configPath);

            Assert.That(store.Get("https://example.com", "WrongPassword", configPath), Is.Null);
        }

        [Test]
        public void Get_WhenNoCredentialStored_ReturnsNull()
        {
            var store = new DockerCredentialStore();
            Assert.That(store.Get("https://example.com", "password123", configPath), Is.Null);
        }

        [Test]
        public void Erase_RemovesStoredCredential()
        {
            var store = new DockerCredentialStore();
            store.Store("https://example.com", "bob", "pw", "password123", configPath);

            store.Erase("https://example.com", configPath);

            Assert.That(store.Get("https://example.com", "password123", configPath), Is.Null);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~DockerCredentialStoreFixture"`
Expected: FAIL — compile error, `DockerCredentialStore` does not exist.

- [ ] **Step 3: Create the implementation**

Create `source/Calamari.Common/Features/Docker/DockerCredentialStore.cs`:

```csharp
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Common.Features.Docker
{
    public class DockerCredentialStore
    {
        const string CredentialsDirectory = "credentials";

        public void Store(string serverUrl, string username, string secret, string encryptionPassword, string dockerConfigPath)
        {
            var credentialsDir = Path.Combine(dockerConfigPath, CredentialsDirectory);
            Directory.CreateDirectory(credentialsDir);

            var credential = new DockerCredential { Username = username, Secret = secret };
            var credentialJson = JsonSerializer.Serialize(credential);

            var encryptor = AesEncryption.ForScripts(encryptionPassword);
            var encryptedBytes = encryptor.Encrypt(credentialJson);

            var filePath = Path.Combine(credentialsDir, GetCredentialFileName(serverUrl));
            File.WriteAllBytes(filePath, encryptedBytes);
        }

        public DockerCredential? Get(string serverUrl, string encryptionPassword, string dockerConfigPath)
        {
            var filePath = Path.Combine(dockerConfigPath, CredentialsDirectory, GetCredentialFileName(serverUrl));
            if (!File.Exists(filePath))
                return null;

            try
            {
                var encryptedBytes = File.ReadAllBytes(filePath);
                var encryptor = AesEncryption.ForScripts(encryptionPassword);
                var credentialJson = encryptor.Decrypt(encryptedBytes);
                return JsonSerializer.Deserialize<DockerCredential>(credentialJson);
            }
            catch
            {
                return null;
            }
        }

        public void Erase(string serverUrl, string dockerConfigPath)
        {
            var filePath = Path.Combine(dockerConfigPath, CredentialsDirectory, GetCredentialFileName(serverUrl));
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public static string GetCredentialFileName(string serverUrl)
        {
            var serverBytes = Encoding.UTF8.GetBytes(serverUrl);
            var base64Server = Convert.ToBase64String(serverBytes)
                                      .Replace("/", "_")
                                      .Replace("+", "-")
                                      .Replace("=", "");
            return $"{base64Server}.cred";
        }
    }

    public class DockerCredential
    {
        public string Username { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~DockerCredentialStoreFixture"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.Common/Features/Docker/DockerCredentialStore.cs source/Calamari.Tests/Fixtures/Docker/DockerCredentialStoreFixture.cs
git commit -m "Extract DockerCredentialStore into Calamari.Common"
```

---

## Task 2: Add the credential protocol handler (testable core of the exe)

**Files:**
- Create: `source/Calamari.DockerCredentialHelper/Calamari.DockerCredentialHelper.csproj`
- Create: `source/Calamari.DockerCredentialHelper/DockerCredentialProtocol.cs`
- Modify: `source/Calamari.Tests/Calamari.Tests.csproj` (add project reference)
- Test: `source/Calamari.Tests/Fixtures/Docker/DockerCredentialProtocolFixture.cs`

- [ ] **Step 1: Create the helper project file**

Create `source/Calamari.DockerCredentialHelper/Calamari.DockerCredentialHelper.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <AssemblyName>docker-credential-octopus</AssemblyName>
        <RootNamespace>Calamari.DockerCredentialHelper</RootNamespace>
        <TargetFramework>net8.0</TargetFramework>
        <RuntimeIdentifiers>win-x64;linux-x64;osx-x64;linux-arm;linux-arm64</RuntimeIdentifiers>
        <Nullable>enable</Nullable>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Calamari.Common\Calamari.Common.csproj" />
    </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the project to the solution and reference it from the test project**

Run:
```bash
dotnet sln source/Calamari.sln add source/Calamari.DockerCredentialHelper/Calamari.DockerCredentialHelper.csproj
dotnet add source/Calamari.Tests/Calamari.Tests.csproj reference source/Calamari.DockerCredentialHelper/Calamari.DockerCredentialHelper.csproj
```

- [ ] **Step 3: Write the failing test**

Create `source/Calamari.Tests/Fixtures/Docker/DockerCredentialProtocolFixture.cs`:

```csharp
using System.IO;
using Calamari.Common.Features.Docker;
using Calamari.DockerCredentialHelper;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Docker
{
    [TestFixture]
    public class DockerCredentialProtocolFixture
    {
        const string Password = "password123";
        string configPath;
        DockerCredentialProtocol protocol;

        [SetUp]
        public void SetUp()
        {
            configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(configPath);
            protocol = new DockerCredentialProtocol(new DockerCredentialStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(configPath))
                Directory.Delete(configPath, true);
        }

        [Test]
        public void Store_ThenGet_WritesCredentialJsonToStdout()
        {
            var storeInput = new StringReader("{\"ServerURL\":\"https://example.com\",\"Username\":\"alice\",\"Secret\":\"s3cret\"}");
            var storeExit = protocol.Run("store", storeInput, new StringWriter(), new StringWriter(), Password, configPath);
            Assert.That(storeExit, Is.EqualTo(0));

            var getOutput = new StringWriter();
            var getExit = protocol.Run("get", new StringReader("https://example.com"), getOutput, new StringWriter(), Password, configPath);

            Assert.That(getExit, Is.EqualTo(0));
            Assert.That(getOutput.ToString(), Does.Contain("alice"));
            Assert.That(getOutput.ToString(), Does.Contain("s3cret"));
        }

        [Test]
        public void Get_WhenMissing_ReturnsExitOneAndNotFoundMessage()
        {
            var error = new StringWriter();
            var exit = protocol.Run("get", new StringReader("https://example.com"), new StringWriter(), error, Password, configPath);

            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("credentials not found"));
        }

        [Test]
        public void Erase_RemovesCredential()
        {
            protocol.Run("store",
                         new StringReader("{\"ServerURL\":\"https://example.com\",\"Username\":\"alice\",\"Secret\":\"s3cret\"}"),
                         new StringWriter(), new StringWriter(), Password, configPath);

            var eraseExit = protocol.Run("erase", new StringReader("https://example.com"), new StringWriter(), new StringWriter(), Password, configPath);
            Assert.That(eraseExit, Is.EqualTo(0));

            var getExit = protocol.Run("get", new StringReader("https://example.com"), new StringWriter(), new StringWriter(), Password, configPath);
            Assert.That(getExit, Is.EqualTo(1));
        }

        [Test]
        public void Run_WithUnknownOperation_ReturnsExitOne()
        {
            var error = new StringWriter();
            var exit = protocol.Run("bogus", new StringReader(""), new StringWriter(), error, Password, configPath);

            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("Invalid operation"));
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~DockerCredentialProtocolFixture"`
Expected: FAIL — `DockerCredentialProtocol` does not exist.

- [ ] **Step 5: Create the protocol handler**

Create `source/Calamari.DockerCredentialHelper/DockerCredentialProtocol.cs`:

```csharp
using System.IO;
using System.Text.Json;
using Calamari.Common.Features.Docker;

namespace Calamari.DockerCredentialHelper
{
    public class DockerCredentialProtocol
    {
        readonly DockerCredentialStore store;

        public DockerCredentialProtocol(DockerCredentialStore store)
        {
            this.store = store;
        }

        public int Run(string operation, TextReader input, TextWriter output, TextWriter error, string encryptionPassword, string dockerConfigPath)
        {
            switch (operation.ToLowerInvariant())
            {
                case "store":
                    return Store(input, error, encryptionPassword, dockerConfigPath);
                case "get":
                    return Get(input, output, error, encryptionPassword, dockerConfigPath);
                case "erase":
                    return Erase(input, dockerConfigPath);
                default:
                    error.WriteLine($"Invalid operation: {operation}. Valid operations are: store, get, erase");
                    return 1;
            }
        }

        int Store(TextReader input, TextWriter error, string encryptionPassword, string dockerConfigPath)
        {
            var request = JsonSerializer.Deserialize<StoreRequest>(input.ReadToEnd());
            if (request == null || string.IsNullOrEmpty(request.ServerURL))
            {
                error.WriteLine("Invalid store request");
                return 1;
            }

            store.Store(request.ServerURL, request.Username, request.Secret, encryptionPassword, dockerConfigPath);
            return 0;
        }

        int Get(TextReader input, TextWriter output, TextWriter error, string encryptionPassword, string dockerConfigPath)
        {
            var serverUrl = input.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(serverUrl))
            {
                error.WriteLine("No server URL provided");
                return 1;
            }

            var credential = store.Get(serverUrl, encryptionPassword, dockerConfigPath);
            if (credential == null)
            {
                error.WriteLine("credentials not found in native keychain");
                return 1;
            }

            var response = new GetResponse { ServerURL = serverUrl, Username = credential.Username, Secret = credential.Secret };
            output.WriteLine(JsonSerializer.Serialize(response));
            return 0;
        }

        int Erase(TextReader input, string dockerConfigPath)
        {
            var serverUrl = input.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(serverUrl))
                store.Erase(serverUrl, dockerConfigPath);
            return 0;
        }

        class StoreRequest
        {
            public string ServerURL { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Secret { get; set; } = string.Empty;
        }

        class GetResponse
        {
            public string ServerURL { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Secret { get; set; } = string.Empty;
        }
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~DockerCredentialProtocolFixture"`
Expected: PASS (4 tests).

- [ ] **Step 7: Commit**

```bash
git add source/Calamari.DockerCredentialHelper/ source/Calamari.sln source/Calamari.Tests/Calamari.Tests.csproj source/Calamari.Tests/Fixtures/Docker/DockerCredentialProtocolFixture.cs
git commit -m "Add Calamari.DockerCredentialHelper project and protocol handler"
```

---

## Task 3: Add the exe entry point

**Files:**
- Create: `source/Calamari.DockerCredentialHelper/Program.cs`

- [ ] **Step 1: Create the entry point**

Docker invokes the helper positionally as `docker-credential-octopus <operation>`, reading/writing the credential payload over stdin/stdout, and passes the parent process environment through. Create `source/Calamari.DockerCredentialHelper/Program.cs`:

```csharp
using System;
using Calamari.Common.Features.Docker;

namespace Calamari.DockerCredentialHelper
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("A credential operation is required (store, get, erase)");
                return 1;
            }

            var operation = args[0];
            var encryptionPassword = Environment.GetEnvironmentVariable("OCTOPUS_CREDENTIAL_PASSWORD");
            var dockerConfigPath = Environment.GetEnvironmentVariable("DOCKER_CONFIG");

            if (string.IsNullOrEmpty(encryptionPassword))
            {
                Console.Error.WriteLine("OCTOPUS_CREDENTIAL_PASSWORD environment variable not set");
                return 1;
            }

            if (string.IsNullOrEmpty(dockerConfigPath))
            {
                Console.Error.WriteLine("DOCKER_CONFIG environment variable not set");
                return 1;
            }

            try
            {
                var protocol = new DockerCredentialProtocol(new DockerCredentialStore());
                return protocol.Run(operation, Console.In, Console.Out, Console.Error, encryptionPassword, dockerConfigPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Docker credential operation failed: {ex.Message}");
                return 1;
            }
        }
    }
}
```

- [ ] **Step 2: Verify the exe builds and runs end-to-end**

Run (Unix):
```bash
dotnet build source/Calamari.DockerCredentialHelper/Calamari.DockerCredentialHelper.csproj
CFG=$(mktemp -d) ; \
OCTOPUS_CREDENTIAL_PASSWORD=pw DOCKER_CONFIG="$CFG" \
  bash -c 'echo "{\"ServerURL\":\"https://example.com\",\"Username\":\"alice\",\"Secret\":\"s3cret\"}" | dotnet run --project source/Calamari.DockerCredentialHelper -- store' ; \
OCTOPUS_CREDENTIAL_PASSWORD=pw DOCKER_CONFIG="$CFG" \
  bash -c 'echo "https://example.com" | dotnet run --project source/Calamari.DockerCredentialHelper -- get'
```
Expected: the final command prints a JSON line containing `alice` and `s3cret`.

- [ ] **Step 3: Commit**

```bash
git add source/Calamari.DockerCredentialHelper/Program.cs
git commit -m "Add docker-credential-octopus entry point"
```

---

## Task 4: Rewire the downloader to use the standalone binary

This trims `DockerCredentialHelper` (Calamari.Shared) to orchestration only — locating the binary, setting env, storing credentials via `DockerCredentialStore`, and writing `config.json` — and removes script deployment and the `OCTOPUS_CALAMARI_EXECUTABLE` indirection.

**Files:**
- Modify (replace): `source/Calamari.Shared/Integration/Packages/Download/DockerCredentialHelper.cs`
- Modify: `source/Calamari.Shared/Integration/Packages/Download/DockerImagePackageDownloader.cs:160-181` (PerformLogin)

- [ ] **Step 1: Replace `DockerCredentialHelper.cs` with the trimmed orchestration class**

Replace the entire contents of `source/Calamari.Shared/Integration/Packages/Download/DockerCredentialHelper.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Docker;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Integration.Packages.Download
{
    public class DockerCredentialHelper
    {
        // Docker resolves credential helpers by the binary name `docker-credential-<name>`.
        const string CredentialHelperName = "octopus";

        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly DockerCredentialStore store = new DockerCredentialStore();

        public DockerCredentialHelper(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public bool SetupCredentialHelper(Dictionary<string, string> environmentVariables,
                                          IVariables variables,
                                          Uri feedUri,
                                          string username,
                                          string password,
                                          string dockerHubRegistry)
        {
            try
            {
                var dockerConfigPath = environmentVariables["DOCKER_CONFIG"];
                Directory.CreateDirectory(dockerConfigPath);

                var encryptionPassword = GetEncryptionPassword(variables);

                // docker-credential-octopus is published alongside Calamari, so it lives in the app base directory.
                AddDirectoryToPath(environmentVariables, AppContext.BaseDirectory);
                environmentVariables["OCTOPUS_CREDENTIAL_PASSWORD"] = encryptionPassword;

                var serverUrl = GetServerUrlForCredentialHelper(feedUri, dockerHubRegistry);
                store.Store(serverUrl, username, password, encryptionPassword, dockerConfigPath);

                CreateDockerConfig(dockerConfigPath, BuildCredHelpers(feedUri, dockerHubRegistry));

                log.Verbose($"Configured Docker credential helper for {serverUrl}");
                return true;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to setup credential helper: {ex.Message}");
                return false;
            }
        }

        public void CleanupCredentialHelper(Dictionary<string, string> environmentVariables)
        {
            try
            {
                var dockerConfigPath = environmentVariables["DOCKER_CONFIG"];

                var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
                if (Directory.Exists(credentialsDir))
                    Directory.Delete(credentialsDir, recursive: true);

                var configFilePath = Path.Combine(dockerConfigPath, "config.json");
                if (File.Exists(configFilePath))
                    File.Delete(configFilePath);
            }
            catch (Exception ex)
            {
                log.Verbose($"Failed to cleanup credential helper files: {ex.Message}");
            }
        }

        public string CreateDockerConfig(string dockerConfigPath, Dictionary<string, string> credHelpers)
        {
            var config = new DockerConfig { CredHelpers = credHelpers };
            var configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            var configFilePath = Path.Combine(dockerConfigPath, "config.json");
            File.WriteAllText(configFilePath, configJson);
            return configFilePath;
        }

        public static string GetServerUrlForCredentialHelper(Uri feedUri, string dockerHubRegistry)
        {
            if (feedUri.Host.Equals(dockerHubRegistry))
                return "https://index.docker.io/v1/";

            return feedUri.GetLeftPart(UriPartial.Authority);
        }

        static string GetEncryptionPassword(IVariables variables)
        {
            // NOTE: carried over from PR #1542 — confirm the correct sensitive-variable password
            // source during review (see spec "Open implementation notes").
            return variables.Get("Octopus.Action.Package.DownloadOnTentacle")
                   ?? variables.Get("SensitiveVariablesPassword")
                   ?? "DefaultFallbackPassword";
        }

        static Dictionary<string, string> BuildCredHelpers(Uri feedUri, string dockerHubRegistry)
        {
            var credHelpers = new Dictionary<string, string>();
            if (feedUri.Host.Equals(dockerHubRegistry))
            {
                credHelpers["index.docker.io"] = CredentialHelperName;
                credHelpers["docker.io"] = CredentialHelperName;
                credHelpers["registry-1.docker.io"] = CredentialHelperName;
                credHelpers["https://index.docker.io/v1/"] = CredentialHelperName;
            }
            else
            {
                credHelpers[feedUri.Host] = CredentialHelperName;
                if (feedUri.Port != -1 && feedUri.Port != 80 && feedUri.Port != 443)
                    credHelpers[$"{feedUri.Host}:{feedUri.Port}"] = CredentialHelperName;
            }

            return credHelpers;
        }

        static void AddDirectoryToPath(Dictionary<string, string> environmentVariables, string directory)
        {
            var pathSeparator = CalamariEnvironment.IsRunningOnWindows ? ";" : ":";
            var currentPath = environmentVariables.TryGetValue("PATH", out var existing)
                ? existing
                : Environment.GetEnvironmentVariable("PATH") ?? "";

            if (!currentPath.Split(pathSeparator.ToCharArray()).Contains(directory))
                environmentVariables["PATH"] = $"{directory}{pathSeparator}{currentPath}";
        }
    }

    public class DockerConfig
    {
        [JsonProperty("credHelpers")]
        public Dictionary<string, string> CredHelpers { get; set; } = new Dictionary<string, string>();
    }
}
```

- [ ] **Step 2: Update `PerformLogin` so the fallback triggers on any non-zero exit**

In `source/Calamari.Shared/Integration/Packages/Download/DockerImagePackageDownloader.cs`, replace the `PerformLogin` method with:

```csharp
        void PerformLogin(string? username, string? password, string feed, Dictionary<string, string> dictionary, bool allowCredentialHelperFallback = true)
        {
            var envVars = new Dictionary<string, string>(dictionary);
            envVars["DockerUsername"] = username;
            envVars["DockerPassword"] = password;
            envVars["FeedUri"] = feed;

            var (result, stdOut) = ExecuteScript("DockerLogin", envVars);
            if (result == null)
                throw new CommandException("Null result attempting to log in Docker registry");
            if (result.ExitCode != 0)
            {
                if (useCredentialHelper && allowCredentialHelperFallback)
                {
                    // The string match is diagnostic only — we fall back on any non-zero exit.
                    var knownHelperError = stdOut.Contains("Error saving credentials");
                    log.Verbose(knownHelperError
                                    ? "Docker login failed due to a credential helper error; retrying without the credential helper."
                                    : "Docker login failed while the credential helper was enabled; retrying without the credential helper.");
                    dockerCredentialHelper.CleanupCredentialHelper(environmentVariables);
                    PerformLogin(username, password, feed, dictionary, allowCredentialHelperFallback: false);
                    return;
                }

                throw new CommandException("Unable to log in Docker registry");
            }
        }
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build source/Calamari.Shared/Calamari.Shared.csproj`
Expected: Build succeeded. (Removed members `StoreCredentials`/`GetCredentials`/`EraseCredentials`/`CleanupCredentials`/`DeployCredentialHelperScript`/`GetCalamariExecutablePath` no longer exist; if the build reports them used elsewhere, that is the test fixtures handled in Task 7.)

- [ ] **Step 4: Commit**

```bash
git add source/Calamari.Shared/Integration/Packages/Download/DockerCredentialHelper.cs source/Calamari.Shared/Integration/Packages/Download/DockerImagePackageDownloader.cs
git commit -m "Rewire downloader to use standalone credential helper binary"
```

---

## Task 5: Revert the invasive startup/logging plumbing

**Files:**
- Modify: `source/Calamari/Program.cs`
- Modify: `source/Calamari.Common/CalamariFlavourProgram.cs:31`
- Delete: `source/Calamari/Commands/DockerCredentialCommand.cs`
- Delete: `source/Calamari.Common/Plumbing/Logging/DeferredLogger.cs`
- Delete: `source/Calamari.Common/Commands/IWantCustomHandlingOfDeferredLogs.cs`
- Delete: `source/Calamari.Shared/Integration/Packages/Download/Scripts/docker-credential-octopus.ps1`
- Delete: `source/Calamari.Shared/Integration/Packages/Download/Scripts/docker-credential-octopus.sh`

- [ ] **Step 1: Revert the `Program` constructor**

In `source/Calamari/Program.cs`, change:

```csharp
        protected Program(ILog log) : base(new DeferredLogger(log))
        {
        }
```

to:

```csharp
        protected Program(ILog log) : base(log)
        {
        }
```

- [ ] **Step 2: Revert `ResolveAndExecuteCommand` and remove the flush helpers**

In `source/Calamari/Program.cs`, replace this block:

```csharp
            var commandCandidates = commands.Where(x => x.Metadata.Name.Equals(options.Command, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (commandCandidates.Length == 0)
            {
                FlushDeferredLogsIfApplicable();
                throw new CommandException($"Could not find the command {options.Command}");
            }

            if (commandCandidates.Length > 1)
            {
                FlushDeferredLogsIfApplicable();
                throw new CommandException($"Multiple commands found with the name {options.Command}");
            }

            var command = commandCandidates[0].Value.Value;
            
            FlushDeferredLogsIfApplicable(command);

            return command.Execute(options.RemainingArguments.ToArray());
        }

        void FlushDeferredLogsIfApplicable()
        {
            if (log is DeferredLogger deferredLogger)
            {
                deferredLogger.FlushDeferredLogs();
            }
        }
        
        void FlushDeferredLogsIfApplicable(ICommandWithArgs command)
        {
            if (log is DeferredLogger deferredLogger && !(command is IWantCustomHandlingOfDeferredLogs))
            {
                // Release any deferred logs given that the command being executed does not handle deferred logs itself.
                deferredLogger.FlushDeferredLogs();
            }
        }
```

with:

```csharp
            var commandCandidates = commands.Where(x => x.Metadata.Name.Equals(options.Command, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (commandCandidates.Length == 0)
                throw new CommandException($"Could not find the command {options.Command}");
            if (commandCandidates.Length > 1)
                throw new CommandException($"Multiple commands found with the name {options.Command}");

            return commandCandidates[0].Value.Value.Execute(options.RemainingArguments.ToArray());
        }
```

- [ ] **Step 3: Revert the `log` field access modifier**

In `source/Calamari.Common/CalamariFlavourProgram.cs`, change:

```csharp
        protected readonly ILog log;
```

back to:

```csharp
        readonly ILog log;
```

- [ ] **Step 4: Delete the now-unused files**

Run:
```bash
git rm source/Calamari/Commands/DockerCredentialCommand.cs \
       source/Calamari.Common/Plumbing/Logging/DeferredLogger.cs \
       source/Calamari.Common/Commands/IWantCustomHandlingOfDeferredLogs.cs \
       source/Calamari.Shared/Integration/Packages/Download/Scripts/docker-credential-octopus.ps1 \
       source/Calamari.Shared/Integration/Packages/Download/Scripts/docker-credential-octopus.sh
```

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build source/Calamari/Calamari.csproj`
Expected: Build succeeded with no references to `DeferredLogger`, `IWantCustomHandlingOfDeferredLogs`, or `DockerCredentialCommand`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Revert invasive DeferredLogger plumbing; remove docker-credential subcommand and scripts"
```

---

## Task 6: Overlay the helper binary into Calamari's publish output

The helper must sit next to `Calamari` in the published output for each RID. The Nuke publish step publishes the helper self-contained into Calamari's per-RID publish directory (`publish/Calamari/<rid>`), before signing and compression.

**Files:**
- Modify: `build/Build.PackageCalamariProjects.cs` (inside `PublishCalamariProjects`, after the publish loop)

- [ ] **Step 1: Add the overlay step**

In `build/Build.PackageCalamariProjects.cs`, in the `PublishCalamariProjects` target, immediately after:

```csharp
                           await Task.WhenAll(publishTasks);
```

insert:

```csharp
                           // Overlay the standalone Docker credential helper into each Calamari runtime folder
                           // so Docker can invoke `docker-credential-octopus` directly from the deployed package.
                           var calamariProject = Solution.GetProject("Calamari");
                           var helperProject = Solution.GetProject("Calamari.DockerCredentialHelper");
                           if (calamariProject != null && helperProject != null)
                           {
                               foreach (var rid in GetRuntimeIdentifiers(calamariProject))
                               {
                                   var calamariRidDirectory = KnownPaths.PublishDirectory / "Calamari" / rid;
                                   Log.Information("Overlaying docker-credential-octopus into {Directory}", calamariRidDirectory);
                                   DotNetPublish(s => s
                                                      .SetConfiguration(Configuration)
                                                      .SetProject(helperProject)
                                                      .SetFramework(Frameworks.Net80)
                                                      .SetRuntime(rid)
                                                      .SetSelfContained(OperatingSystem.IsWindows())
                                                      .SetOutput(calamariRidDirectory));
                               }
                           }
```

- [ ] **Step 2: Verify the helper lands beside Calamari**

Run (single RID for speed; `--target-runtime` filters `GetRuntimeIdentifiers`):
```bash
cd build && ./build.sh PublishCalamariProjects --target-runtime linux-x64
```
(If the repo uses `nuke`/`dotnet nuke` instead of `build.sh`, use that — check `build/` for the entry script.)

Then confirm:
```bash
ls publish/Calamari/linux-x64/docker-credential-octopus
```
Expected: the file exists alongside the `Calamari` apphost in the same directory.

- [ ] **Step 3: Commit**

```bash
git add build/Build.PackageCalamariProjects.cs
git commit -m "Overlay docker-credential-octopus into Calamari publish output"
```

---

## Task 7: Clean up the credential-helper test fixtures

The store/protocol behaviour is now covered by Tasks 1 and 2. Remove the obsolete script fixture and the now-invalid store-method tests on `DockerCredentialHelper`.

**Files:**
- Delete: `source/Calamari.Tests/Fixtures/Integration/Packages/DockerCredentialScriptsFixture.cs`
- Modify: `source/Calamari.Tests/Fixtures/Integration/Packages/DockerCredentialHelperFixture.cs`
- Verify: `source/Calamari.Tests/Fixtures/Integration/Packages/DockerImagePackageDownloaderCredentialHelperFixture.cs`

- [ ] **Step 1: Delete the scripts fixture**

The `.sh`/`.ps1` scripts no longer exist (deleted in Task 5), so this fixture cannot compile.

Run:
```bash
git rm source/Calamari.Tests/Fixtures/Integration/Packages/DockerCredentialScriptsFixture.cs
```

- [ ] **Step 2: Remove the obsolete store tests from `DockerCredentialHelperFixture.cs`**

Open `source/Calamari.Tests/Fixtures/Integration/Packages/DockerCredentialHelperFixture.cs` and delete every test method that calls the removed members. Concretely, remove the methods that call any of:
`StoreCredentials`, `GetCredentials`, `EraseCredentials`, `CleanupCredentials`.

Based on the current file these are: `StoreCredentials_CreatesEncryptedCredentialFile`, `GetCredentials_RetrievesStoredCredentials`, `GetCredentials_WithWrongPassword_ReturnsNull`, `GetCredentials_WithNonExistentCredentials_ReturnsNull`, `EraseCredentials_RemovesStoredCredentials`, the `CleanupCredentials` test, and any parameterised tests that store/get credentials (the loops around lines 169-224).

**Keep** any test that exercises the still-public surface: `CreateDockerConfig(...)` and `DockerCredentialHelper.GetServerUrlForCredentialHelper(...)`. If, after removing the obsolete methods, no tests remain, delete the file with `git rm` instead (store/protocol coverage lives in the Task 1 and Task 2 fixtures).

- [ ] **Step 3: Verify the downloader credential-helper fixture still compiles**

Open `source/Calamari.Tests/Fixtures/Integration/Packages/DockerImagePackageDownloaderCredentialHelperFixture.cs`. It does not reference any removed members (verified during planning). If it references the deleted scripts or `OCTOPUS_CALAMARI_EXECUTABLE`, update those assertions to expect `docker-credential-octopus` on `PATH` and the `OCTOPUS_CREDENTIAL_PASSWORD` env var instead.

- [ ] **Step 4: Build the test project to verify everything compiles**

Run: `dotnet build source/Calamari.Tests/Calamari.Tests.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Run the Docker credential test fixtures**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~Docker"`
Expected: PASS — `DockerCredentialStoreFixture`, `DockerCredentialProtocolFixture`, the trimmed `DockerCredentialHelperFixture`, and `DockerImagePackageDownloaderCredentialHelperFixture` (the last may require Docker and self-skip in environments without it — note any skips).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Clean up Docker credential helper test fixtures"
```

---

## Task 8: Full solution build and final verification

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build source/Calamari.sln`
Expected: Build succeeded, no warnings-as-errors failures.

- [ ] **Step 2: Confirm no stale references remain**

Run:
```bash
git grep -n "OCTOPUS_CALAMARI_EXECUTABLE\|DeferredLogger\|IWantCustomHandlingOfDeferredLogs\|docker-credential --operation\|DockerCredentialCommand" -- source/ ':!docs/'
```
Expected: no matches (all references removed). Investigate and fix any hits.

- [ ] **Step 3: Commit (if anything was fixed)**

```bash
git add -A
git commit -m "Final cleanup of standalone Docker credential helper refactor"
```
