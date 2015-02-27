$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force
Import-Module "$here\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Scripting.psm1" -Force

$global:OctopusParameters = @{}

Describe "Tentacle.Scripting" {    
    $variablesFile = "$here\Variables.vars"

    $OctopusParameters["Octopus.Scripting.PathToScriptCS"] = Resolve-Path "$here\..\..\packages\Octopus.Dependencies.ScriptCS.3.0.1\runtime\scriptcs.exe"
    $OctopusParameters["Foo.Bar"] = "variables rule!" 

    $env:OctopusVariablesFile = $variablesFile

    Write-OctopusVariables -variables $OctopusParameters -variablesFile $variablesFile

    It "Should find ScriptCS.exe" {
        Test-Path $OctopusParameters["Octopus.Scripting.PathToScriptCS"] | Should Be $true
    }

    Context "When calling a ScriptCS script without variables" {
        'Console.WriteLine("Hello world!");' | Out-File "TestScript.csx"

        It "Should invoke script" {
            $out = (Invoke-OctopusScriptCSScript "TestScript.csx" | Out-String)
            $out | Should Match "world!"
        }

	    Remove-Item "TestScript.csx"
    }
    
    Context "When calling a ScriptCS script with variables" {
        'Console.WriteLine("Hello, " + Octopus.Parameters["Foo.Bar"]);' | Out-File "TestScript.csx"

        It "Should invoke script" {
            $out = (Invoke-OctopusScriptCSScript "TestScript.csx" | Out-String)
            $out | Should Match "Hello, variables rule!"
        }

	    Remove-Item "TestScript.csx"
    }
	
    Remove-Item $variablesFile
}