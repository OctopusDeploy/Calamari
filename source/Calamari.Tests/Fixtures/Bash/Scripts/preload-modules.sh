#!/bin/bash

# Test that preloaded modules are available
echo "Calling test_function from preloaded module..."
test_function

echo "Checking preloaded variable..."
echo "PRELOADED_VAR=$PRELOADED_VAR"
