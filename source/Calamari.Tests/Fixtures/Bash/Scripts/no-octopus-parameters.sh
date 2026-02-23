#!/bin/bash
# Script that deliberately does NOT use octopus_parameters
# Used to test that the array is not loaded when not needed

# Use get_octopusvariable instead (uses the switch statement, not the array)
name=$(get_octopusvariable "PerfSentinel")
echo "ScriptRan=true"
echo "SentinelValue=$name"
