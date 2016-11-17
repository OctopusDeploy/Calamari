## --------------------------------------------------------------------------------------
## Configuration
## --------------------------------------------------------------------------------------

function Is-DeploymentTypeDisabled($value) {
	return !$value -or ![Bool]::Parse($value)
}

$deployVhdToVm = !(Is-DeploymentTypeDisabled $OctopusParameters["Octopus.Action.Vhd.DeployVhdToVm"])
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

