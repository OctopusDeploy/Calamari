$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Tools.psm1" -Force

$global:OctopusParameters = @{}

function Invoke-Test([string]$name) {    
    $file = "$here\Samples\$name"
    Copy-Item "$file" "$file.tmp" -Force
	Update-OctopusApplicationConfigurationFile "$file.tmp" | Out-Host
    [xml]$out = Get-Content "$file.tmp"
    Remove-Item "$file.tmp"
    return $out
}

Describe "Tentacle.ConfigurationVariables" {
    It "App.config" {
        $OctopusParameters["WelcomeMessage"] = "I for one welcome our new PowerShell overlords" 

		$x = Invoke-Test "App.config"

        (Select-XML $x -xpath "//*[local-name()='appSettings']/*[@key='WelcomeMessage']/@value") | Should Be "I for one welcome our new PowerShell overlords"
    }
}
