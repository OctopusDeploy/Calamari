#!/usr/bin/env bash
bash --version 2>&1 | head -n 1

set -eo pipefail
SCRIPT_DIR=$(cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd)

###########################################################################
# CONFIGURATION
###########################################################################

BUILD_PROJECT_FILE="$SCRIPT_DIR/build/_build.csproj"
TEMP_DIRECTORY="$SCRIPT_DIR//.nuke/temp"

DOTNET_GLOBAL_FILE="$SCRIPT_DIR//global.json"
DOTNET_INSTALL_URL="https://dot.net/v1/dotnet-install.sh"
DOTNET_CHANNEL="Current"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_MULTILEVEL_LOOKUP=0

###########################################################################
# EXECUTION
###########################################################################

function FirstJsonValue {
    # Stock Nuke shells out to perl. The Amazon Linux execution container does not ship it, so fall
    # back to sed rather than dying with "perl: command not found".
    if command -v perl &>/dev/null; then
        perl -nle 'print $1 if m{"'"$1"'": "([^"]+)",?}' <<< "${@:2}"
    else
        sed -n 's/.*"'"$1"'"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' <<< "${@:2}" | head -n 1
    fi
}

# ----- Octopus Deploy Modification -----
# Stock Nuke assumes curl. The Linux execution containers install wget instead
function DownloadFile {
    local url="$1"
    local destination="$2"

    if command -v curl &>/dev/null; then
        curl -Lsfo "$destination" "$url"
    elif command -v wget &>/dev/null; then
        wget -qO "$destination" "$url"
    else
        echo "Unable to download $url - neither curl nor wget is available on this machine." >&2
        return 1
    fi
}

# Without this you get "command not found", makes you think missing tool but it's actually missing SDK
function DotnetBootstrapReason {
    if [[ ! -x "$(command -v dotnet)" ]]; then
        echo "no 'dotnet' found on PATH"
    else
        local installed
        installed=$(dotnet --list-sdks 2>/dev/null | cut -d' ' -f1 | paste -sd' ' -)
        echo "'dotnet --version' failed - global.json likely pins an SDK that is not installed (found: ${installed:-none})"
    fi
}
# ----- End Octopus Deploy Modification -----

# If dotnet CLI is installed globally and it matches requested version, use for execution
if [ -x "$(command -v dotnet)" ] && dotnet --version &>/dev/null; then
    export DOTNET_EXE="$(command -v dotnet)"
else
    echo "Bootstrapping a local .NET SDK: $(DotnetBootstrapReason)"

    # Download install script
    DOTNET_INSTALL_FILE="$TEMP_DIRECTORY/dotnet-install.sh"
    mkdir -p "$TEMP_DIRECTORY"
    DownloadFile "$DOTNET_INSTALL_URL" "$DOTNET_INSTALL_FILE"
    chmod +x "$DOTNET_INSTALL_FILE"

    # If global.json exists, load expected version
    if [[ -f "$DOTNET_GLOBAL_FILE" ]]; then
        DOTNET_VERSION=$(FirstJsonValue "version" "$(cat "$DOTNET_GLOBAL_FILE")")
        if [[ "$DOTNET_VERSION" == ""  ]]; then
            unset DOTNET_VERSION
        fi
    fi

    # ----- Octopus Deploy Modification -----
    #
    # The default behaviour of the Nuke Bootstrapper (when .NET is not already preinstalled) is
    # to read from the global.json, then install that exact version. It doesn't roll forward.
    # This means that if our global.json says 8.0.100, and the latest version is 8.0.200, it will
    # always install 8.0.100 and we will not pick up any security or bug fixes that 8.0.200 carries.
    #
    # This means we would need to manually update our global.json file every time there is a new
    # .NET SDK available, and then all developers would need to immediately install this on their machines.
    #
    # In our builds, we want the same "automatic roll-forward" behaviour that we get when we use the dotnet/sdk:10.0 docker
    # images -- where we always get the latest patch version of the SDK without manual intervention.
    #
    # We achieve this with a small tweak to the Nuke bootstrapper to tell it to install the latest version from
    # the 10.0 channel, regardless of what's in the global.json.

    unset DOTNET_VERSION
    DOTNET_CHANNEL="10.0"
    # ----- End Octopus Deploy Modification -----

    # Install by channel or version
    DOTNET_DIRECTORY="$TEMP_DIRECTORY/dotnet-unix"
    if [[ -z ${DOTNET_VERSION+x} ]]; then
        "$DOTNET_INSTALL_FILE" --install-dir "$DOTNET_DIRECTORY" --channel "$DOTNET_CHANNEL" --no-path
    else
        "$DOTNET_INSTALL_FILE" --install-dir "$DOTNET_DIRECTORY" --version "$DOTNET_VERSION" --no-path
    fi
    export DOTNET_EXE="$DOTNET_DIRECTORY/dotnet"

    # ----- Octopus Deploy Modification -----
    # Update the path with the temporary dotnet exe so it can be found by anything be run out of this shell
    export PATH="$PATH:$DOTNET_DIRECTORY"
    # ----- End Octopus Deploy Modification -----
fi

echo "Microsoft (R) .NET Core SDK version $("$DOTNET_EXE" --version)"

"$DOTNET_EXE" build "$BUILD_PROJECT_FILE" /nodeReuse:false /p:UseSharedCompilation=false -nologo -clp:NoSummary --verbosity quiet
"$DOTNET_EXE" run --project "$BUILD_PROJECT_FILE" --no-build -- "$@"
