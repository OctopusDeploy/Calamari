#!/bin/bash
# Octopus Linux helper function script
# Version: 1.1.0
# -----------------------------------------------------------------------------

sensitiveVariableKey=$1

# -----------------------------------------------------------------------------
# Function to base64 encode a service message value
#		Accepts 1 argument:
#			string: the value to encode
# -----------------------------------------------------------------------------
function encode_servicemessagevalue
{
	echo -n "$1" | openssl enc -base64 -A
}

# -----------------------------------------------------------------------------
# Function to base64 decode a service message value
#		Accepts 1 argument:
#			string: the value to decode
# -----------------------------------------------------------------------------
function decode_servicemessagevalue
{
	echo -n "$1" | openssl enc -base64 -A -d
}

# -----------------------------------------------------------------------------
# Functions to request server masking of sensitive values
# -----------------------------------------------------------------------------
function __mask_sensitive_value
{
    # We want to write to both stdout and stderr due to a racecondition between stdout and stderr
    # causing error logs to not be masked if stderr event is handled first.
    echo "##octopus[mask value='$(encode_servicemessagevalue "$1")']"
    echo "##octopus[mask value='$(encode_servicemessagevalue "$1")']" >&2
}

__mask_sensitive_value $sensitiveVariableKey

# -----------------------------------------------------------------------------
# Function to decrypt a sensitive variable
#		Accepts 2 arguments:
#			string: the value to decrypt (base64 encoded)
#			string: the decryption iv (hex)
# -----------------------------------------------------------------------------
function decrypt_variable
{
	echo $1 | openssl enc -a -A -d -aes-256-cbc -nosalt -K $sensitiveVariableKey -iv $2
}

#	---------------------------------------------------------------------------
# Function for getting an octopus variable
#   Accepts 1 argument:
#     string: value of the name of the octopus variable
#	---------------------------------------------------------------------------
function get_octopusvariable
{
  INPUT=$( encode_servicemessagevalue "$1" )

  case $INPUT in
#### VariableDeclarations ####
    *)
      echo ""
    ;;
    esac
}

#	---------------------------------------------------------------------------
# Function for failing a step with an optional message
#   Accepts 1 argument:
#     string: reason for failing
#	---------------------------------------------------------------------------
function fail_step
{

	if [ ! -z "${1:-}" ]
	then
		echo "##octopus[resultMessage message='$(encode_servicemessagevalue "$1")']"
	fi

	exit 1;
}

#	---------------------------------------------------------------------------
# Function for setting an octopus variable
#   Accepts 3 arguments:
#     string: value of the name of the octopus variable
#     string: value of the value of the octopus variable
#     string: optional '-sensitive' to make variable sensitive
#	---------------------------------------------------------------------------
function set_octopusvariable
{
	MESSAGE="##octopus[setVariable"

	if [ -n "$1" ]
	then
		MESSAGE="$MESSAGE name='$(encode_servicemessagevalue "$1")'"
	fi

	if [ -n "$2" ]
	then
		MESSAGE="$MESSAGE value='$(encode_servicemessagevalue "$2")'"
	fi

	if [ ! -z "${3:-}" ] && [ "$3" = "-sensitive" ]
	then
		MESSAGE="$MESSAGE sensitive='$(encode_servicemessagevalue "True")'"
	fi

	MESSAGE="$MESSAGE]"

	echo $MESSAGE
}

