$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Tools.psm1" -Force

$global:OctopusParameters = New-Object Octostache.VariableDictionary

function Invoke-Test([string]$name) {    
    $file = "$here\Samples\$name"
    Copy-Item "$file" "$file.tmp" -Force
	Invoke-OctopusVariableSubsitution "$file.tmp" | Out-Host
    $out = [System.IO.File]::ReadAllText("$file.tmp")
    Remove-Item "$file.tmp"
    return $out
}

Describe "Tentacle.Substitutions" {
    It "Servers.json" {
        $OctopusParameters["ServerEndpoints[FOREXUAT01].Name"] = "forexuat01.local"
        $OctopusParameters["ServerEndpoints[FOREXUAT01].Port"] = "1566"
        $OctopusParameters["ServerEndpoints[FOREXUAT02].Name"] = "forexuat02.local"
        $OctopusParameters["ServerEndpoints[FOREXUAT02].Port"] = "1566"

		$x = Invoke-Test "Servers.json"

		[System.Text.RegularExpressions.Regex]::Replace($x, "\s+", "") | Should Be "{""Servers"":[{""Name"":""forexuat01.local"",""Port"":1566},{""Name"":""forexuat02.local"",""Port"":1566}]}"
    }
}
