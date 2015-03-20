$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$pathToPackageDownloads = "$here\..\..\Work\"

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
	$attemptTwo = [System.IO.Path]::GetFullPath("$parent\..\..\..\$folder\bin\$name")
	if (Test-Path $attemptTwo) 
	{
		return $attemptTwo
	}

	throw "Cannot find a tool named $name. Search paths: `r`n$attemptOne`r`n$attemptTwo"
}

function Invoke-PackageDownload
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory=$True, ValueFromPipeline=$True)]
        [string]$packageId,
        [Parameter(Mandatory=$True, ValueFromPipeline=$True)]
        [string]$packageVersion,
		[Parameter(Mandatory=$True, ValueFromPipeline=$True)]
		[string]$feedId,
        [Parameter(Mandatory=$True, ValueFromPipeline=$True)]
        [string]$feedUri,
        [Parameter(Mandatory=$False, ValueFromPipeline=$True)]
        [string]$feedUsername,
        [Parameter(Mandatory=$False, ValueFromPipeline=$True)]
        [string]$feedPassword,
        [Parameter(Mandatory=$False, ValueFromPipeline=$True)]
        [switch]$forcePackageDownload
    )

    begin { }
    process
    {
        $exe = Find-OctopusTool -name "Octopus.Deploy.PackageDownloader.exe"
        
        $optionalArguments = @()
        if($feedUsername)
        {
            $optionalArguments += @("-feedUsername", "$feedUsername")
        }
        if($feedPassword)
        {
            $optionalArguments += @("-feedPassword", "$feedPassword")
        }
        if($forcePackageDownload)
        {
            $optionalArguments += @("-forcePackageDownload")
        }

        try
        {
            & $exe -packageId "$packageId" -packageVersion "$packageVersion" -feedId "$feedId" -feedUri "$feedUri" ($optionalArguments)
            if($LASTEXITCODE -ne 0)
            {
                throw "Exit code $LASTEXITCODE returned" 
            }
        }
        finally { }
    }
    end { }
}


function Invoke-Test([string]$packageId, [string]$packageVersion, [string]$feedId, [string]$feedUri, [string]$feedUsername, [string]$feedPassword, [switch]$forcePackageDownload)
{
    try {
        $ErrorActionPreference = 'Stop'
        $out = (Invoke-PackageDownload -packageId $packageId -packageVersion $packageVersion -feedId $feedId -feedUri $feedUri -feedUsername $feedUsername -feedPassword $feedPassword -forcePackageDownload:$forcePackageDownload.IsPresent | Out-String)
        return $out
    }
    catch [Exception] {
        Throw $_
    }
}

# Package details
$packageId = "OctoConsole"
$invalidPackageId = "OctConsole"
$packageVersion = "1.0.0.0"
$invalidPackageVersion = "1.0.0.x"
$expectedPackageHash = "40d78a00090ba7f17920a27cc05d5279bd9a4856"
$expectedPackageSize = "6346"

# Public NuGet feed details
$feedId = "feeds-myget"
$feedUri = "https://www.myget.org/F/octopusdeploy-tests"
$invalidFeedUri = "www.myget.org/F/octopusdeploy-tests"

# Authenticated NuGet feed details
$authFeedId = "feeds-authmyget"
$authFeedUri = "https://www.myget.org/F/octopusdeploy-authtests"
$feedUsername = $env:ODPESTER_MYGETUSERNAME
$feedPassword = $env:ODPESTER_MYGETPASSWORD
$invalidFeedPassword = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"

# Local fileshare details
$localFeedId = "feeds-local"
$localFileShare = "C:\Packages"
$invalidLocalFileShare = "X:\Packages"
$localFileShareUri = "file:///C:/Packages"

