using System;
using System.Threading.Tasks;
using Calamari.Aws.Integration.Ecs;
using Calamari.Aws.Kubernetes.Discovery;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Deployment;
using Newtonsoft.Json;

namespace Calamari.Aws.Behaviours;

public class EcsClusterDiscoveryBehaviour(ILog log) : IDeployBehaviour
{
    public bool IsEnabled(RunningDeployment context) => true;


    public async Task Execute(RunningDeployment context)
    {
        var targetDiscoveryContextVariable = context.Variables.Get(SpecialVariables.TargetDiscoveryContext);
        if (string.IsNullOrEmpty(targetDiscoveryContextVariable))
        {
            log.Warn($"Could not find target discovery context in variable {SpecialVariables.TargetDiscoveryContext}.");
            log.Warn("Aborting target discovery.");
            return;
        }

        if (!TryGetAuthenticationMethod(targetDiscoveryContextVariable!, out string authenticationMethod))
        {
            return;
        }

        var targetDiscoveryContext = authenticationMethod == "account"
            ? GetTargetDiscoveryContext<AwsAccessKeyCredentials>(targetDiscoveryContextVariable)
            : GetTargetDiscoveryContext<AwsOidcCredentials>(targetDiscoveryContextVariable);

        // AwsCredentialsBase
        var environment = await AwsEnvironmentGeneration.Create(log, context.Variables);
        // environment.AwsCredentials
        var ecsClient = EcsClientFactory.Create(environment);
        
        // ecsClient.ListClustersAsync()



        await Task.Delay(1);
        
        return;
    }

     static bool TryGetAuthenticationMethod(string targetDiscoveryJson, out string authenticationMethod)
     {
         try
         {
             var targetDiscoveryContext = JsonConvert.DeserializeObject<TargetDiscoveryContext<AccountAuthenticationDetails<dynamic>>>(targetDiscoveryJson);
             authenticationMethod = targetDiscoveryContext?.Authentication?.AuthenticationMethod ?? throw new Exception("AuthenticationMethod is null");
             return true;
         }
         catch (JsonException ex)
         {
             Log.Warn($"Could not read authentication method from target discovery context, {SpecialVariables.TargetDiscoveryContext} is in wrong format, {ex.Message}");
             Log.Warn("Aborting target discovery.");
             authenticationMethod = null;
             return false;
         }
     }

     TargetDiscoveryContext<AccountAuthenticationDetails<AwsCredentialsBase>> GetTargetDiscoveryContext<T>(
         string json) where T : AwsCredentialsBase
     {
         try
         {
             var context = JsonConvert
                 .DeserializeObject<TargetDiscoveryContext<AccountAuthenticationDetails<T>>>(json);
             if (context?.Authentication != null)
             {
                 throw new NotImplementedException();
             }
         }
         catch (JsonException ex)
         {
             Log.Warn($"Target discovery context from variable {SpecialVariables.TargetDiscoveryContext} is in wrong format: {ex.Message}");
             return null;
         }
         return null;
     }
}