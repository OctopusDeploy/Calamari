# Open this file in PowerShell ISE, then run it. 

$ErrorActionPreference = "Stop"

try { Clear } catch { }

function global:Write-Host(  
    [parameter(Mandatory=$false, ValueFromRemainingArguments =$true)]
    [String[]]$x)
{
    if ($x) 
    {
        write-output ([String]::Join(" ", $x))
        [System.Console]::WriteLine([String]::Join(" ", $x))
    }
}

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
cd $here

$env:OctopusVariables = [System.IO.Path]::GetFullPath("TestVariables.tmp")
Write-Host "Variables file" $env:OctopusVariables

Import-Module "$here\..\Octopus.Deploy.Scripts\Tentacle.psm1" -Force

try
{
    Import-Module "..\packages\Pester.3.3.5\tools\pester.psm1"
    Invoke-Pester

    Write-Host "Done"
}
finally 
{
#    Remove-Item $env:OctopusVariables
}

