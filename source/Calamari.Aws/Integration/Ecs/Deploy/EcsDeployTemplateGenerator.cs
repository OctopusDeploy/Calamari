using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Inputs.Ecs;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs.Deploy;

public record GeneratedTemplate(string Body, IReadOnlyList<Parameter> Parameters);

public class EcsDeployTemplateGenerator(DeployEcsCommandInputs commandInputs)
{
    public GeneratedTemplate Generate()
    {
        var parameters = BuildParameters();
        var template = new EcsDeployTemplate(commandInputs, parameters).Build();

        var body = JsonConvert.SerializeObject(template, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        });

        return new GeneratedTemplate(
            body,
            parameters.Select(p => new Parameter { ParameterKey = p.Name, ParameterValue = p.Value }).ToList());
    }

    List<IEcsTemplateParameter> BuildParameters()
    {
        var list = new List<IEcsTemplateParameter>
        {
            EcsTemplateParameter.Of(EcsTemplateParameterNames.ClusterName,          commandInputs.ClusterName),
            EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskDefinitionName,   commandInputs.ServiceTaskName),
            EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskDefinitionCpu,    commandInputs.Cpu),
            EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskDefinitionMemory, commandInputs.Memory),
        };

        // The remaining parameters are only registered when the user-supplied value
        // differs from the default — matching SPF, which keeps the template lean
        // when defaults are in use and parameterises only what's been customised.

        if (!string.IsNullOrEmpty(commandInputs.TaskRole))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskRole, commandInputs.TaskRole));
        }

        if (!string.IsNullOrEmpty(commandInputs.TaskExecutionRole))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskExecutionRole, commandInputs.TaskExecutionRole));
        }

        if (DiffersFromDefault(commandInputs.DesiredCount, EcsInputDefaults.DesiredCount))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.DesiredCount, commandInputs.DesiredCount));
        }

        if (DiffersFromDefault(commandInputs.MinimumHealthyPercentage, EcsInputDefaults.MinimumHealthPercent))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.MinimumHealthPercent, commandInputs.MinimumHealthyPercentage));
        }

        if (DiffersFromDefault(commandInputs.MaximumHealthyPercentage, EcsInputDefaults.MaximumHealthPercent))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.MaximumHealthPercent, commandInputs.MaximumHealthyPercentage));
        }

        if (commandInputs.RequiresLogGroup)
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.LogGroupName, commandInputs.DefaultLogGroupPath));
        }

        return list;
    }

    // Epsilon-based double comparison — direct != is unreliable across precision and NaN.
    static bool DiffersFromDefault(double value, double @default) =>
        Math.Abs(value - @default) > double.Epsilon;
}
