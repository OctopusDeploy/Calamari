#!/usr/bin/env bash

# Define directories.
SCRIPT_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )

# Define default arguments.
SCRIPT="build.cake"
TARGET="Default"
CONFIGURATION="Debug"
WHERE=""
VERBOSITY="Diagnostic"
DRYRUN=
SHOW_VERSION=false
SCRIPT_ARGUMENTS=()

# Parse arguments.
for i in "$@"; do
    case $1 in
        -s|--script) SCRIPT="$2"; shift ;;
        -t|--target) TARGET="$2"; shift ;;
        -c|--configuration) CONFIGURATION="$2"; shift ;;
        -v|--verbosity) VERBOSITY="$2"; shift ;;
		-w|--where) WHERE="$2"; shift ;;
		-d|--dryrun) DRYRUN="-dryrun" ;;
        --version) SHOW_VERSION=true ;;
        --) shift; SCRIPT_ARGUMENTS+=("$@"); break ;;
        *) SCRIPT_ARGUMENTS+=("$1") ;;
    esac
    shift
done

# Make sure that Cake has been installed.
dotnet cake --version > /dev/null
if [ $? -ne 0 ]; then
    dotnet tool install --global Cake.Tool
    dotnet cake --version > /dev/null
    if [ $? -ne 0 ]; then
        echo "Unable to install cake tool with command: dotnet tool install --global Cake.Tool"
        exit 1;
    fi
fi

# Start Cake
if $SHOW_VERSION; then
    dotnet cake "$CAKE_EXE" -version
else
    echo bootstrapping cake...
    dotnet cake --bootstrap --verbosity=$VERBOSITY
    echo executing following command:
    echo dotnet cake $SCRIPT --verbosity=$VERBOSITY --configuration=$CONFIGURATION --target=$TARGET $DRYRUN "${SCRIPT_ARGUMENTS[@]}" --includeNetFramework=false
    dotnet cake $SCRIPT --verbosity=$VERBOSITY --configuration=$CONFIGURATION --target=$TARGET $DRYRUN "${SCRIPT_ARGUMENTS[@]}" --includeNetFramework=false
fi