if($ShouldFail -eq "yes") {
	Write-Error "You want me to fail"
}
Write-Host "$PreDeployGreeting from PreDeploy.ps1"
Write-Host "VHD is mounted at $OctopusVhdMountPoint"
Write-Host "VHD partition 0 is mounted at $OctopusVhdMountPoint_0"
Write-Host "VHD partition 1 is mounted at $OctopusVhdMountPoint_1"
