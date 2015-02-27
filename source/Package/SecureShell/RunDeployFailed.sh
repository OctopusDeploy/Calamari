#! /bin/bash

PROGNAME=$(basename $0)
source $(dirname $0)/functions

deployFailed="$1/DeployFailed.sh"
if [ -f $deployFailed ]
then
	echo "Running deployfailed script"
	chmod u+rx $deployFailed
	$deployFailed
	if [ $? -ne 0 ]
	then
		error_exit $PROGNAME $LINENO "deployfailed script failed" $?
	fi
fi
