using System;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Resources.Models;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests;

[TestFixture]
public class AzureResourceGroupOperatorFixture
{
    [Test]
    public void ExtractAzureErrorInfo_CompilesErrorMessage()
    {
        var statusMessage = StatusMessageWith("StorageAccountAlreadyTaken", "The storage account named testsa is already taken.");

        var result = AzureResourceGroupOperator.ExtractAzureErrorInfo(statusMessage);

        Assert.That(result, Is.EqualTo("[StorageAccountAlreadyTaken] The storage account named testsa is already taken."));
    }

    [Test]
    public void ExtractAzureErrorInfo_NoError_IsEmpty()
    {
        var statusMessage = ArmResourcesModelFactory.StatusMessage("Failed", null);

        var result = AzureResourceGroupOperator.ExtractAzureErrorInfo(statusMessage);

        Assert.That(result, Is.Empty);
    }

    [TestCase("Failed", "[FAILED]")]
    [TestCase("Canceled", "[CANCELED]")]
    [TestCase("failed", "[FAILED]")]
    public void FormatFailedOperation_UsesUppercasedProvisioningStateAsTag(string provisioningState, string expectedTag)
    {
        var properties = OperationProperties(provisioningState,
            TargetResource("Microsoft.Storage/storageAccounts", "testsa"));

        var result = AzureResourceGroupOperator.FormatFailedOperation(properties);

        Assert.That(result, Does.Contain($"{expectedTag} Microsoft.Storage/storageAccounts 'testsa'"));
    }

    [Test]
    public void FormatFailedOperation_NoProvisioningState_FallsBackToFailedTag()
    {
        var properties = OperationProperties(null, TargetResource("Microsoft.Storage/storageAccounts", "testsa"));

        var result = AzureResourceGroupOperator.FormatFailedOperation(properties);

        Assert.That(result, Does.Contain("[FAILED]"));
    }

    [Test]
    public void FormatFailedOperation_NoTargetResource_UsesUnknownPlaceholders()
    {
        var properties = OperationProperties("Failed", targetResource: null);

        var result = AzureResourceGroupOperator.FormatFailedOperation(properties);

        Assert.That(result, Does.Contain("Unknown Type 'Unknown Resource'"));
    }

    [Test]
    public void FormatFailedOperation_TargetResourceWithoutResourceType_UsesUnknownTypePlaceholder()
    {
        var targetResource = ArmResourcesModelFactory.TargetResource(id: null, resourceName: "testsa", resourceType: null);
        var properties = OperationProperties("Failed", targetResource);

        var result = AzureResourceGroupOperator.FormatFailedOperation(properties);

        Assert.That(result, Does.Contain("Unknown Type 'testsa'"));
    }

    static StatusMessage StatusMessageWith(string code, string message)
        => ArmResourcesModelFactory.StatusMessage("Failed", new ResponseError(code, message));

    static TargetResource TargetResource(string resourceType, string resourceName)
        => ArmResourcesModelFactory.TargetResource(id: null, resourceName: resourceName, resourceType: new ResourceType(resourceType));

    static ArmDeploymentOperationProperties OperationProperties(string provisioningState,
                                                                TargetResource targetResource,
                                                                StatusMessage statusMessage = null,
                                                                DateTimeOffset? timestamp = null)
        => ArmResourcesModelFactory.ArmDeploymentOperationProperties(provisioningOperation: null,
            provisioningState: provisioningState,
            timestamp: timestamp,
            duration: null,
            serviceRequestId: null,
            statusCode: null,
            statusMessage: statusMessage,
            targetResource: targetResource,
            requestContent: null,
            responseContent: null);
}