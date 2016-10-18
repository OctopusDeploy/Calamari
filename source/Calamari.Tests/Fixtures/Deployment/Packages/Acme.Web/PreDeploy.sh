#!/bin/bash
if [ $(get_octopusvariable "ShouldFail") == "yes" ]; then
    echo "You want me to fail"
	exit 1
fi
echo $(get_octopusvariable "PreDeployGreeting") "from PreDeploy.sh"