# -----------------------------------------------------------------------------
# Function to create a new octopus artifact
#	Accepts 2 arguments:
#	  string: value of the path to the artifact
#	  string: value of the original file name of the artifact
# -----------------------------------------------------------------------------
function new_octopusartifact
{
	echo "Collecting $1 as an artifact..."

	if [ ! -e "$1" ]
	then
		error_exit $PROGNAME $LINENO "\"$(1)\" does not exist." $E_FILE_NOT_FOUND
	    exit $?
	fi

	pth=$1
	ofn=$2
	len=$(wc -c < $1 )

	if [ -z "$ofn" ]
	then
	    ofn=`basename "$pth"`
	fi

	echo "##octopus[stdout-verbose]"
	echo "Artifact $ofn will be collected from $pth after this step completes"
	echo "##octopus[stdout-default]"
	echo "##octopus[createArtifact path='$(encode_servicemessagevalue "$pth")' name='$(encode_servicemessagevalue "$ofn")' length='$(encode_servicemessagevalue $len)']"
}

function remove-octopustarget {
	echo "##octopus[delete-target machine='$(encode_servicemessagevalue "$1")']"
}

function new_octopustarget() (
  parameters=""

  while :
  do
      case "$1" in
        -n | --name)
          parameters="$parameters name='$(encode_servicemessagevalue "$2")'"
          shift 2
          ;;
        -t | --target-id)
          parameters="$parameters targetId='$(encode_servicemessagevalue "$2")'"
          shift 2
          ;;
        --inputs)
          parameters="$parameters inputs='$(encode_servicemessagevalue "$2")'"
          shift 2
          ;;
        --roles)
          parameters="$parameters octopusRoles='$(encode_servicemessagevalue "$2")'"
          shift 2
          ;;
        --worker-pool)
          parameters="$parameters octopusDefaultWorkerPoolIdOrName='$(encode_servicemessagevalue "$2")'"
          shift 2
          ;;
        --update-if-existing)
          parameters="$parameters updateIfExisting='$(encode_servicemessagevalue "true")'"
          shift
          ;;
        --) # End of all options.
          shift
          break
          ;;
        -*)
          echo "Error: Unknown option: $1" >&2
          exit 1
          ;;
        *)  # No more options
          break
          ;;
      esac
  done

  echo "##octopus[createStepPackageTarget ${parameters}]"
)

# -----------------------------------------------------------------------------
# Function to update progress
#	Accepts 2 arguments:
#	  int: percentage progress
#	  string: message to show
# -----------------------------------------------------------------------------
function update_progress
{
	echo "##octopus[progress percentage='$(encode_servicemessagevalue "$1")' message='$(encode_servicemessagevalue "$2")']"
}

# -----------------------------------------------------------------------------
# Functions write a messages as different levels
# -----------------------------------------------------------------------------
function write_verbose
{
	echo "##octopus[stdout-verbose]"
	echo $1
	echo "##octopus[stdout-default]"
}

function write_highlight
{
	echo "##octopus[stdout-highlight]"
	echo $1
	echo "##octopus[stdout-default]"
}

function write_wait
{
	echo "##octopus[stdout-wait]"
	echo $1
	echo "##octopus[stdout-default]"
}

function write_warning
{
	echo "##octopus[stdout-warning]"
	echo $1
	echo "##octopus[stdout-default]"
}


# -----------------------------------------------------------------------------
# Functions to write the environment information
# -----------------------------------------------------------------------------
function log_environment_information
{
	suppressEnvironmentLogging=$(get_octopusvariable "Octopus.Action.Script.SuppressEnvironmentLogging")
	if [ "$suppressEnvironmentLogging" == "True" ]
	then
		return 0
	fi

	echo "##octopus[stdout-verbose]"
	echo "Bash Environment Information:"
	echo "  OperatingSystem: $(uname -a)"
	echo "  CurrentUser: $(whoami)"
	echo "  HostName: $(hostname)"
	echo "  ProcessorCount: $(getconf _NPROCESSORS_ONLN)"
	currentDirectory="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
	echo "  CurrentDirectory: $currentDirectory"
	tempDirectory=$(dirname $(mktemp -u))
	echo "  TempDirectory: $tempDirectory"
	echo "  HostProcessID: $$"
	echo "##octopus[stdout-default]"
}

log_environment_information

