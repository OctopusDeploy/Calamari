$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Tools.psm1" -Force

$pathToPackageDownloads = "$here\..\..\Work\"

function Invoke-Test([string]$packageId, [string]$packageVersion, [string]$feedId, [string]$feedUri, [string]$feedUsername, [string]$feedPassword, [switch]$forcePackageDownload)
{
    $out = (Invoke-PackageDownload -packageId $packageId -packageVersion $packageVersion -feedId $feedId -feedUri $feedUri -feedUsername $feedUsername -feedPassword $feedPassword -forcePackageDownload:$forcePackageDownload.IsPresent | Out-String)
    #Write-Host $out
    return $out
}

Describe "Tentacle.PackageDownloader" {
    It "Public feed" {
        $packageId = "OctoConsole"
        $packageVersion = "1.0.0.0"
        $feedId = "feeds-myget"
        $feedUri = "https://www.myget.org/F/octopusdeploy-tests"

        $out = Invoke-Test $packageId $packageVersion $feedId $feedUri
        $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$feedUri'"))
        $out | Should Match "Downloaded package will be stored in: .*\\Work\\$feedId"
        $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"40d78a00090ba7f17920a27cc05d5279bd9a4856`"]"))
        $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"6346`"]"))
        $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: $feedUri"))
    }

    It "Package is in cache" {
        $packageId = "OctoConsole"
        $packageVersion = "1.0.0.0"
        $feedId = "feeds-myget"
        $feedUri = "https://www.myget.org/F/octopusdeploy-tests"

        $out = Invoke-Test $packageId $packageVersion $feedId $feedUri
        $out | Should Match ([regex]::Escape("Checking package cache for package $packageId $packageVersion"))
        $out | Should Match "Package was found in cache. No need to download. Using file: .*\\Work\\$feedId\\.*$packageId`.${packageVersion}_.*`.nupkg"
    }

    It "Authenticated feed" {
    }

    It "Authenticated feed with invalid credentials" {
    }

    It "Local fileshare" {
    }

    It "Invalid package ID" {
    }

    It "Invalid package version" {
    }

    It "Invalid feed URI" {
    }

    It "Force package download" {
    }
}

Remove-Item $pathToPackageDownloads -Recurse