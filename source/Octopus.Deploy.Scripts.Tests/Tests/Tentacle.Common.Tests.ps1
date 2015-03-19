$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force

Describe "Tentacle.Common" {
    It "Should write and read variables" {
        $created = New-Object Octostache.VariableDictionary
        $created["Foo.Foo"] = "bar"
        $created["Foo.Baz"] = "Hello worldæ!"
        $created["Foo.Empty"] = ""
        $created["Foo.Null"] = $null

        $variablesFile = "$here\Variables.vars"

        Write-OctopusVariables -variables $created -variablesFile $variablesFile
        $read = Read-OctopusVariables -variablesFile $variablesFile

        $read["Foo.Foo"] | Should Be "bar"
        $read["Foo.Baz"] | Should Be "Hello worldæ!"
        $read["Foo.Empty"] | Should Be ""
        $read["Foo.Null"] | Should Be ""
        Remove-Item $variablesFile
    }
}