$parent = split-path -parent $MyInvocation.MyCommand.Definition



function Convert-ServiceMessageValue([string]$value)
{
	$valueBytes = [System.Text.Encoding]::UTF8.GetBytes($value)
	return [Convert]::ToBase64String($valueBytes)
}

function Get-FileDetails([string]$fileName)
{
	[Reflection.Assembly]::LoadWithPartialName("System.Security") | out-null
	$sha1 = New-Object System.Security.Cryptography.SHA1Managed

	$file = [System.IO.File]::Open($filename, "open", "read")
	$fileHash = ""
    $sha1.ComputeHash($file) | %{
        $fileHash += $_.ToString("x2")
    }
    $file.Dispose()
	Set-OctopusVariable "FileHash" $fileHash
	
	$size = (Get-Item $fileName).length
	Set-OctopusVariable "FileSize" $size
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

function Install-Package([string]$packageFileName, [string]$outputDirectory)
{
    function MapUriPath([System.Uri]$partUri)
    {
        $path = $partUri.ToString().Trim("/")
        $path = $path.Replace("/", "\\")
        $path = [System.Uri]::UnescapeDataString($path)
        return $path
    }

    function IsSupposedToBeExtracted([string]$path) 
    {       
        if ($path.StartsWith("_rels", [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
        if ($path.StartsWith("package\\services\\metadata", [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
        if ([System.IO.Path]::GetExtension($path).Equals(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) -and $path.IndexOf('/') -eq -1) { return $false }
        return $true
    }

    $package = [System.IO.Packaging.Package]::Open($packageFileName)
    $extracted = 0

    try
    {
        foreach ($part in $package.GetParts())
        {
            $path = MapUriPath($part.Uri)

            if ((IsSupposedToBeExtracted($path)) -eq $false) 
            {
                continue
            }
            
            $targetPath = [System.IO.Path]::Combine($outputDirectory, $path)
            $parentDirectory = [System.IO.Path]::GetDirectoryName($targetPath)
            if ((Test-Path $parentDirectory) -eq $false)
            {
                New-Item -ItemType Directory -Path $parentDirectory -ErrorAction SilentlyContinue | Out-Null
            }

            $partStream = $part.GetStream()
            $targetStream = New-Object System.IO.FileStream -ArgumentList @($targetPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::Read)
            $partStream.CopyTo($targetStream)
            $targetStream.Flush()
            $partStream.Dispose()
            $targetStream.Dispose()
        }
    }
    finally 
    {
        $package.Close()
    }
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

function Write-Debug([string]$message)
{
	Write-Verbose $message
}

function Write-Verbose([string]$message)
{
	Write-Host "##octopus[stdout-verbose]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Write-Warning([string]$message)
{
	Write-Host "##octopus[stdout-warning]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Reload-OctopusVariables([string]$variablesFile)
{
	function MakeLegacyKey($key) 
	{
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

	function MakeSmartKey($key)
	{
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
	
	$global:OctopusParameters.GetNames() | ForEach-Object {
		$name = $_
		$value = $result.Get($_)
		$legacyKey = MakeLegacyKey($name)
		$smartKey = MakeSmartKey($name)
		if ($legacyKey -ne $smartKey)
		{
			AssignVariable -k $legacyKey -v $value
		}
	    AssignVariable -k $smartKey -v $value
	}
}
