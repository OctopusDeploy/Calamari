using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Calamari.Commands.Support;

namespace Calamari.Modules
{
    interface ICommandLocator
    {
        /// <summary>
        /// Here we find the one Command class that we want to run
        /// </summary>
        /// <returns>The named command class, or null if none exist</returns>
        Type Find(string name, Assembly assembly);
        /// <summary>
        /// Get all the command attributes
        /// </summary>
        /// <returns>All the Command Attributes in the given assembly</returns>
        ICommandMetadata[] List(Assembly assembly);
        /// <summary>
        /// Get a named service from autofac, or null if it doesn't exist
        /// </summary>
        /// <param name="ctx">The autofac context</param>
        /// <param name="named">The name of the ICommand service</param>
        /// <returns></returns>
        ICommand GetOptionalNamedCommand(IComponentContext ctx, string named);
    }
}
