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

#stop existing vm
$vm = Get-VM -Name $vmname
if($vm){
    Stop-VM $vm

    while ($vm.State -ne "Off"){
        sleep -s 2
        $vm = Get-VM -Name $vmname
    }
}


#set vm storage...



#restart the vm
if($vm){
    Start-VM $vm
}

