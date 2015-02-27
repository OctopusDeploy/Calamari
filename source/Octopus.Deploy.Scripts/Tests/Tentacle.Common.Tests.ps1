$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\Modules\Tentacle.Common.psm1" -Force

Describe "Tentacle.Common" {

    Context "Variables are read from and written to a file" {
        $created = New-Object 'System.Collections.Generic.Dictionary[String,String]' (,[System.StringComparer]::OrdinalIgnoreCase)
        $created["Foo.Bar"] = "bar"
        $created["Foo.Baz"] = "Hello worldæ!"
        $created["Foo.Empty"] = ""
        $created["Foo.Null"] = $null

        $variablesFile = "$here\Variables.vars"

        Write-OctopusVariables -variables $created -variablesFile $variablesFile
        $read = Read-OctopusVariables -variablesFile $variablesFile

        It "Should write and read variables" {
            $read["Foo.Bar"] | Should Be "bar"
            $read["Foo.Baz"] | Should Be "Hello worldæ!"
            $read["Foo.Empty"] | Should Be ""
            $read["Foo.Null"] | Should Be ""
        }
        
        Remove-Item $variablesFile
    }    
}