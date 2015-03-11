$parent = split-path -parent $MyInvocation.MyCommand.Definition

function Find-OctopusTool
{
    [CmdletBinding()]
	param
    (
		[Parameter(Mandatory=$True)]
		[string]$name
	)
	
	$exe = [System.IO.Path]::GetFullPath("$parent\..\Tools\$name")
	if (Test-Path $exe) 
	{
		return $exe
	}

	$folder = [System.IO.Path]::GetFileNameWithoutExtension($name) 
	$exe = [System.IO.Path]::GetFullPath("$parent\..\..\$folder\bin\$name")
	if (Test-Path $exe) 
	{
		return $exe
	}

	throw "Cannot find a tool named $name"
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
