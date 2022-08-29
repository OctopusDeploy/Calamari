function Is-DeploymentTypeDisabled($value) {
	return !$value -or ![Bool]::Parse($value)
}
$extractionDir = $OctopusParameters["OctopusOriginalPackageDirectoryPath"]
$vhds = @(Get-ChildItem "$extractionDir\*" -Include *.vhd, *.vhdx)
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
Dismount-DiskImage -ImagePath $vhdPath
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


# Stop VM and Wait for it
Write-Host "Stopping VM $vmname"
Stop-VM -Name $vmname

$attempts = 0
$timeout = New-TimeSpan -minutes 5
$sw = [system.diagnostics.stopwatch]::startNew()

$vm = Get-VM -Name $vmname
while ($vm.State -ne "Off" -and $sw.Elapsed -lt $timeout){
   if($attempts++ -gt 0){
      Write-Host "Waiting for VM $vmname to stop, current state" $vm.State
      sleep -s 5
   }
   $vm = Get-VM -Name $vmname
}

if($vm.State -ne "Off"){
    throw "Unable to stop VM $vmname after 5 minutes"
}


# Swap the drive
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


# Start VM and Wait for it
Write-Host "Starting VM $vmname"
Start-VM -Name $vmname

$attempts = 0
$timeout = New-TimeSpan -minutes 5
$sw = [system.diagnostics.stopwatch]::startNew()

$vm = Get-VM -Name $vmname
while ($vm.State -ne "Running" -and $sw.Elapsed -lt $timeout){
   if($attempts++ -gt 0){
      Write-Host "Waiting for VM $vmname to start, current state" $vm.State
      sleep -s 5
   }
   $vm = Get-VM -Name $vmname
}

if($vm.State -ne "Running"){
    throw "Unable to start VM $vmname after 5 minutes"
}

Write-Host "VM $vmname is Running"

# Wait for heartbeat
Write-Host "Waiting for heartbeat on $vmname"

$attempts = 0
$timeout = New-TimeSpan -minutes 5
$sw = [system.diagnostics.stopwatch]::startNew()

$hb = (Get-VMIntegrationService -vmName $vmname | ?{$_.name -eq "Heartbeat"}).PrimaryStatusDescription
while ($hb -ne "OK" -and $sw.Elapsed -lt $timeout){
   if($attempts++ -gt 0){
      Write-Host "Waiting for heartbeat on $vmname"
      sleep -s 5
   }
   $hb = (Get-VMIntegrationService -vmName $vmname | ?{$_.name -eq "Heartbeat"}).PrimaryStatusDescription
}

if($hb -ne "OK"){
    throw "VM $vmname is running but heartbeat has not returned OK, it may have failed to boot or still be booting"
}

Write-Host "VM $vmname heartbeat is OK"