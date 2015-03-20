$OctopusModuleRoot = split-path -parent $MyInvocation.MyCommand.Definition
$TentaclePackageVersion = [System.IO.Path]::GetFileName($scriptPath)
Write-Verbose "Tentacle version $TentaclePackageVersion"

function Import-OctopusModules
{
	# Loads modules under Modules folder
	Get-ChildItem -Path "$OctopusModuleRoot\Modules" -Include *.psm1 -Recurse | foreach { 
		Write-Verbose "Importing $_";
		Write-Host "##octopus[stdout-verbose]" 
		Import-Module $_ -Verbose
		Write-Host "##octopus[stdout-default]"
	}
}

function Find-OctostacheDll 
{
	$attemptOne = [System.IO.Path]::GetFullPath("$OctopusModuleRoot\Tools\Octostache.dll")
	$attemptTwo = [System.IO.Path]::GetFullPath("$OctopusModuleRoot\..\Octopus.Deploy.Substitutions\bin\Octostache.dll")
	if (Test-Path $attemptOne) 
	{
		return $attemptOne
	}
	else 
	{
		if (Test-Path $attemptTwo) 
		{
			return $attemptTwo
		}
	}

	throw "Could not find Octostache.dll under '$attemptOne' or '$attemptTwo'"
}

function Import-OctopusVariables
{
	# Ensure Octostache.dll, and any dependencies, are loaded
	$originalLocation = Get-Location
	try 
	{
		$octostache = Find-OctostacheDll
		Set-Location -path ([System.IO.Path]::GetDirectoryName($octostache))
		[Reflection.Assembly]::LoadFrom("$octostache") | Out-Null
	}
	finally 
	{
		Set-Location $originalLocation
	}
	
	if (([System.String]::IsNullOrEmpty($env:OctopusVariables)) -eq $true)
	{
		throw "The environment variable 'OctopusVariables' was not set. It should have been set to point at a file containing the JSON-encoded variables."
	}

	$global:OctopusParameters = New-Object Octostache.VariableDictionary -ArgumentList @($env:OctopusVariables)
	$global:OctopusParameters["Foo"] = "Bar"
	$global:OctopusParameters.Save()

	Write-Host "Variables loaded from (and saved to):" $env:OctopusVariables
}

# Initialization
Import-OctopusVariables
Import-OctopusModules
