using System;
using System.Collections.Generic;
using Autofac;

namespace Calamari
{
    /// <summary>
    /// This class is only here until Calamari.Contracts is a thing, then it will move to there
    /// </summary>
    public interface ICalamariExtension
    {
        /// <summary>
        /// Return the command names and their corresponding implementation types that are to be registered 
        /// </summary>
        /// <returns></returns>
        Dictionary<string, Type> RegisterCommands();
        
        void Load(ContainerBuilder builder);
    }
}