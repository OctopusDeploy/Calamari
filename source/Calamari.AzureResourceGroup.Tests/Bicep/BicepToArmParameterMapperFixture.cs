using Calamari.AzureResourceGroup.Bicep;
using Calamari.Common.Plumbing.Variables;
using NuGet.Protocol;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests.Bicep;

[TestFixture]
public class BicepToArmParameterMapperFixture
{
  const string SimpleBicepParametersString = """
                                             [{"Key":"storageAccountName","Value":"teststorageaccount"},{"Key":"location","Value":"Australia South East"},{"Key":"myStuff","Value":"[PLACEHOLDER]"}]
                                             """;

  const string SimpleArmTemplate = """
                                   {
                                     "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
                                     "contentVersion": "1.0.0.0",
                                     "metadata": {
                                       "_generator": {
                                         "name": "bicep",
                                         "version": "0.40.2.10011",
                                         "templateHash": "14097892204907684939"
                                       }
                                     },
                                     "parameters": {
                                       "storageAccountName": {
                                         "type": "string"
                                       },
                                       "location": {
                                         "type": "string",
                                         "defaultValue": "[resourceGroup().location]"
                                       },
                                       "myStuff": {
                                         "type": "string"
                                       }
                                     },
                                     "resources": [
                                       {
                                         "type": "Microsoft.Storage/storageAccounts",
                                         "apiVersion": "2021-06-01",
                                         "name": "[parameters('storageAccountName')]",
                                         "location": "[parameters('location')]",
                                         "sku": {
                                           "name": "Standard_LRS"
                                         },
                                         "kind": "StorageV2",
                                         "tags": {
                                           "tagValue": "[parameters('myStuff')]"
                                         },
                                         "properties": {
                                           "accessTier": "Hot"
                                         }
                                       }
                                     ],
                                     "outputs": {
                                       "storageAccountId": {
                                         "type": "string",
                                         "value": "[resourceId('Microsoft.Storage/storageAccounts', parameters('storageAccountName'))]"
                                       }
                                     }
                                   }
                                   """;

  CalamariVariables variables;
  [SetUp]
  public void SetUp()
  {
    variables = new CalamariVariables();

  }
    
    
    [Test]
    public void Map_WithEmptyBicepParameters_ReturnsEmptyString()
    {
        var bicepParametersString = string.Empty;
        
        var result = BicepToArmParameterMapper.Map(bicepParametersString, SimpleArmTemplate, variables);
        
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Map_EmptyArmParameters_ReturnsEmptyString()
    {
      var result = BicepToArmParameterMapper.Map(SimpleBicepParametersString, "{\"noParametersHere\":{\"value\":\"told ya\"}}", variables);
      
      Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Map_WithMatchingParameters_ReturnsParameterString()
    {
      var expectedParameterString = """
                               {
                                 "storageAccountName": {
                                   "value": "teststorageaccount"
                                 },
                                 "location": {
                                   "value": "Australia South East"
                                 },
                                 "myStuff": {
                                   "value": "[PLACEHOLDER]"
                                 }
                               }
                               """.ReplaceLineEndings();
      
      
      
      var result = BicepToArmParameterMapper.Map(SimpleBicepParametersString, SimpleArmTemplate, variables).ReplaceLineEndings();


      // Convert so we can ignore Platform specific line endings.
      Assert.That(result.ToJson(), Is.EqualTo(expectedParameterString.ToJson()));

    }

    [Test]
    public void Map_WithOctopusVariableValueDefined_IsResolvedInParametersString()
    {
      variables.Add("Octopus.TestValue", "banana");
      var parameterStringInput = SimpleBicepParametersString.Replace("[PLACEHOLDER]", "#{ Octopus.TestValue }");
      var expectedParameterString = """
                                    {
                                      "storageAccountName": {
                                        "value": "teststorageaccount"
                                      },
                                      "location": {
                                        "value": "Australia South East"
                                      },
                                      "myStuff": {
                                        "value": "banana"
                                      }
                                    }
                                    """.ReplaceLineEndings();
      
      var result = BicepToArmParameterMapper.Map(parameterStringInput, SimpleArmTemplate, variables).ReplaceLineEndings();
      
      // Convert so we can ignore Platform specific line endings.
      Assert.That(result, Is.EqualTo(expectedParameterString));
    }

    [Test]
    public void Map_WithOctopusVariableDefinedWithNonDelimitedJsonObject_IsResolvedInParametersStringWithProperDelimiting()
    {
      variables.Add("Octopus.TestValue",
                    """
                    {
                      "SomeKey": "WithAValue",
                      "AnotherKey": "WithADifferentValue",
                      "NestedObject": {
                        "NestedObjectKey": "YetAnotherValue"
                      }
                    }
                    """);
      var parameterStringInput = SimpleBicepParametersString.Replace("[PLACEHOLDER]", "#{ Octopus.TestValue }");
      var expectedParameterString = """
                                    {
                                      "storageAccountName": {
                                        "value": "teststorageaccount"
                                      },
                                      "location": {
                                        "value": "Australia South East"
                                      },
                                      "myStuff": {
                                        "value": "{\"SomeKey\":\"WithAValue\",\"AnotherKey\":\"WithADifferentValue\",\"NestedObject\":{\"NestedObjectKey\":\"YetAnotherValue\"}}"
                                      }
                                    }
                                    """.ReplaceLineEndings();
      
      var result = BicepToArmParameterMapper.Map(parameterStringInput, SimpleArmTemplate, variables).ReplaceLineEndings();
      
      // Convert so we can ignore Platform specific line endings.
      Assert.That(result, Is.EqualTo(expectedParameterString));
    }
}
