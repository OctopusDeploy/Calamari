using System.Collections.Generic;
using System.Collections.Specialized;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Newtonsoft.Json.Linq;

namespace Calamari.Terraform
{
    public class ApplyTerraformConvention : TerraformConvention
    {
        public ApplyTerraformConvention(ICalamariFileSystem fileSystem, IFileSubstituter fileSubstituter) : base(fileSystem, fileSubstituter)
        {
        }

        protected override void Execute(RunningDeployment deployment, StringDictionary environmentVariables)
        {
            IEnumerable<(string, JToken)> NewFunction(string result)
            {
                var jObj = JObject.Parse(result);

                foreach (var property in jObj.Properties())
                {
                    yield return (property.Name, property.Value);
                }
            }

            using (var cli = new TerraformCLIExecutor(fileSystem, deployment))
            {
                cli.ExecuteCommand($"apply -no-color -input=false -auto-approve {cli.TerraformVariableFiles} {cli.ActionParams}", environmentVariables);

                // Attempt to get the outputs. This will fail if none are defined in versions prior to v0.11.8
                var exitCode = cli.ExecuteCommand("output -no-color -json", environmentVariables, out var result);
                if (exitCode != 0)
                {
                    return;
                }

                foreach (var (name, token) in NewFunction(result))
                {
                    Log.SetOutputVariable($"TerraformJsonOutputs[{name}]", token.ToString());
                    Log.SetOutputVariable($"TerraformValueOutputs[{name}]", token.ToString());
                }
            }
        }
    }
}