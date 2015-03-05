$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition


# Loads modules under Modules folder
Get-ChildItem -Path "$scriptPath\Modules" -Include *.psm1 -Recurse | foreach { Import-Module $_ 3>$null }

$version = [System.IO.Path]::GetFileName($scriptPath)
Write-Verbose "Tentacle version $version"
