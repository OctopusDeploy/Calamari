using System;
using System.Linq;
using System.Security.Cryptography;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Variables
{
    public interface IActionCustomProperties 
    {
    }
    
    public class ArgoCDActionCustomProperties : IActionCustomProperties
    {
        public ArgoApplication[] Applications { get; set; } = Array.Empty<ArgoApplication>();
    }

    
    public class ArgoApplication 
    {
        public string Name { get; set; }
        public ArgoApplicationSource[] Sources { get; set; }
    }

    
    public class ArgoApplicationSource
    {
        public Uri RepoUrl { get; set; }
        public string TargetRevision { get; set; }
        public string Path { get; set; }
        
        public GitCredentials? Credentials { get; set; }
    }

    public class GitCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public interface ICustomPropertiesFactory
    {
        T Create<T>();
    }

    public class CustomPropertiesFactory : ICustomPropertiesFactory
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly CommonOptions options;

        static readonly object LoaderLock = new object();

        public CustomPropertiesFactory(ICalamariFileSystem fileSystem, ILog log, CommonOptions options)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.options = options;
        }

        public T Create<T>()
        {
            lock (LoaderLock)
            {
                return LoadExecutionVariablesFromFile<T>();
            }
        }

        T LoadExecutionVariablesFromFile<T>()
        {
            var sensitiveFilePassword = options.InputVariables.VariablesPassword;
            var json = string.IsNullOrWhiteSpace(sensitiveFilePassword)
                ? fileSystem.ReadFile(options.CustomPropertiesFile)
                : Decrypt(fileSystem.ReadAllBytes(options.CustomPropertiesFile), sensitiveFilePassword);

            try
            {
                return JsonConvert.DeserializeObject<T>(json, SerializerSettings);
            }
            catch (JsonReaderException)
            {
                throw new CommandException("Unable to parse variables as valid JSON.");
            }
        }

        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None
        };

        static string Decrypt(byte[] encryptedVariables, string encryptionPassword)
        {
            try
            {
                return AesEncryption.ForServerVariables(encryptionPassword).Decrypt(encryptedVariables);
            }
            catch (CryptographicException)
            {
                throw new CommandException("Cannot decrypt variables. Check your password is correct.");
            }
        }
    }
}