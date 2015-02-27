# Open this file in PowerShell ISE, then run it. 

$ErrorActionPreference = "Stop"

try { Clear } catch { }

$here = Split-Path -Parent $MyInvocation.MyCommand.Path

cd $here

Import-Module "..\packages\Pester.3.3.5\tools\pester.psm1"
Invoke-Pester

Write-Host "Done"
