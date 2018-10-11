#!/bin/bash

function check_app_exists {
	command -v $1 > /dev/null 2>&1
	if [[ $? -ne 0 ]]; then
		fail_step "The executable $1 does not exist, or is not on the path"
	fi
}

check_app_exists nginx