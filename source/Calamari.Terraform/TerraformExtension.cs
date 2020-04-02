using System;
using System.Collections.Generic;
using Autofac;

namespace Calamari.Terraform
{
    public class TerraformExtension : ICalamariExtension
    {
        readonly Dictionary<string, Type> commandTypes = new Dictionary<string, Type>
        {
            {"plan-terraform", typeof(PlanCommand)},
            {"apply-terraform", typeof(ApplyCommand)},
            {"destroyplan-terraform", typeof(DestroyPlanCommand)},
            {"destroy-terraform", typeof(DestroyCommand)},
        };
        
        public Dictionary<string, Type> RegisterCommands()
        {
            return commandTypes;
        }

        public void Load(ContainerBuilder builder)
        {
        }
    }
}