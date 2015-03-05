function Invoke-OctopusPowerShellScript
{
	[CmdletBinding()]
	param
    (
		[Parameter(Mandatory=$True, ValueFromPipeline=$True)]
		[string]$scriptName
	)

	if ([System.IO.Path]::IsPathRooted($scriptName) -eq $false) 
	{
		$scriptName = ".\$scriptName"
	}

    . $scriptName
}

function Invoke-OctopusScriptCSScript
{
	[CmdletBinding()]
	param
    (
		[Parameter(Mandatory=$True, ValueFromPipeline=$True)]
		[string]$scriptName
	)

    $parentPath = Split-Path -parent $script:MyInvocation.MyCommand.Path
    $scriptCsExe = $OctopusParameters["Octopus.Scripting.PathToScriptCS"]
    if (!$scriptCsExe) {
	    $scriptCsExe = Resolve-Path "$parentPath\..\Tools\ScriptCS\ScriptCS.exe"
    }

    $tempScriptFile = "$scriptName.bootstrap.csx"
    $configPath = resolve-path "${parentPath}\ConfigurationLoader.csx"
    $scriptPath = resolve-path "${scriptName}"
    "#load ""${configPath}""`r`n#load ""${scriptPath}""`r`n" | Out-File $tempScriptFile
    & "$scriptCSexe" $tempScriptFile 2>&1
    Remove-Item $tempScriptFile
}

function Invoke-OctopusScript
{
    [CmdletBinding()]
	param
    (
		[Parameter(Mandatory=$True, ValueFromPipeline=$True)]
		[string[]]$scripts
	)

    begin { }
	process 
    {
		    foreach ($scriptName in $scripts)
            {
			    switch ([System.IO.Path]::GetExtension($scriptName).ToLowerInvariant())
                {
                    ".ps1" { Invoke-OctopusPowerShellScript $scriptName }
                    ".csx" { Invoke-OctopusScriptCSScript $scriptName }
                }
		    }
    }
	end { }
}
