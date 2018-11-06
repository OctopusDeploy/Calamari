#!/bin/bash
for variablename in "${OCTOPUS_VARIABLENAMES[@]}"
do
  echo ${variablename} = $( get_octopusvariable "${variablename}")
done

