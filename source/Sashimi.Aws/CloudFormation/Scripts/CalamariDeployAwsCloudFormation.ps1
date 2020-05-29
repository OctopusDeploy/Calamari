param(
    [string]$variablePassword=""
)

$PreconditionFailed=0

{{PreconditionCheck}}

if ($PreconditionFailed -ne 0) {
    Exit 1
}

# Added to support lack of passwords in 3.0 Tentacle
if($variablePassword -ne ""){
    $variablePassword="-sensitiveVariablesPassword="+ $variablePassword
}

# The template can either be contained in a package or stand-alone
If ([string]::IsNullOrEmpty("{{PackageFile}}") -eq $false) {
    # There may be no template parameters file
    If ([string]::IsNullOrEmpty("{{TemplateParametersFile}}") -eq $false) {
        & "${env:TentacleHome}\Calamari\{{CalamariPath}}{{CalamariVersion}}\{{CalamariExecutable}}" deploy-aws-cloudformation -package "{{PackageFile}}" -template "{{TemplateFile}}" -templateParameters "{{TemplateParametersFile}}" -waitForCompletion "{{WaitForCompletion}}" -stackName "{{StackName}}" -action "{{Action}}" -iamCapabilities "{{IamCapabilities}}" -disableRollback "{{DisableRollback}}" {{VariablesArgument}}    
    } Else {
        & "${env:TentacleHome}\Calamari\{{CalamariPath}}{{CalamariVersion}}\{{CalamariExecutable}}" deploy-aws-cloudformation -package "{{PackageFile}}" -template "{{TemplateFile}}" -waitForCompletion "{{WaitForCompletion}}" -stackName "{{StackName}}" -action "{{Action}}" -iamCapabilities "{{IamCapabilities}}" -disableRollback "{{DisableRollback}}" {{VariablesArgument}}
    }
     
} Else {
    & "${env:TentacleHome}\Calamari\{{CalamariPath}}{{CalamariVersion}}\{{CalamariExecutable}}" deploy-aws-cloudformation -template "{{TemplateFile}}" -templateParameters "{{TemplateParametersFile}}" -waitForCompletion "{{WaitForCompletion}}" -stackName "{{StackName}}" -action "{{Action}}" -iamCapabilities "{{IamCapabilities}}" -disableRollback "{{DisableRollback}}" {{VariablesArgument}}
}

if ((Test-Path variable:global:LastExitCode))
{
    Exit $LastExitCode
}
