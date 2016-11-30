$ErrorActionPreference = "Stop"

$vhds = @(Get-ChildItem * -Include *.vhd, *.vhdx)
If($vhds.Length -lt 1)
{
	Write-Error "No VHDs found. A single VHD must be in the root of the package deployed to use this step"
	exit -1
}

If($vhds.Length -gt 1)
{
	Write-Error "More than one VHD found ($vhds.Length). A single VHD must be in the root of the package deployed to use this step"
	exit -2
}

$vhdPath = Resolve-Path $vhds[0].FullName

$driveLetter = (Mount-DiskImage -ImagePath $vhdPath -PassThru `
    | Get-DiskImage `
    | Get-Disk `
    | Get-Partition).DriveLetter + ":\"

# append a record to the AdditionalPaths list 
function Add-ToAdditionalPaths([string]$new) {
	$current = $OctopusParameters["Octopus.Action.AdditionalPaths"]
    If([string]::IsNullOrEmpty($current)){
        $current = $new
    } Else {
        $current = $current + "," + $new
    }
	Set-OctopusVariable -name "Octopus.Action.AdditionalPaths" -value $current
}

If($OctopusParameters["Octopus.Action.Vhd.ApplicationPath"]){
    $path = split-path $OctopusParameters["Octopus.Action.Vhd.ApplicationPath"] -NoQualifier | % Trim "." | % { join-path -Path $driveLetter -ChildPath $_ }
} Else {
    $path = $driveLetter
}

Add-ToAdditionalPaths $path
Set-OctopusVariable -name "VhdMountPoint" -value $driveLetter
Write-Host "VHD at $vhdPath mounted to $driveLetter"
