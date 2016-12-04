echo "##octopus[stdout-verbose]"
docker -v
echo {{Command}}
echo "##octopus[stdout-default]"

CONTAINER_ID=$({{Command}})

rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi

echo "Container Name: $(docker inspect --format='{{.Name}}' $CONTAINER_ID)"
echo "Container Id: $CONTAINER_ID"

inspection=$(docker inspect --format='{{json .}}' $CONTAINER_ID)
echo "##octopus[stdout-verbose]"
echo $inspection
echo "##octopus[stdout-default]"
set_octopusvariable "Docker.Inspect"  "$inspection"