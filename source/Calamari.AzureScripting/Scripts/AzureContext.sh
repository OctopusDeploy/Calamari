#!/bin/bash

Octopus_Azure_Environment=$(get_octopusvariable "Octopus.Action.Azure.Environment")
Octopus_Azure_ADClientId=$(get_octopusvariable "Octopus.Action.Azure.ClientId")
Octopus_Azure_ADPassword=$(get_octopusvariable "Octopus.Action.Azure.Password")
Octopus_Azure_ADTenantId=$(get_octopusvariable "Octopus.Action.Azure.TenantId")
Octopus_Azure_SubscriptionId=$(get_octopusvariable "Octopus.Action.Azure.SubscriptionId")
Octopus_Azure_OctopusUseOidc=$(get_octopusvariable "OctopusUseOidc")
Octopus_Azure_OctopusUseServicePrincipal=$(get_octopusvariable "OctopusUseServicePrincipal")
Octopus_Azure_AccessToken=$(get_octopusvariable "OctopusAzureAccessToken")

function check_app_exists {
	command -v $1 > /dev/null 2>&1
	if [[ $? -ne 0 ]]; then
		echo >&2 "The executable $1 does not exist, or is not on the PATH."
		echo >&2 "See https://g.octopushq.com/TBD for more information."
		exit 1
	fi
}

function setup_context {
    {
        # Config directory is set to make sure that our security is right for the step running it
        # and not using the one in the default config dir to avoid issues with user defined ones
        export AZURE_CONFIG_DIR="$OctopusCalamariWorkingDirectory/azure-cli"
        mkdir -p $AZURE_CONFIG_DIR

        # The azure extensions directory is getting overridden above when we set the azure config dir (undocumented behavior).
        # Set the azure extensions directory to the value of $OctopusAzureExtensionsDirectory if specified,
        # otherwise, back to the default value of $HOME/.azure/cliextension.
        if [ -n "$OctopusAzureExtensionsDirectory" ]
        then
            echo "Setting Azure CLI extensions directory to $OctopusAzureExtensionsDirectory"
            export AZURE_EXTENSION_DIR="$OctopusAzureExtensionsDirectory"
        else
            export AZURE_EXTENSION_DIR="$HOME/.azure/cliextensions"
        fi

        # authenticate with the Azure CLI
        echo "##octopus[stdout-verbose]"

        az cloud set --name ${Octopus_Azure_Environment:-"AzureCloud"} 2>null 3>null

        echo "Azure CLI: Authenticating with Service Principal"
        loginArgs=()
        # Use the full argument because of https://github.com/Azure/azure-cli/issues/12105

        if [ $Octopus_Azure_OctopusUseOidc ]
        then
          loginArgs+=("$Octopus_Azure_AccessToken")
          loginArgs+=("--tenant=$Octopus_Azure_ADTenantId")
          echo az login ${loginArgs[@]}
          az login "${loginArgs[@]}"
        else
          loginArgs+=("--username=$Octopus_Azure_ADClientId")
          loginArgs+=("--password=$Octopus_Azure_ADPassword")
          loginArgs+=("--tenant=$Octopus_Azure_ADTenantId")
          echo az login --service-principal ${loginArgs[@]}
          # Note: Need to double quote the loginArgs here to ensure that spaces aren't treated as separate arguments
          #       It also seems like putting double quotes around each individual argument makes az cli include the "
          #       character as part of the input causing issues...
          az login --service-principal "${loginArgs[@]}"
        fi

        echo "Azure CLI: Setting active subscription to $Octopus_Azure_SubscriptionId"
        az account set --subscription $Octopus_Azure_SubscriptionId

        echo "##octopus[stdout-default]"
        write_verbose "Successfully authenticated with the Azure CLI"
    } || {
        # failed to authenticate with Azure CLI
        echo "Failed to authenticate with Azure CLI"
        exit 1
    }
}

echo "##octopus[stdout-verbose]"
check_app_exists az
setup_context

OctopusAzureTargetScript=$(get_octopusvariable "OctopusAzureTargetScript")
OctopusAzureTargetScriptParameters=$(get_octopusvariable "OctopusAzureTargetScriptParameters")

echo "Invoking target script \"$OctopusAzureTargetScript\" with $OctopusAzureTargetScriptParameters parameters"
echo "##octopus[stdout-default]"

source "$OctopusAzureTargetScript" $OctopusAzureTargetScriptParameters || exit 1 :
{
    az logout 2>null 3>null
}