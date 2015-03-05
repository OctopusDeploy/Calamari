function Convert-ServiceMessageValue([string]$value)
{
	$valueBytes = [System.Text.Encoding]::UTF8.GetBytes($value)
	return [Convert]::ToBase64String($valueBytes)
}

function Set-OctopusVariable([string]$name, [string]$value) 
{ 	
    $name = Convert-ServiceMessageValue($name)
    $value = Convert-ServiceMessageValue($value)

	Write-Host "##octopus[setVariable name='$($name)' value='$($value)']"
}

function New-OctopusArtifact([string]$path, [string]$name="""") 
{ 	
    if ($name -eq """") 
    {
        $name = [System.IO.Path]::GetFileName($path)
        $name = Convert-ServiceMessageValue($name)
    }

    $path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path)
    $path = [System.IO.Path]::GetFullPath($path)
    $path = Convert-ServiceMessageValue($path)

	Write-Host "##octopus[createArtifact path='$($path)' name='$($name)']"
}

function Write-Warning([string]$message)
{
	Write-Host "##octopus[stdout-warning]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Write-Verbose([string]$message)
{
	Write-Host "##octopus[stdout-verbose]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Write-Debug([string]$message)
{
	Write-Verbose $message
}

function Get-FileSizeString([long]$bytes)
{
    $Kilobyte = 1024;
    $Megabyte = 1024 * $Kilobyte;
    $Gigabyte = 1024 * $Megabyte;
    $Terabyte = 1024 * $Gigabyte;

    if ($bytes -gt $Terabyte) { return ($bytes / $Terabyte).ToString("0 TB") }
    if ($bytes -gt $Gigabyte) { return ($bytes / $Gigabyte).ToString("0 GB") }
    if ($bytes -gt $Megabyte) { return ($bytes / $Megabyte).ToString("0 MB") }
    if ($bytes -gt $Kilobyte) { return ($bytes / $Kilobyte).ToString("0 KB") }
    return $bytes + " bytes";   
}

function Read-OctopusVariables([string]$variablesFile)
{
	function MakeLegacyKey($key) {
		$result = New-Object System.Text.StringBuilder

		for ($i = 0; $i -lt $key.Length; $i++)
		{
			if ([System.Char]::IsLetterOrDigit($key[$i]))
			{
				$c = $key[$i]
				$null = $result.Append($c)
			}
		}

		return $result.ToString()
	}

	function MakeSmartKey($key){
		$result = New-Object System.Text.StringBuilder

		for ($i = 0; $i -lt $key.Length; $i++)
		{
			$c = $key[$i]
			if (([System.Char]::IsLetterOrDigit($key[$i])) -or ($c -eq '_'))
			{
				$null = $result.Append($c)
			}
		}

		return $result.ToString()
	}

	function AssignVariable($k, $v) 
	{
		$fullVariablePath = "variable:global:$k"
		if (-Not (Test-Path $fullVariablePath)) 
		{
			Set-Item -Path $fullVariablePath -Value $v
		}
	}

	$targetStream = New-Object System.IO.FileStream -ArgumentList @($variablesFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
	$reader = New-Object System.IO.StreamReader -ArgumentList @($targetStream)
	$result = New-Object 'System.Collections.Generic.Dictionary[String,String]' (,[System.StringComparer]::OrdinalIgnoreCase)

	while (($line = $reader.ReadLine()))
	{
		if ([String]::IsNullOrEmpty($line)) 
		{
			continue;
		}

		$parts = $line.Split(',')
		$name = $parts[0]
		$value = $parts[1]
	
		$name = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($name))
		$value = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value))

		$result[$name] = $value

		$legacyKey = MakeLegacyKey($name)
		$smartKey = MakeSmartKey($name)
		if ($legacyKey -ne $smartKey)
		{
			AssignVariable -k $legacyKey -v $value
		}
	    AssignVariable -k $smartKey -v $value
	}

	$reader.Dispose()
	$targetStream.Dispose()
	return $result
}

function Write-OctopusVariables($variables, [string]$variablesFile) 
{
	$targetStream = New-Object System.IO.FileStream -ArgumentList @($variablesFile, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::Read)
	$writer = New-Object System.IO.StreamWriter -ArgumentList @($targetStream)
	foreach ($pair in $variables.GetEnumerator())
	{
        $name = $pair.Key
        $value = $pair.Value
        
        if ([string]::IsNullOrEmpty($name)) { continue; }
        if ([string]::IsNullOrEmpty($value)) { $value = "" }

		$name = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($name))
		$value = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($value))
		$writer.Write($name)
		$writer.Write(",")
		$writer.Write($value)
		$writer.WriteLine()
	}
    $writer.Flush()
	$writer.Dispose()
	$targetStream.Dispose()
}
