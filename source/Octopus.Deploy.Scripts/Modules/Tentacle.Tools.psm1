$parent = split-path -parent $MyInvocation.MyCommand.Definition

function Find-OctopusTool
{
    [CmdletBinding()]
	param
    (
		[Parameter(Mandatory=$True)]
		[string]$name
	)
	
	$attemptOne = [System.IO.Path]::GetFullPath("$parent\..\Tools\$name")
	if (Test-Path $attemptOne) 
	{
		return $attemptOne
	}

	$folder = [System.IO.Path]::GetFileNameWithoutExtension($name) 
	$attemptTwo = [System.IO.Path]::GetFullPath("$parent\..\..\$folder\bin\$name")
	if (Test-Path $attemptTwo) 
	{
		return $attemptTwo
	}

	throw "Cannot find a tool named $name. Search paths: `r`n$attemptOne`r`n$attemptTwo"
}

function Update-OctopusApplicationConfigurationFile
{
    [CmdletBinding()]
	param
    (
		[Parameter(Mandatory=$True, ValueFromPipeline=$True)]
		[string[]]$configurationFiles
	)

    begin { }
	process 
    {
		$exe = Find-OctopusTool -name "Octopus.Deploy.ConfigurationVariables.exe"
        $varsFile = [System.IO.Path]::GetFullPath("Variables.vars.tmp")
        Write-OctopusVariables $OctopusParameters -variablesFile $varsFile	

        try
        {
		    foreach ($config in $configurationFiles)
            {
                & $exe $config -variablesFile "$varsFile"
                if ($LASTEXITCODE -ne 0) 
                {
                    throw "Exit code $LASTEXITCODE returned"
                }
		    }
        }
        finally 
        {
            Remove-Item $varsFile -Force
        }

    }
	end { }
}

function Invoke-OctopusConfigurationTransform
{
    [CmdletBinding()]
	param
    (
		[Parameter(Mandatory=$True)]
		[string]$config,
	
		[Parameter(Mandatory=$True)]
		[string]$transform
	)

	$exe = Find-OctopusTool -name "Octopus.Deploy.ConfigurationTransforms.exe"
	
	& "$exe" "$config" "$transform" "$config"

    if ($LASTEXITCODE -ne 0) 
    {
        throw "Exit code $LASTEXITCODE returned"
    }
}

