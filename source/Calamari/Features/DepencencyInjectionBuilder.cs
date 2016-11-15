using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Features.Conventions;

namespace Calamari.Features
{
    internal class DepencencyInjectionBuilder
    {
        private readonly CalamariCalamariContainer knownTypes;

        public DepencencyInjectionBuilder(CalamariCalamariContainer knownTypes)
        {
            this.knownTypes = knownTypes;
        }

        public object BuildConvention(Type type, object[] ctorArgs)
        {
            
            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            
            if (!constructors.Any())
            {
               return Activator.CreateInstance(type);
            }

            if (constructors.Count() > 1)
            {
                throw new Exception(
                    $"Convention {type.Name} has more than one constructor. If there are more than one constructors, please specify using attribute.");

            }
            
            var ctorParams = constructors[0].GetParameters();

            var idx = 0;
            var ctorArgs2 = ctorParams.Select(p => KnownTypesRegistration(type, ctorArgs, p, ref idx)).ToArray();
            if (ctorArgs.Length > idx)
            {
                var expectedArgs = string.Join(", ", ctorParams.Select(p => p.ParameterType +" "+ p.Name));
                var actual = string.Join(", ", ctorArgs.Select(p => p.GetType().Name));
                throw new Exception(
                    $"Some constuctor arguments provided for {type.Name}({expectedArgs}) were unable to be mapped to constructor parameters. Provided: ({actual})");
            }
            return Activator.CreateInstance(type, ctorArgs2);
        }

        private object KnownTypesRegistration(Type type, object[] ctorArgs, ParameterInfo p, ref int idx)
        {
            if (ctorArgs.Length > idx)
            {
                if (ctorArgs[idx] == null)
                {
                    //if (((Type)p.ParameterType).GetTypeInfo().IsValueType)
                    {
                      //  throw new Exception($"Unable to pass null value to {type.Name} for value type parameter {p.Name}");
                    }
                    return ctorArgs[idx++];
                }

                if (ctorArgs[idx].GetType().IsAssignableFrom(p.ParameterType))
                {
                    return ctorArgs[idx++];
                }
            }

            if (knownTypes.Registrations.ContainsKey(p.ParameterType))
            {
                return knownTypes.Registrations[p.ParameterType];
            }
            throw new Exception(
                $"Parameter {p.Name} on constructor for {type.Name} did not match any known or provided argument types");
        }
    }
}
