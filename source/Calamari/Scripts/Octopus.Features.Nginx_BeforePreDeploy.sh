#!/bin/bash

# Enter any commands that are required by the NGINX feature in a way the program doesn't do anything, like displaying the commands help
requiredCommandsToCheck="
cp --help
mv --help
rm --help
systemctl --help
nginx -h
"

function check_user_has_sudo_access_without_password_to_command {
    sudo -n $1 $2 > /dev/null 2>&1
    if [[ $? -ne 0 ]]; then
        echo >&2 "User does not have 'sudo' access without password to command '$1'"
        failedSudoCheck=1
    fi
}

function check_user_has_sudo_access_without_password_to_required_commands {
    IFS=$'\n' read -rd '' -a cmdArr <<< "$requiredCommandsToCheck"
    
    failedSudoCheck=0
    for cmd in "${cmdArr[@]}"
    do
        check_user_has_sudo_access_without_password_to_command ${cmd}
    done || exit $?
    
    if [[ $failedSudoCheck -ne 0 ]]; then
        echo >&2 "User does not have the required 'sudo' access without password."
        echo >&2 "See https://g.octopus.hq.com/NginxInstall from more information"
        exit 1
    fi
}

function check_app_exists {
	command -v $1 > /dev/null 2>&1
	if [[ $? -ne 0 ]]; then
		echo >&2 "The executable $1 does not exist, or is not on the path"
		exit 1
	fi
}

check_app_exists nginx
check_user_has_sudo_access_without_password_to_required_commands
