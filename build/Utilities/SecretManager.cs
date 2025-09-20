using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octopus.OnePassword.Sdk;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Calamari.Build.Utilities;

public static class SecretManager
{
    static readonly ILogger Logger = Log.ForContext(typeof(SecretManager));

    static readonly bool SecretManagerIsEnabled = Convert.ToBoolean(Environment.GetEnvironmentVariable("OCTOPUS__Tests__SecretManagerEnabled") ?? "True");
    static readonly string SecretManagerAccount = Environment.GetEnvironmentVariable("OCTOPUS__Tests__SecretManagerAccount") ?? "octopusdeploy.1password.com";

    static readonly Lazy<SecretManagerClient> SecretManagerClient = new(LoadSecretManagerClient);

    static SecretManagerClient LoadSecretManagerClient()
    {
        var loggerFactory = new LoggerFactory().AddSerilog();
        var microsoftLogger = loggerFactory.CreateLogger<SecretManagerClient>();
        return new SecretManagerClient(SecretManagerAccount, Array.Empty<string>(), microsoftLogger);
    }

    public static string? GetValue(ParameterFromPasswordStore attr, CancellationToken cancellationToken = default) 
        => GetValueAsync(attr, cancellationToken).GetAwaiter().GetResult();

    public static async Task<string?> GetValueAsync(ParameterFromPasswordStore attr, CancellationToken cancellationToken = default)
    {
        if (!SecretManagerIsEnabled) return null;

        var valueFromSecretManager = string.IsNullOrEmpty(attr.SecretReference)
            ? null
            : await SecretManagerClient.Value.GetSecret(attr.SecretReference, cancellationToken, throwOnNotFound: false);
        if (string.IsNullOrEmpty(valueFromSecretManager)) return null;

        Logger.Information("Parameter with name {Name} was read from the secret manager", attr.Name);
        return valueFromSecretManager;
    }
}
