#!/bin/bash
# Octopus Linux helper function script
# Version: 1.0.0
# -----------------------------------------------------------------------------
 
# -----------------------------------------------------------------------------
# Function to base64 encode a service message value
#		Accepts 1 argument:
#			string: the value to encode
# -----------------------------------------------------------------------------
function encode_servicemessagevalue
{
	echo -n "$1" | openssl enc -base64 -A
	exit $?
}

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

#	---------------------------------------------------------------------------
# Function for setting an octopus variable
#   Accepts 2 arguments:
#     string: value of the name of the octopus variable
#     string: value of the value of the octopus variable
#	---------------------------------------------------------------------------
function set_octopusvariable
{
	MESSAGE="##octopus[setVariable"

	if [ -n "$1" ]
	then
		MESSAGE="$MESSAGE name='$(encode_servicemessagevalue $1)'"
	fi

	if [ -n "$2" ]
	then
		MESSAGE="$MESSAGE value='$(encode_servicemessagevalue $2)'"
	fi

	MESSAGE="$MESSAGE]"

	echo $MESSAGE
	exit $?
}
