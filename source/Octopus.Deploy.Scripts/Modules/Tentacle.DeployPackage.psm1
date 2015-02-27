Write-Host "This is the main Octopus Deploy package deployer"

# Ensure temporary install directory exists
# Extract NuGet package to above directory
# Run pre-deploy scripts
# Run configuration file conventions etc.
# Run deploy scripts
# Run IIS etc.
# Run post-deploy scripts
# Cleanup

# A few notes:
# - PowerShell scripts need to be invoked in their own PowerShell.exe instances so that they get fresh variable snapshots
# - Output variables from each PS script should be passed into the next PS script and subsequent steps
# - Need to clean up files etc.