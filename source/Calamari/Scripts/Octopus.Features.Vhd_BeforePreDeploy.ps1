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
$mountedDrive = ""
$attempts = 0

# retry
while ([string]::IsNullOrEmpty($mountedDrive)){
	if($attempts -ge 5){
		Write-Error "Unable to mount VHD"
		exit -3
	}

	$attempts = $attempts + 1
    Try{
		Mount-VHD $vhdPath -ErrorAction SilentlyContinue
        $image = Get-DiskImage $vhdPath -ErrorAction SilentlyContinue
        $disk = Get-Disk -number $image.Number -ErrorAction SilentlyContinue
        $partition = Get-Partition -disk $disk -ErrorAction SilentlyContinue
        $volume = Get-Volume -partition $partition -ErrorAction SilentlyContinue
        $mountedDrive = $volume.DriveLetter
    }
    Catch{}
    sleep -Seconds 2
}
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

Add-ToAdditionalPaths $letterDrive
Write-Host "VHD at $vhdPath mounted to $letterDrive"