using System.Collections.Generic;
using System.Linq;
using Autofac;
using Autofac.Builder;
using Autofac.Features.Metadata;
using Calamari.Common.Util;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class ContainerBuilderServicePrioritisationExtensions
    {
        const string RegistrationPriorityMetadataKey = "RegistrationPriority";
        
        public static void WithPriority<TLimit, TActivatorData, TRegistrationStyle>(this IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> target, int priority)
        {
            target.WithMetadata(PriorityMetadata, priority);
        }

        public static void RegisterPrioritisedList<T>(this ContainerBuilder builder)
        {
            builder.Register(c =>
                             {
                                 var registeredServices = c.Resolve<IEnumerable<Meta<T>>>().ToList();
                
                                 // Start by getting the core list of prioritised registrations
                                 var prioritisedServices = registeredServices
                                                           .Where(t => t.Metadata.ContainsKey(RegistrationPriorityMetadataKey))
                                                           .OrderBy(t => t.Metadata[RegistrationPriorityMetadataKey])
                                                           .Select(t => t.Value);
                                 
                                 // Also grab any extras that had no priority metadata 
                                 var unprioritisedServices = registeredServices
                                                             .Where(t => t.Metadata.ContainsKey(RegistrationPriorityMetadataKey) != true)
                                                             .Select(t => t.Value);
                
                                 // Return the prioritised services first, followed by the unprioritised ones in any order
                                 return new PrioritisedList<T>(prioritisedServices.Union(unprioritisedServices));
                             });
        }
    }
    
    /// <summary>
    /// Helper class to enable the DI container to provide a list of services where order is important,
    /// supported by registration of specific metadata using <see cref="ContainerBuilderServicePrioritisationExtensions"/>
    /// </summary>
    /// <typeparam name="T">Service requiring prioritisation</typeparam>
    public class PrioritisedList<T> : List<T>
    {
        public PrioritisedList()
        { }
        
        public PrioritisedList(IEnumerable<T> collection)
            : base(collection)
        { }
    }
}
