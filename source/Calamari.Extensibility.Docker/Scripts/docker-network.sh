echo "##octopus[stdout-verbose]"
docker -v
echo {{Command}}
echo "##octopus[stdout-default]"

NETWORK_ID=$({{Command}})

rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi

echo "Network Name: $(docker network inspect --format={{.Name}} $NETWORK_ID)"
echo "Network Id: $NETWORK_ID"

inspection=$(docker network inspect --format='{{json .}}' $NETWORK_ID)
echo "##octopus[stdout-verbose]"
echo $inspection
echo "##octopus[stdout-default]"
set_octopusvariable "Docker.Inspect"  "$inspection"