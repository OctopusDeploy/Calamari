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

Mount-DiskImage -ImagePath $vhdPath
 
$image = Get-DiskImage $vhdPath
$partition = Get-Partition -DiskNumber $image.Number
$volume = Get-Volume -partition $partition
$mountedDrive = $volume.DriveLetter
$letterDrive  = $mountedDrive + ":\"

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
    $path = split-path $OctopusParameters["Octopus.Action.Vhd.ApplicationPath"] -NoQualifier | % Trim "." | % { join-path -Path $letterDrive -ChildPath $_ }
} Else {
    $path = $letterDrive
}

Add-ToAdditionalPaths $path
Set-OctopusVariable -name "VhdMountPoint" -value $letterDrive
Write-Host "VHD at $vhdPath mounted to $letterDrive"
