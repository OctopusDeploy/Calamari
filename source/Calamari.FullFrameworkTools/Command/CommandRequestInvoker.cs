using System;
using System.IO;
using System.Linq;
using Calamari.FullFrameworkTools.Iis;
using Calamari.FullFrameworkTools.Utils;
using Calamari.FullFrameworkTools.WindowsCertStore;
using Newtonsoft.Json;

namespace Calamari.FullFrameworkTools.Command
{

    public interface IRequestTypeLocator
    {
        Type FindType(string command);
    }

    public class RequestTypeLocator: IRequestTypeLocator
    {
        public Type FindType(string command)
        {
            var allRequests = typeof(IRequest).Assembly.GetTypes().Where(t => typeof(IRequest).IsAssignableFrom(t));
            var request = allRequests.FirstOrDefault(n => n.Name.Equals(command));
            if (request is null)
            {
                throw new Exception($"Unable to find command `{command}`");
            }

            return request;
        }
    }
    public class CommandRequestInvoker
    {
        readonly IRequestTypeLocator requestTypeLocator;
        readonly ICommandHandler commandLocator;

        public CommandRequestInvoker(IRequestTypeLocator requestTypeLocator, ICommandHandler commandLocator)
        {
            this.requestTypeLocator = requestTypeLocator;
            this.commandLocator = commandLocator;
        }

        public object Run(string command, string content)
        {
            var requestType = requestTypeLocator.FindType(command);
            var requestObject = (IRequest)JsonConvert.DeserializeObject(content, requestType);
            if (requestObject is null)
            {
                throw new Exception($"Unable to find deserialize request `{command}`");
            }
            
            return commandLocator.Handle(requestObject);
        }

        public object Run(string command, string encryptionPassword, string filePath)
        {
            string rawContent;
            
            try
            {
                var encrypedContent = File.ReadAllBytes(filePath);
                rawContent = new AesEncryption(encryptionPassword).Decrypt(encrypedContent);
            }
            catch (IOException ex)
            {
                throw new CommandException($"Unable to read file: {ex.Message}");
            }

            return Run(command, rawContent);
        }

    }
}