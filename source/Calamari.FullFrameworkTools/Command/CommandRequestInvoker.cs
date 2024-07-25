using System;
using System.IO;
using System.Text.Json;
using Calamari.FullFrameworkTools.Iis;

namespace Calamari.FullFrameworkTools.Command
{
    public class CommandRequestInvoker
    {
        readonly ICommandLocator commandLocator;

        public CommandRequestInvoker(ICommandLocator commandLocator)
        {
            this.commandLocator = commandLocator;
        }

        public object Run(string command, string content)
        {
            var commandHandler = commandLocator.GetCommand(command);
            if (commandHandler == null)
            {
                throw new CommandException($"Unknown command {command}");
            }

            var requestType = commandHandler.GetType().BaseType.GetGenericArguments()[0]; //Probably proper type checking
            var requestObject = JsonSerializer.Deserialize(content, requestType, new JsonSerializerOptions());
            return commandHandler.Handle(requestObject);
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