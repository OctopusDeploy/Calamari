#!/bin/bash

set -euo pipefail

OPERATION="$1"

# Get the Calamari executable path from environment variable
if [ -z "${OCTOPUS_CALAMARI_EXECUTABLE:-}" ]; then
    echo "OCTOPUS_CALAMARI_EXECUTABLE environment variable not set" >&2
    exit 1
fi

CALAMARI_EXE="$OCTOPUS_CALAMARI_EXECUTABLE"
exec "$CALAMARI_EXE" docker-credential --operation="$OPERATION"