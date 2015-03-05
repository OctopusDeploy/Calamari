$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force
Import-Module "$here\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Scripting.psm1" -Force

$global:OctopusParameters = @{}
$OctopusParameters["Octopus.Scripting.PathToScriptCS"] = Resolve-Path "$here\..\..\packages\Octopus.Dependencies.ScriptCS.3.0.1\runtime\scriptcs.exe"

function Invoke-TestScript([string]$name) {
    $variablesFile = "$here\Variables.vars"
    $env:OctopusVariablesFile = $variablesFile
	Write-OctopusVariables -variables $OctopusParameters -variablesFile $variablesFile
	$global:OctopusParameters = Read-OctopusVariables -variablesFile $variablesFile
    $out = (Invoke-OctopusScript $name | Out-String)
    Remove-Item $variablesFile
    return $out
}

Describe "Tentacle.Scripting" {
    It "Invalid.ps1" {
        { Invoke-TestScript "$here\Scripts\Invalid.ps1" } | Should Throw
    }

    It "InvalidSyntax.ps1" {
        { Invoke-TestScript "$here\Scripts\InvalidSyntax.ps1" } | Should Throw
    }

    It "CanSetVariable.ps1" {
        $out = Invoke-TestScript "$here\Scripts\CanSetVariable.ps1"
        $out | Should Match ([regex]::Escape("##octopus[setVariable name='VGVzdEE=' value='V29ybGQh']"))
        $out | Should Match ([regex]::Escape("##octopus[setVariable name='VGhpc0lzQVZlcnlMb25nVmFyaWFibGVOYW1lV2l0aE1vcmVUaGFuMzNDaGFyYWN0ZXJzSVRoaW5rVGhpc0lzQVZlcnlMb25nVmFyaWFibGVOYW1lV2l0aE1vcmVUaGFuMzNDaGFyYWN0ZXJzSVRoaW5rVGhpc0lzQVZlcnlMb25nVmFyaWFibGVOYW1lV2l0aE1vcmVUaGFuMzNDaGFyYWN0ZXJzSVRoaW5rVGhpc0lzQVZlcnlMb25nVmFyaWFibGVOYW1lV2l0aE1vcmVUaGFuMzNDaGFyYWN0ZXJzSVRoaW5r' value='V29ybGQh']"))
        $out | Should Match ([regex]::Escape("##octopus[setVariable name='VGVzdEI=' value='VGhpcyBpcyBhIHJlYWxseSByZWFsbHkgcmVhbGx5IHJlYWxseSByZWFsbHkgcmVhbGx5IHJlYWxseSByZWFsbHkgcmVhbGx5IHJlYWxseSByZWFsbHkgcmVhbGx5IHJlYWxseSByZWFsbHkgcmVhbGx5IHJlYWxseSByZWFsbHkgcmVhbGx5IHJlYWxseSByZWFsbHkgcmVhbGx5IHJlYWxseSByZWFsbHkgbG9uZyBzdHJpbmch']"))
        $out | Should Match ([regex]::Escape("##octopus[setVariable name='VGVzdEM=' value='SGVsbG8/IUBDVykqRkAhKCMqRERPTERTS0M8Pic=']"))
    }
	
    It "PrintVariables.ps1" {
        $OctopusParameters["Variable1"] = "ABC" 
        $OctopusParameters["Variable2"] = "DEF" 
        $OctopusParameters["Variable3"] = "GHI" 
        $OctopusParameters["Foo_bar"] = "Hello" 
        $OctopusParameters["Host"] = "Never" 

        $out = Invoke-TestScript "$here\Scripts\PrintVariables.ps1"
        $out | Should Match ([regex]::Escape("V1= ABC"))
        $out | Should Match ([regex]::Escape("V2= DEF"))
        $out | Should Match ([regex]::Escape("V3= GHI"))
        $out | Should Match ([regex]::Escape("FooBar= Hello"))		# Legacy - '_' used to be removed
        $out | Should Match ([regex]::Escape("Foo_Bar= Hello"))		# Current - '_' is valid in PowerShell
        $out | Should Not Match ([regex]::Escape("H= Never"))	    # Host is built-in in PowerShell
    }

    It "CanCreateArtifact.ps1" {
        $out = Invoke-TestScript "$here\Scripts\CanCreateArtifact.ps1"
        $out | Should Match ([regex]::Escape("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=']"))
    }

	It "CanDotSource.ps1" {
        $out = Invoke-TestScript "$here\Scripts\CanDotSource.ps1"
        $out | Should Be "Hello!`r`n"
    }

	It "Ping.ps1" {
        $out = Invoke-TestScript "$here\Scripts\Ping.ps1"
        $out | Should Match ([regex]::Escape("hello`r`n`r`nPinging "))
    }

    It "Hello.csx" {
        $OctopusParameters["Name"] = "paul!" 
        $out = Invoke-TestScript "$here\Scripts\Hello.csx"
        $out | Should Match "Hello paul!"
    }

    It "CanSetVariable.csx" {
        $out = Invoke-TestScript "$here\Scripts\CanSetVariable.csx"
        $out | Should Match ([regex]::Escape("##octopus[setVariable name='V2VhdGhlcg==' value='U3Vubnkh']"))
    }
	
    It "CanCreateArtifact.csx" {
        $out = Invoke-TestScript "$here\Scripts\CanCreateArtifact.csx"
        $out | Should Match ([regex]::Escape("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' originalFilename='RmlsZS50eHQ=']"))
    }
}
