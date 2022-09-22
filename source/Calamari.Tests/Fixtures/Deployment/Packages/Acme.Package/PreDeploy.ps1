#
# PreDeploy.ps1
#
write-host "hello from pre-deploy, skipping remaining conventions"
Set-OctopusVariable 'Octopus.Action.SkipRemainingConventions' 'True'