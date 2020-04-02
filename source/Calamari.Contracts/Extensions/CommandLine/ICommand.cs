using System;
using System.Collections.Generic;
using System.IO;

namespace Calamari.Commands.Support
{
    /// <summary>
    /// A command that requires the command line arguments passed to it. We are transitioning away from this interface  
    /// </summary>
    public interface ICommand
    {
        IOptionSet Options { get; set; }
        int Execute(string[] commandLineArguments);
    }
    
    public interface IOptionSet
    {
        IOptionSet Add(string prototype, string description, Action<string> action);
        IOptionSet Add<T>(string prototype, string description, Action<T> action);
        List<string> Parse(IEnumerable<string> arguments);
    }
}