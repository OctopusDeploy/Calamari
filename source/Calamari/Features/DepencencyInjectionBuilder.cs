using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Features.Conventions;

namespace Calamari.Features
{
    internal class DepencencyInjectionBuilder
    {
        private readonly CalamariContainer knownTypes;

        public DepencencyInjectionBuilder(CalamariContainer knownTypes)
        {
            this.knownTypes = knownTypes;
        }

        public object Build(Type type)
        {

            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);


            if (!constructors.Any())
            {
                return Activator.CreateInstance(type);
            }

            if (constructors.Count() > 1)
            {
                throw new InvalidOperationException($"Convention {type.Name} has more than one constructor. If there are more than one constructors, please specify using attribute.");

            }

            var ctorParams = constructors[0].GetParameters();


            var ctorArgs2 = ctorParams.Select(p =>
            {
                if (knownTypes.Registrations.ContainsKey(p.ParameterType))
                {
                    return knownTypes.Registrations[p.ParameterType];
                }
                throw new InvalidOperationException(
                    $"Parameter `{p.Name}` on constructor for {type.Name} did not match any known or provided argument types.");
            }).ToArray();

            return Activator.CreateInstance(type, ctorArgs2);
        }
    }
}
