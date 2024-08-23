/*using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Calamari.FullFrameworkTools.Iis;

namespace Calamari.FullFrameworkTools.Command
{
    public interface ICommandLocator
    {
        IFullFrameworkToolCommandHandler GetCommand(string name);
    }
    
    public class CommandLocator: ICommandLocator
    {
        public IList<IFullFrameworkToolCommandHandler> Commands = new List<IFullFrameworkToolCommandHandler>()
        {
            new ImportCertificateToStoreHandler(),
            new OverwriteHomeDirectoryHandler()
        };

        public IFullFrameworkToolCommandHandler GetCommand(string name)
        {
            return Commands.FirstOrDefault(t => t.GetType().Name.Equals($"{name}Handler"));
        }
        
        
        public IFullFrameworkToolCommandHandler GetCommand<THandler>()
        {
            return Commands.FirstOrDefault(t => GetAllTypes(t.GetType()).Contains(typeof(THandler)));
        }

        public IEnumerable<Type> GetAllTypes(Type type)
        {
            yield return type;
            
            if (type.BaseType == null)
                yield break;
            
            yield return type.BaseType;
            foreach (var b in GetAllTypes(type.BaseType))
            {
                yield return b;
            }
        }
    }
}*/