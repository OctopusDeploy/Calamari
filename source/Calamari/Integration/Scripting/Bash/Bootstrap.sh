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
	echo -n "$1" | openssl enc -base64 -A -d
	exit $?
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
	len=$(stat -c%s $1 )


	if [ -z "$ofn" ]
	then
	    ofn=`basename "$pth"`
	fi

	echo "##octopus[createArtifact path='$(encode_servicemessagevalue $pth)' name='$(encode_servicemessagevalue $ofn)' length='$(encode_servicemessagevalue $len)']"

	exit $?
}
