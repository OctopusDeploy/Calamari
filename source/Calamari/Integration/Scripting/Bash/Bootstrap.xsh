#!/bin/bash
# Octopus Linux helper function script
# Version: 1.0.0
# -----------------------------------------------------------------------------

# -----------------------------------------------------------------------------
# Function to base64 decode a service message value
#		Accepts 1 argument:
#			string: the value to decode
# -----------------------------------------------------------------------------
function decode_servicemessagevalue
{
	echo -n "$1" | openssl enc -base64 -d
	exit $?
}

#	---------------------------------------------------------------------------
# Function for getting an octopus variable
#   Accepts 1 argument:
#     string: value of the name of the octopus variable
#	---------------------------------------------------------------------------

function get_octopusvariable
{
  INPUT=$(echo -n "$1" | openssl enc -base64 -A)
  case $INPUT in
#### VariableDeclarations ####
    *)
      echo "Unrecognized command \"$1\""
    ;;
    esac
}
