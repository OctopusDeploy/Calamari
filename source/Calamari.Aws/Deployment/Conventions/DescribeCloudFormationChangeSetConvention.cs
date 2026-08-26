using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Aws.Deployment.Conventions;

public class DescribeCloudFormationChangeSetConvention(
    Func<IAmazonCloudFormation> clientFactory,
    StackEventLogger stackEventLogger,
    Func<RunningDeployment, StackArn> stackProvider,
    Func<RunningDeployment, ChangeSetArn> changeSetProvider,
    ILog log)
    : CloudFormationInstallationConventionBase(stackEventLogger, log)
{
    public override void Install(RunningDeployment deployment)
    {
        InstallAsync(deployment).GetAwaiter().GetResult();
    }

    Task InstallAsync(RunningDeployment deployment)
    {
        var stack = stackProvider(deployment);
        var changeSet = changeSetProvider(deployment);

        return WithAmazonServiceExceptionHandling(async () => 
                                                      await DescribeChangeset(stack, changeSet, deployment.Variables)
                                                 );
    }
        
    //Repeated from the SDK, where this set is only exposed as an instance property on Amazon.Runtime.RetryPolicy
    static readonly HashSet<string> ThrottlingErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Throttling",
        "ThrottlingException",
        "ThrottledException",
        "RequestThrottled",
        "RequestThrottledException",
        "TooManyRequestsException",
        "ProvisionedThroughputExceededException",
        "RequestLimitExceeded",
        "BandwidthLimitExceeded",
        "SlowDown",
        "PriorRequestNotComplete",
        "EC2ThrottledException"
    };

    static bool IsThrottling(AmazonCloudFormationException ex)
        => (ex.ErrorCode != null && ThrottlingErrorCodes.Contains(ex.ErrorCode))
           || ex.StatusCode == HttpStatusCode.TooManyRequests;

    public async Task DescribeChangeset(StackArn stack, ChangeSetArn changeSet, IVariables variables)
    {
        Guard.NotNull(stack, "The provided stack identifier or name may not be null");
        Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");
        Guard.NotNull(variables, "The variable dictionary may not be null");

        try
        {
            var response = await clientFactory.DescribeChangeSetAsync(stack, changeSet);
            var changes = response?.Changes ?? new List<Change>();
            SetOutputVariable(variables, "ChangeCount", changes.Count.ToString());
            SetOutputVariable(variables, "Changes", JsonConvert.SerializeObject(changes, Formatting.Indented));
        }
        catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
        {
            throw new PermissionException(
                                          "The AWS account used to perform the operation does not have the required permissions to describe the change set.\n" +
                                          "Please ensure the current account has permission to perform action 'cloudformation:DescribeChangeSet'." +
                                          ex.Message + "\n");
        }
        catch (AmazonCloudFormationException ex) when (IsThrottling(ex))
        {
            throw new RateExceededException(
                                            "AWS throttled the request to describe the CloudFormation change set. This is usually transient - try the deployment again.\n" +
                                            ex.Message + "\n");
        }
        catch (AmazonCloudFormationException ex)
        {
            throw new UnknownException("An unrecognized exception was thrown while describing the CloudFormation change set.", ex);
        }
    }
}