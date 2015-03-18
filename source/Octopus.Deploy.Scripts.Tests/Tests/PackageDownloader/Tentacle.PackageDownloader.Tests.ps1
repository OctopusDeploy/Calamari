$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Common.psm1" -Force
Import-Module "$here\..\..\..\Octopus.Deploy.Scripts\Modules\Tentacle.Tools.psm1" -Force

$pathToPackageDownloads = "$here\..\..\Work\"

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
$packageId = "OctoConsole"
$invalidPackageId = "OctConsole"
$packageVersion = "1.0.0.0"
$invalidPackageVersion = "1.0.0.x"
$feedId = "feeds-myget"
$authFeedId = "feeds-authmyget"
$localFeedId = "feeds-local"
$feedUri = "https://www.myget.org/F/octopusdeploy-tests"
$authFeedUri = "https://www.myget.org/F/octopusdeploy-testsauthd"
$invalidFeedUri = "www.myget.org/F/octopusdeploy-tests"
$localFileShare = "C:\Packages"
$invalidLocalFileShare = "X:\Packages"
$localFileShareUri = "file:///C:/Packages"
$feedUsername = ""
$feedPassword = ""
$invalidFeedUsername = ""
$invalidFeedPassword = ""

Describe "Tentacle.PackageDownloader" {
    Context "Public NuGet Feed" {
        It "should download package from feed" {
            $out = Invoke-Test $packageId $packageVersion $feedId $feedUri
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$feedUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$feedId"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"40d78a00090ba7f17920a27cc05d5279bd9a4856`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"6346`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$feedUri'"))
        }

        It "should use package from cache" {
            $out = Invoke-Test $packageId $packageVersion $feedId $feedUri
            $out | Should Match ([regex]::Escape("Checking package cache for package $packageId $packageVersion"))
            $out | Should Match "Package was found in cache. No need to download. Using file: .*\\Work\\$feedId\\.*$packageId`.${packageVersion}_.*`.nupkg"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"40d78a00090ba7f17920a27cc05d5279bd9a4856`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"6346`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }

        It "should download package from feed even though it is in cache" {
            $out = Invoke-Test $packageId $packageVersion $feedId $feedUri -forcePackageDownload
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$feedUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$feedId"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"40d78a00090ba7f17920a27cc05d5279bd9a4856`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"6346`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$feedUri'"))
        }
    }

    Context "Authenticated NuGet Feed" {
        It "should download package from feed" {
            #$out = Invoke-Test $packageId $packageVersion $authFeedId $authFeedUri $feedUsername $feedPassword
        }

        It "should fail when invalid credentials" {
            #{ Invoke-Test $packageId $packageVersion $authFeedId $authFeedUri $invalidFeedUsername $invalidFeedPassword } | Should Throw
        }

        It "should use package from cache" {
        }

        It "should download package from feed even though package is in cache" {
        }
    }

    Context "Local fileshare" {
        It "should fail when invalid fileshare" {
            { Invoke-Test $packageId $packageVersion $localFeedId $invalidLocalFileShare } | Should Throw
        }

        It "should download package from local fileshare" {
            $out = Invoke-Test $packageId $packageVersion $localFeedId $localFileShare
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$localFileShareUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$localFeedId"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$localFileShare'"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"40d78a00090ba7f17920a27cc05d5279bd9a4856`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"6346`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }

        It "should fail when no permissions to fileshare" {
        }

        It "should use package from cache" {
            $out = Invoke-Test $packageId $packageVersion $localFeedId $localFileShare
            $out | Should Match ([regex]::Escape("Checking package cache for package $packageId $packageVersion"))
            $out | Should Match "Package was found in cache. No need to download. Using file: .*\\Work\\$localFeedId\\.*$packageId`.${packageVersion}_.*`.nupkg"
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"40d78a00090ba7f17920a27cc05d5279bd9a4856`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"6346`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }

        It "should download package from fileshare when package is in cache" {
            $out = Invoke-Test $packageId $packageVersion $localFeedId $localFileShare -forcePackageDownload
            $out | Should Match ([regex]::Escape("Downloading NuGet package $packageId $packageVersion from feed: '$localFileShareUri'"))
            $out | Should Match "Downloaded package will be stored in: .*\\Work\\$localFeedId"
            $out | Should Match ([regex]::Escape("Package $packageId $packageVersion successfully downloaded from feed: '$localFileShare'"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Hash`" value=`"40d78a00090ba7f17920a27cc05d5279bd9a4856`"]"))
            $out | Should Match ([regex]::Escape("##octopus[setVariable name=`"Package.Size`" value=`"6346`"]"))
            $out | Should Match "##octopus[setVariable name=`"Package.InstallationDirectoryPath`" value=`".*\Work\.*\.*$packageId`.${packageVersion}_.*`.nupkg`"]"
        }
    }

    Context "NuGet package details" {
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
    
    Context "NuGet feed details" {
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