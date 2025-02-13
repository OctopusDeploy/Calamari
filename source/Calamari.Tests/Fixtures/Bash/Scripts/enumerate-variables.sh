#!/bin/bash

for key in "${!octopus_parameters[@]}"; do
    value="${octopus_parameters[$key]}"
    echo "Key: $key, Value: $value"
done
