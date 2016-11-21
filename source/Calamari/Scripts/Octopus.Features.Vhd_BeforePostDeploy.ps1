function Is-DeploymentTypeDisabled($value) {
	return !$value -or ![Bool]::Parse($value)
}

$vhds = @(Get-ChildItem * -Include *.vhd, *.vhdx)
If($vhds.Length -lt 1)
{
	Write-Error "No VHDs found. A single VHD must be in the root of the package deployed to use this step"
	exit -1
}

If($vhds.Length -gt 1)
{
	Write-Error "More than one VHD found. A single VHD must be in the root of the package deployed to use this step"
	exit -2
}

# Dismount
$vhdPath = $vhds[0].FullName
Dismount-VHD $vhdPath
Write-Host "VHD at $vhdPath dismounted"

$deployVhdToVm = !(Is-DeploymentTypeDisabled $OctopusParameters["Octopus.Action.Vhd.DeployVhdToVm"])
if(-Not $deployVhdToVm){
	exit 0
}

$vmname = $OctopusParameters["Octopus.Action.Vhd.VmName"]

if(!$vmname){
	Write-Error "Deploy VHD to VM enabled, but no VM Name set"
	exit -3
}


Write-Host "Stopping VM $vmname"
$vm = Get-VM -Name $vmname
Stop-VM $vm

$attempts = 0
while ($vm.State -ne "Off"){
   sleep -s 5
   Write-Host "Waiting for VM $vmname to stop"
   $vm = Get-VM -Name $vmname
}

$existingDrive = @(GET-VMHardDiskDrive -VM $vm) | Select -First 1

if($existingDrive){

    write-host "Removing existing drive"
    Remove-VMHardDiskDrive -VMName $vmname -ControllerType $existingDrive.ControllerType `
                                   -ControllerNumber $existingDrive.ControllerNumber `
                                   -ControllerLocation $existingDrive.ControllerLocation

    write-host "Adding new drive"
    Add-VMHardDiskDrive -VMName $vmname -Path $vhdpath `
                                   -ControllerType $existingDrive.ControllerType `
                                   -ControllerNumber $existingDrive.ControllerNumber `
                                   -ControllerLocation $existingDrive.ControllerLocation

} Else {
    write-host "Adding new drive"
    Add-VMHardDiskDrive -VMName $vmname -Path $vhdpath
}

write-host "Starting VM"

#restart the vm
if($vm){
    Start-VM $vm
}

