#if IIS_SUPPORT
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.FullFrameworkTools.Command;
using Calamari.FullFrameworkTools.Utils;
using Calamari.FullFrameworkTools.WindowsCertStore;
using Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Integration.Iis
{
    public interface ILegacyFrameworkInvoker
    {
        TResponse Invoke<TRequest, TResponse>(TRequest cmd);
    }
    public class LegacyFrameworkInvoker: ILegacyFrameworkInvoker
    {
        readonly ICalamariFileSystem fileSystem;

        public LegacyFrameworkInvoker()
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
        }

        public LegacyFrameworkInvoker(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public TResponse Invoke<TRequest, TResponse>(TRequest cmd)
        {
            var path = @"C:\Development\OctopusDeploy\calamari-second\source\Calamari.FullFrameworkTools\bin\Debug\net462\out\Calamari.FullFrameworkTools.exe";

            var json = JsonConvert.SerializeObject(cmd);
            using (var tempDir = TemporaryDirectory.Create())
            {
                var filepath = Path.Combine(tempDir.DirectoryPath, "cmd.json");
                using (var file = new TemporaryFile(filepath))
                {
                    var password = AesEncryption.RandomString(16);;
                    var enc = new AesEncryption(password);
                    var encRequestObj = enc.Encrypt(json);
                    fileSystem.WriteAllBytes(file.FilePath, encRequestObj);

                    var args = new[] { cmd.GetType().Name, "--password", password, "--file", file.FilePath };
                    var taskResult = "";
                    var processResult = SilentProcessRunner.ExecuteCommand(path,
                                                                           string.Join(" ", args),
                                                                           tempDir.DirectoryPath,
                                                                           s =>
                                                                           {
                                                                               var line = JObject.Parse(s);
                                                                               if (!Enum.TryParse<LogLevel>(line["Level"]?.ToString(), out var logLevel))
                                                                               {
                                                                                   Log.Verbose($"Error Parsing Output: #{s}");
                                                                               }
                                                                               switch (logLevel)
                                                                               {
                                                                                   case LogLevel.Verbose:
                                                                                       Log.Verbose(line["Message"]?.ToString() ?? string.Empty);
                                                                                       break;
                                                                                   case LogLevel.Info:
                                                                                       Log.Info(line["Message"]?.ToString() ?? string.Empty);
                                                                                       break;
                                                                                   case LogLevel.Warn:
                                                                                       Log.Warn(line["Message"]?.ToString() ?? string.Empty);
                                                                                       break;
                                                                                   case LogLevel.Error:
                                                                                       Log.Error(line["Message"]?.ToString() ?? string.Empty);
                                                                                       break;
                                                                                   case LogLevel.Fatal:
                                                                                       Log.Error(line["Message"]?.ToString() ?? string.Empty);
                                                                                       Log.Error(line["Type"]?.ToString() ?? string.Empty);
                                                                                       Log.Error(line["StackTrace"]?.ToString() ?? string.Empty);
                                                                                       throw new Exception(line["Message"]?.ToString());
                                                                                   case LogLevel.Result:
                                                                                       taskResult = line["Result"]?.ToString();
                                                                                       break;
                                                                                   default:
                                                                                       throw new ArgumentOutOfRangeException();
                                                                               }
                                                                           },
                                                                           Log.Error);

                    if (processResult.ExitCode != 0)
                    {
                        throw new Exception("Operation failed: "+ processResult.ErrorOutput);
                    }
                    return JsonConvert.DeserializeObject<TResponse>(taskResult);
                }
            }
        }


        enum LogLevel
        {
            Verbose,
            Info,
            Warn,
            Error,
            Fatal, // Used for Exceptions
            Result, // Special Response
        }


        

    }
    
    public class InProcessInvoker: ILegacyFrameworkInvoker
    {
        public TResponse Invoke<TRequest, TResponse>(TRequest cmd)
        {
            var log = new InnerLog();
            var commandLocator = new RequestTypeLocator();
            var commandHandler = new CommandHandler(null, new Calamari.FullFrameworkTools.Iis.InternetInformationServer());
            var requestInvoker = new CommandRequestInvoker(commandLocator, commandHandler);

            var json = JsonConvert.SerializeObject(cmd);

            var result = requestInvoker.Run(nameof(OverwriteHomeDirectoryRequest), json);
            return (TResponse)result;
        }
    }

}
#endif