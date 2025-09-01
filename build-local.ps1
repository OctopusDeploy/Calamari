Param(
    [string] $Framework,
    [string] $Runtime
)

Write-Host "
##################################################################################################
#                                                                                                #
#  This is a helper script for running local Calamari builds, it's going to do the following:    #
#    * Append a timestamp to the NuGet package versions to give you a unique version number      #
#      without needing to commit your changes locally                                            #
#    * Set the msbuild verbosity to minimal to reduce noise                                      #
#    * Create NuGet packages for the various runtimes in parallel                                #
#    * Skip creating .Tests NuGet packages                                                       #
#    * Set the CalamariVersion property in Octopus.Server.csproj                                 #
#                                                                                                # 
#  This script is intended to only be run locally and not in CI.                                 #
#                                                                                                #
#  If something unexpected is happening in your build or Calamari changes you may want to run    #
#  the full build by running ./build.ps1 and check again as something in the optimizations here  #
#  might have caused an issue.                                                                   #
#                                                                                                #
##################################################################################################
" -ForegroundColor Cyan

$branch = & git branch --show-current

Write-Host "Branch: $branch"

$now = Get-Date
$year = $now.Year
$numericVersion = "$year.99.0"

$sanitizedBranch = $branch.Replace("/","-").Replace("_","-")

Write-Host "Numeric version: $numericVersion"
Write-Host "Sanitized branch: $sanitizedBranch"

$env:OCTOVERSION_CurrentBranch = $sanitizedBranch
$env:OCTOVERSION_MajorMinorPatch= $numericVersion
$env:OCTOVERSION_PreReleaseTagWithDash = "-$sanitizedBranch"
$env:OCTOVERSION_FullSemVer = "$numericVersion-$sanitizedBranch"

./build.ps1 -BuildVerbosity Minimal -Verbosity Normal --Append-Timestamp -SetOctopusServerVersion -TargetFramework "$Framework" -TargetRuntime "$Runtime"

Write-Host "
########################################################################################
#                                                                                      #
#  Local build complete, restart your Octopus Server to test your Calamari changes :)  #
#                                                                                      #
########################################################################################
" -ForegroundColor Cyan