Describe "Tentacle.PackageDownloader" {
    Context "Public NuGet Feed" {
        It "should download package from feed" {
            $out = Invoke-Test $packageId $packageVersion $feedId $feedUri
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$feedUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$feedId"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$feedUri'"))
        }
        It "should use package from cache" {
            $out = Invoke-Test $packageId $packageVersion $feedId $feedUri
            $out | Should Match ([regex]::Escape("Checking package cache for package $packageId $packageVersion"))
            $out | Should Match "Package was found in cache. No need to download. Using file: .*\\Work\\$feedId\\.*$packageId`.${packageVersion}_.*`.nupkg"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }
        It "should download package from feed even though it is in cache" {
            $out = Invoke-Test $packageId $packageVersion $feedId $feedUri -forcePackageDownload
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$feedUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$feedId"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$feedUri'"))
        }
    }

    Context "Authenticated NuGet Feed" {
        It "should download package from feed" -Skip {
            $out = Invoke-Test $packageId $packageVersion $authFeedId $authFeedUri $feedUsername $feedPassword
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$authFeedUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$authFeedId"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$authFeedUri'"))
        }
        It "should use package from cache" -Skip {
            $out = Invoke-Test $packageId $packageVersion $authFeedId $authFeedUri $feedUsername $feedPassword
            $out | Should Match ([regex]::Escape("Checking package cache for package $packageId $packageVersion"))
            $out | Should Match "Package was found in cache. No need to download. Using file: .*\\Work\\$authFeedId\\.*$packageId`.${packageVersion}_.*`.nupkg"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }
        It "should download package from feed even though package is in cache" -Skip {
            $out = Invoke-Test $packageId $packageVersion $authFeedId $authFeedUri $feedUsername $feedPassword -forcePackageDownload
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$authFeedUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$authFeedId"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$authFeedUri'"))
        }
        It "should fail when invalid credentials" -Skip {
            { Invoke-Test $packageId $packageVersion $authFeedId $authFeedUri $feedUsername $invalidFeedPassword -forcePackageDownload } | Should Throw
        }
    }

    Context "Local fileshare" {
        It "should download package from local fileshare" -Skip {
            $out = Invoke-Test $packageId $packageVersion $localFeedId $localFileShare
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$localFileShareUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$localFeedId"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$localFileShare'"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }
        It "should use package from cache" -Skip {
            $out = Invoke-Test $packageId $packageVersion $localFeedId $localFileShare
            $out | Should Match ([regex]::Escape("Checking package cache for package $packageId $packageVersion"))
            $out | Should Match "Package was found in cache. No need to download. Using file: .*\\Work\\$localFeedId\\.*$packageId`.${packageVersion}_.*`.nupkg"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }
        It "should download package from fileshare when package is in cache" -Skip {
            $out = Invoke-Test $packageId $packageVersion $localFeedId $localFileShare -forcePackageDownload
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$localFileShareUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$localFeedId"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$localFileShare'"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"$expectedPackageHash`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"$expectedPackageSize`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }
        It "should fail when invalid fileshare" -Skip {
            { Invoke-Test $packageId $packageVersion $localFeedId $invalidLocalFileShare -forcePackageDownload } | Should Throw
        }
        It "should fail when no permissions to fileshare" -Skip {
            { Invoke-Test $packageId $packageVersion $localFeedId "" -forcePackageDownload } | Should Throw
        }
    }

    Context "Invalid NuGet package details" {
        It "should fail when no package ID" {
            { Invoke-Test "" $packageVersion $feedId $feedUri } | Should Throw
        }
        It "should fail when invalid package ID" {
            { Invoke-Test $invalidPackageId $packageVersion $feedId $feedUri } | Should Throw
        }
        It "should fail when package version" {
            { Invoke-Test $packageId "" $feedId $feedUri } | Should Throw
        }
        It "should fail when invalid package version" {
            { Invoke-Test $packageId $invalidPackageVersion $feedId $feedUri } | Should Throw
        }
    }
    
    Context "Invalid NuGet feed details" {
        It "should fail when no feed ID" {
            { Invoke-Test $packageId $packageVersion "" $feedUri } | Should Throw
        }
        It "should fail when no no feed URI" {
            { Invoke-Test $packageId $packageVersion $feedId "" } | Should Throw
        }
        It "should fail when invalid feed URI" {
            { Invoke-Test $packageId $packageVersion $feedId $invalidFeedUri } | Should Throw
        }
    }
}

Remove-Item $pathToPackageDownloads -Recurse