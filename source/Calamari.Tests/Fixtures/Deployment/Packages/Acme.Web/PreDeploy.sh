#!/bin/bash
shouldFail = get_octopusvariable "ShouldFail"
if [ $shouldFail = 'yes' ]; then
    echo "You want me to fail"
	exit 1
fi
echo $(get_octopusvariable "PreDeployGreeting") "from PreDeploy.sh"