using System;
using System.Collections.Generic;
using Calamari.LaunchTools;
using Calamari.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Tests.Fixtures.Manifest
{
    public class InstructionBuilder
    {
        readonly List<Instruction> instructions = new List<Instruction>();

        InstructionBuilder()
        {
        }

        public static InstructionBuilder Create()
        {
            return new InstructionBuilder();
        }

        public InstructionBuilder WithCalamariInstruction(string commandName, object inputs)
        {
            instructions.Add(
                             new Instruction
                             {
                                 Launcher = Calamari.LaunchTools.LaunchTools.Calamari,
                                 LauncherInstructions = JsonConvert.SerializeObject(new CalamariInstructions
                                                                                    {
                                                                                        Command = commandName,
                                                                                        Inputs = JObject.FromObject(inputs)
                                                                                    },
                                                                                    JsonSerialization.GetDefaultSerializerSettings())
                             });

            return this;
        }

        public InstructionBuilder WithNodeInstruction()
        {
            instructions.Add(new Instruction
            {
                Launcher = Calamari.LaunchTools.LaunchTools.Node,
                LauncherInstructions = JsonConvert.SerializeObject(new NodeInstructions
                                                                   {
                                                                       BootstrapperPathVariable = nameof(NodeInstructions.BootstrapperPathVariable),
                                                                       NodePathVariable = nameof(NodeInstructions.NodePathVariable),
                                                                       TargetPathVariable = nameof(NodeInstructions.TargetPathVariable),
                                                                       InputsVariable = nameof(NodeInstructions.InputsVariable),
                                                                       DeploymentTargetInputsVariable = nameof(NodeInstructions.DeploymentTargetInputsVariable),
                                                                       BootstrapperInvocationCommand = "Execute"
                                                                   },
                                                                   JsonSerialization.GetDefaultSerializerSettings())
            });

            return this;
        }

        public string AsString()
        {
            return JsonConvert.SerializeObject(instructions, JsonSerialization.GetDefaultSerializerSettings());
        }
    }
}