$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Tools.psm1" -Force

$pathToPackageDownloads = "$here\..\..\Work\"

function Invoke-Test([string]$packageId, [string]$packageVersion, [string]$feedUri, [string]$feedUsername, [string]$feedPassword, [switch]$forcePackageDownload)
{
    $out = (Invoke-PackageDownload -packageId $packageId -packageVersion $packageVersion -feedUri $feedUri -feedUsername $feedUsername -feedPassword $feedPassword -forcePackageDownload:$forcePackageDownload.IsPresent | Out-String)
    return $out
}

function Clear-DownloadFolder([string]$packageId, [string]$packageVerison)
{
    Remove-Item "$pathToPackageDownloads\${packageId}.${packageVersion}*.nupkg"
}

Describe "Tentacle.PackageDownloader" {
    It "Public feed" {
        $packageId = "OctoConsole"
        $packageVersion = "1.0.0.0"
        $feedUri = "https://www.myget.org/F/octopusdeploy-tests"

        $out = Invoke-Test $packageId $packageVersion $feedUri
        $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$feedUri'"))
        $out | Should Match "Downloaded package will be stored in: .*\\Work\\"
        $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"40d78a00090ba7f17920a27cc05d5279bd9a4856`"]"))
        $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"6346`"]"))
        $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: $feedUri"))
        Clear-DownloadFolder $packageId $packageVersion
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

Remove-Item $pathToPackageDownloads