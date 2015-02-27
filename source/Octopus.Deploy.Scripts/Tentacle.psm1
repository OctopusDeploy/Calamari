Write-Host "Tentacle module loaded..."

$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition

# Loads modules under Modules folder
Get-ChildItem -Path "$scriptPath\Modules" -Include *.psm1 -Recurse | foreach { Write-Host "Importing $_"; Import-Module $_ 3>$null }
