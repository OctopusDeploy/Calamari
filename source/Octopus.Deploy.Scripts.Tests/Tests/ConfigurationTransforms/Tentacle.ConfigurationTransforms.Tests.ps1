$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Tools.psm1" -Force

$global:OctopusParameters = New-Object Octostache.VariableDictionary

function Invoke-Test([string]$config, [string]$transform) {
    $config = "$here\Samples\$config"
    $transform = "$here\Samples\$transform"
    Copy-Item "$config" "$config.tmp" -Force
	Invoke-OctopusConfigurationTransform -config "$config.tmp" -transform "$transform" | Out-Host
    [xml]$out = Get-Content "$config.tmp"
    Remove-Item "$config.tmp"
    return $out
}

function Invoke-TestCapturingOutput([string]$config, [string]$transform) {
    $config = "$here\Samples\$config"
    $transform = "$here\Samples\$transform"
    Copy-Item "$config" "$config.tmp" -Force
	$out = (Invoke-OctopusConfigurationTransform -config "$config.tmp" -transform "$transform" | Out-String)
    Remove-Item "$config.tmp"
    return $out
}

Describe "Tentacle.ConfigurationTransforms" {
    It "Web.Release.config => Web.config" {
		$x = Invoke-Test -config "Web.config" -transform "Web.Release.config"

        (Select-XML $x -xpath "//*[local-name()='appSettings']/*[@key='WelcomeMessage']/@value") | Should Be "Release!"
        (Select-XML $x -xpath "//*[local-name()='compilation']/@debug") | Should Be $Null
    }
	
    It "Web.Buggy.config => Web.config" {
		$out = (Invoke-TestCapturingOutput -config "Web.config" -transform "Web.Buggy.config" | Out-String)

		$out | Should Match ([regex]::Escape("stdout-warning"))
    }
	
    It "Web.Broken.config => Web.config" {
		{ Invoke-TestCapturingOutput -config "Web.config" -transform "Web.Broken.config" -ErrorAction Continue } | Should Throw
    }
}
