using System;
using Calamari.Serialization;
using Newtonsoft.Json;

namespace Calamari.Commands
{
    public interface ICommandWithInputs
    {
        int Execute(string inputs);
    }

    public abstract class Command<TInputs> : ICommandWithInputs
    {
        public int Execute(string inputs)
        {
            return Execute(JsonConvert.DeserializeObject<TInputs>(inputs, JsonSerialization.GetDefaultSerializerSettings()));
        }

        protected abstract int Execute(TInputs inputs);
    }
}