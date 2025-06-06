#!/usr/bin/env bash

auto_accept=""
target_framework=""
target_runtime=""

while test $# -gt 0; do
  case "$1" in
  -y)
    auto_accept='yes'
    ;;
  --framework)
    target_framework=$2
    shift
    ;;
  --runtime)
    target_runtime=$2
    shift
    ;;
  esac
  shift
done

Green='\033[32m'
Yellow='\033[1;33m'
NoColour='\033[0m' # No Color

StartMessage="${Green}\
╬════════════════════════════════════════════════════════════════════════════════════════════════╬
║                                                                                                ║
║  This is a helper script for running local Calamari builds, it's going to do the following:    ║
║    * Append a timestamp to the NuGet package versions to give you a unique version number      ║
║      without needing to commit your changes locally                                            ║
║    * Set the msbuild verbosity to minimal to reduce noise                                      ║
║    * Create NuGet packages for the various runtimes in parallel                                ║
║    * Skip creating .Tests NuGet packages                                                       ║
║    * Set the CalamariVersion property in Octopus.Server.csproj                                 ║
║                                                                                                ║ 
║  This script is intended to only be run locally and not in CI.                                 ║
║                                                                                                ║
║  If something unexpected is happening in your build or Calamari changes you may want to run    ║
║  the full build by running ./build.ps1 and check again as something in the optimizations here  ║
║                                                                                                ║
║  might have caused an issue.                                                                   ║
╬════════════════════════════════════════════════════════════════════════════════════════════════╬\
${NoColour}
"

WarningMessage="${Yellow}\
╬════════════════════════════════════════════════════════╬
║ WARNING:                                               ║
║ Building Calamari on a non-windows machine will result ║
║ in Calmari and Calamari.Cloud nuget packages being     ║
║ built against net6.0. This means that some             ║
║ steps may not work as expected because they require a  ║
║ .Net Framework compatible Calamari Nuget Package.      ║
╬════════════════════════════════════════════════════════╬\
${NoColour}"

FinishMessage="${Green}\
╬══════════════════════════════════════════════════════════════════════════════════════╬
║                                                                                      ║
║  Local build complete, restart your Octopus Server to test your Calamari changes :)  ║
║                                                                                      ║
╬══════════════════════════════════════════════════════════════════════════════════════╬\
${NoColour}"

echo -e "$StartMessage"

echo -e "$WarningMessage"

if [ -z "$auto_accept" ]; then
  read -p "Are you sure you want to continue? (Y,n): " option
  
  if ! [[ -z "$option" ]] && ! [[ "$option" == 'Y' ]] && ! [[ "$option" == 'y' ]]
  then
      echo "Build Cancelled."
      exit 0;
  fi
fi

branch=$(git branch --show-current)

echo "Branch: $branch"

year=$(date '+%Y')
numericVersion="$year.99.0"

sanitizedBranch=$(echo "$branch" | sed 's|^refs/heads/||; s|[/_]|-|g' | sed 's|\+.*$||g')

export OCTOVERSION_MajorMinorPatch="$numericVersion"
export OCTOVERSION_PreReleaseTagWithDash="-$sanitizedBranch"
export OCTOVERSION_FullSemVer="$numericVersion-$sanitizedBranch"

./build.sh -BuildVerbosity Minimal -Verbosity Minimal -AppendTimestamp -SetOctopusServerVersion -TargetFramework "$target_framework" -TargetRuntime "$target_runtime"

echo -e "$FinishMessage"