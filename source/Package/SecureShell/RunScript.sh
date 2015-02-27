#! /bin/bash -e
# Octopus Linux deployment script
# Version: 1.0.0
# ------------------------------------------------------------------------------
#
# This script is used to control how we deploy packages to Linux
#
PROGNAME=$(basename $0)

# Ensure that our tools folder is on the PATH
export TOOLSPATH=$(dirname $0)
TOOLSPATH=$(pwd)${TOOLSPATH:1}

case ":$PATH:" in
	*:$TOOLSPATH:*) ;;
	*) PATH=$PATH:$TOOLSPATH ;;
esac

export TEMPFILESPATH=$1

echo "Setting up environment for script run"

echo "Setting up Octopus variables"
set +e # disable errexit as the following variables are not required
Octopus_Directory=$(tentacle get "Octopus.Action.Package.Ssh.RootDirectoryPath")
Octopus_Action_Ssh_PackagesDirectoryPath=$(tentacle get "Octopus.Action.Package.Ssh.PackagesDirectoryPath")
Octopus_Action_Ssh_ApplicationsDirectoryPath=$(tentacle get "Octopus.Action.Package.Ssh.ApplicationsDirectoryPath")
Octopus_Project=$(tentacle get "Octopus.Project.Name")
Octopus_Environment=$(tentacle get "Octopus.Environment.Name")
set -e # enable errexit so that we exit if an error occurs

echo "Running the script body"

$TEMPFILESPATH/ScriptBody.sh

if [ $? -ne 0 ]
then
	error_exit $PROGNAME $LINENO "Script failed" $?
fi

echo "Finished script run"