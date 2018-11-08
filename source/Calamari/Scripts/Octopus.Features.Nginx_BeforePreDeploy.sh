#!/bin/bash

function check_user_has_sudo_access_without_password_prompt {
    sudo -n cp --help > /dev/null 2>&1 && sudo -n rm --help > /dev/null 2>&1 && sudo -n nginx -h > /dev/null 2>&1
    if [[ $? -ne 0 ]]; then
        echo >&2 "User requires 'sudo' access without password to the 'cp', 'rm' and 'nginx' commands."
        echo >&2 "See https://g.octopushq.com/NginxPermissions for more information."
        exit 1
    fi
}

function check_app_exists {
	command -v $1 > /dev/null 2>&1
	if [[ $? -ne 0 ]]; then
		fail_step "The executable $1 does not exist, or is not on the path"
	fi
}

check_user_has_sudo_access_without_password_prompt
check_app_exists nginx