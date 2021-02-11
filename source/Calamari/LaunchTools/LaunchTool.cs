﻿using System;
using Calamari.Serialization;
using Newtonsoft.Json;

namespace Calamari.LaunchTools
{
    public interface ILaunchTool
    {
        int Execute(string instructions, params string[] args);
    }

    public abstract class LaunchTool<T> : ILaunchTool where T: class
    {
        public int Execute(string instructions, params string[] args)
        {
            var toolSpecificInstructions = JsonConvert.DeserializeObject<T>(instructions, JsonSerialization.GetDefaultSerializerSettings());

            return ExecuteInternal(toolSpecificInstructions, args);
        }

        protected abstract int ExecuteInternal(T instructions, params string[] args);
    }
}