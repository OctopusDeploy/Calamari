########################
# Helper script for running local Calamari builds which does the following:
# * Skips packing test projects
# * Runs pack of nuget packages for runtimes in parallel for better performance
# * Appends a timestamp to the nuget version so don't need to commit each time to get a unique package version
# * Sets the octopus server version for you
#
# The intention of this is not to replace anything the existing build script does, but to reduce the inner feedback loop
# for Calamari changes getting across to Server as part of dev testing.
########################
./build.ps1 -Target Local -BuildVerbosity Minimal -PackInParallel -Timestamp -SetOctopusServerVersion