function decrypt_and_parse_variables {
    local encrypted="$1"
    local iv="$2"

    local decrypted
    decrypted=$(decrypt_variable "$encrypted" "$iv")
    declare -gA octopus_parameters=()

    key_byte_lengths=()
    value_byte_lengths=()
    concatenated_hex=""
    while IFS='$' read -r hex_key hex_value; do
        hex_value="${hex_value//$'\n'/}"
        key_byte_len=$(( ${#hex_key} / 2 ))
        value_byte_len=$(( ${#hex_value} / 2 ))
        key_byte_lengths+=("$key_byte_len")
        value_byte_lengths+=("$value_byte_len")
        concatenated_hex+="${hex_key}${hex_value}"
    done <<< "$decrypted"
    decoded_output=$(hex_to_ascii "$concatenated_hex")
    
    section_end=$(date +%s%3N)

    section_start=$(date +%s%3N)
    decoded_keys=()
    decoded_values=()

    # Use a temporary file to avoid subshell issues 
    tmp_file=$(mktemp)
    
    printf "%s" "$decoded_output" > "$tmp_file"
    exec 3<"$tmp_file"

    for idx in "${!key_byte_lengths[@]}"; do
        key_byte_len="${key_byte_lengths[idx]}"
        value_byte_len="${value_byte_lengths[idx]}"

        read -r -N "$key_byte_len" decoded_key <&3
        read -r -N "$value_byte_len" decoded_value <&3

        [[ "$decoded_value" == "nul" ]] && decoded_value=""
        decoded_keys+=("$decoded_key")
        decoded_values+=("$decoded_value")
    done

    exec 3<&-
    rm -f "$tmp_file"

    for i in "${!decoded_keys[@]}"; do
        octopus_parameters["${decoded_keys[$i]}"]="${decoded_values[$i]}"
    done
}

hex_to_ascii() {
    local hex_string="$1"
    hex_string="${hex_string//[$'\t\r\n ']/}"
    # If the length is odd, pad with a leading zero
    if (( ${#hex_string} % 2 )); then
        hex_string="0$hex_string"
    fi
    echo "$hex_string" | awk '
    BEGIN {
      # Build a lookup table for hex digits
      hexmap["0"] = 0; hexmap["1"] = 1; hexmap["2"] = 2; hexmap["3"] = 3;
      hexmap["4"] = 4; hexmap["5"] = 5; hexmap["6"] = 6; hexmap["7"] = 7;
      hexmap["8"] = 8; hexmap["9"] = 9; hexmap["A"] = 10; hexmap["B"] = 11;
      hexmap["C"] = 12; hexmap["D"] = 13; hexmap["E"] = 14; hexmap["F"] = 15;
      hexmap["a"] = 10; hexmap["b"] = 11; hexmap["c"] = 12; hexmap["d"] = 13;
      hexmap["e"] = 14; hexmap["f"] = 15;
    }
    # Define a generic function to convert a hex string to decimal
    function hex2dec(hex,    i, n, d) {
      n = 0;
      for (i = 1; i <= length(hex); i++) {
         d = substr(hex, i, 1);
         n = n * 16 + hexmap[d];
      }
      return n;
    }
    {
      # Process the input string two characters at a time
      for (i = 1; i <= length($0); i += 2)
        printf "%c", hex2dec(substr($0, i, 2));
    }'
}

bashParametersArrayFeatureToggle=#### BashParametersArrayFeatureToggle ####

if [ "$bashParametersArrayFeatureToggle" = true ]; then
    if (( ${BASH_VERSINFO[0]:-0} > 4 || (${BASH_VERSINFO[0]:-0} == 4 && ${BASH_VERSINFO[1]:-0} > 2) )); then
        decrypt_and_parse_variables "#### VARIABLESTRING.ENCRYPTED ####" "#### VARIABLESTRING.IV ####"
    else
        echo "Bash version 4.2 or later is required to use octopus_parameters"
    fi
fi



