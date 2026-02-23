#!/bin/bash
# Used for performance testing of decrypt_and_parse_variables.
# Counts the number of entries in octopus_parameters and prints the total,
# along with a spot-check of a known sentinel key to verify correctness.

count=0
for key in "${!octopus_parameters[@]}"; do
    count=$(( count + 1 ))
done

echo "VariableCount=$count"

# Spot-check: BuildPerformanceTestVariables always injects this sentinel.
echo "SpotCheck=${octopus_parameters[PerfSentinel]}"
