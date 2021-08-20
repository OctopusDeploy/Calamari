using System;
using Calamari.Serialization;
using Newtonsoft.Json;

namespace Calamari.Commands
{
    public interface ICommandWithInputs
    {
        void Execute(string inputs);
    }

    public abstract class Command<TInputs> : ICommandWithInputs
    {
        public void Execute(string inputs)
        {
            Execute(JsonConvert.DeserializeObject<TInputs>(inputs, JsonSerialization.GetDefaultSerializerSettings()));
        }

        protected abstract void Execute(TInputs inputs);
    }
}