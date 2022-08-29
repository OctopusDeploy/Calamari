using System;
using Calamari.Serialization;
using Newtonsoft.Json;

namespace Calamari.LaunchTools
{
    public interface ILaunchTool
    {
        int Execute(string instructions);
    }

    public abstract class LaunchTool<T> : ILaunchTool where T: class
    {
        public int Execute(string instructions)
        {
            var toolSpecificInstructions = JsonConvert.DeserializeObject<T>(instructions, JsonSerialization.GetDefaultSerializerSettings());

            return ExecuteInternal(toolSpecificInstructions);
        }

        protected abstract int ExecuteInternal(T instructions);
    }
}