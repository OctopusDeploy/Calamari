using System;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Octopus.TinyTypes;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class ServerTaskId : CaseInsensitiveStringTinyType
    {
        [JsonConstructor]
        public ServerTaskId(string value) : base(value)
        {
        }

        public static ServerTaskId FromVariables(IVariables variables)
        {
            var taskId = variables.Get(KnownVariables.ServerTask.Id);
            if (taskId == null)
                throw new Exception("ServerTask.Id not set.");
            return new ServerTaskId(taskId);
        }
    }
}