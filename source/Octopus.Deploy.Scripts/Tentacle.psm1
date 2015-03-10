$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition


# Loads modules under Modules folder
Get-ChildItem -Path "$scriptPath\Modules" -Include *.psm1 -Recurse | foreach { 
	Write-Verbose "Importing $_";
	Write-Host "##octopus[stdout-verbose]" 
	Import-Module $_ -Verbose
	Write-Host "##octopus[stdout-default]"
}

$version = [System.IO.Path]::GetFileName($scriptPath)
Write-Verbose "Tentacle version $version"
