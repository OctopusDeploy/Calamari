$ErrorActionPreference = "Stop"

if (-not (Get-Command Mount-DiskImage -errorAction SilentlyContinue))
{
    Write-Error "VHD deployment requires Windows Server 2012 or newer"
	exit -1
}

$extractionDir = $OctopusParameters["OctopusOriginalPackageDirectoryPath"]
$vhds = @(Get-ChildItem "$extractionDir\*" -Include *.vhd, *.vhdx)
If($vhds.Length -lt 1)
{
	Write-Error "No VHDs found. A single VHD must be in the root of the package deployed to use this step"
	exit -2
}

If($vhds.Length -gt 1)
{
	Write-Error "More than one VHD found ($vhds.Length). A single VHD must be in the root of the package deployed to use this step"
	exit -3
}

function EvaluateFlagDefaultTrue([string]$flag){
    if(!$flag)
    {
        return $true;
    }

    return [System.Convert]::ToBoolean($flag)
}

# attach a partition to a drive letter
function AddMountPoint($partition){
    try {
        $partition | Add-PartitionAccessPath -AssignDriveLetter | Out-Null
    }
    catch {
        Write-Error "Could not assign a drive letter, ensure that no other VHD with the same partition signature ($($partition.DiskId)) is mounted"
        throw "Could not assign a drive letter: $($_.Exception.Message)"
    }
    $volume = Get-Volume -partition $partition
    $driveLetter = $volume.DriveLetter
    $mountPoint = $driveLetter + ":\"
    return $mountPoint
}

# get the path in the vhd that we will do substitutions on. can be overridden per partition
function GetAdditionalPath([string]$mountPoint, [int]$index){
    If($OctopusParameters["OctopusVhdPartitions[" + $index +"].ApplicationPath"]){
        return split-path $OctopusParameters["OctopusVhdPartitions[" + $index +"].ApplicationPath"] -NoQualifier | % Trim "." | % { join-path -Path $mountPoint -ChildPath $_ }
    }
    If($OctopusParameters["Octopus.Action.Vhd.ApplicationPath"]){
        return split-path $OctopusParameters["Octopus.Action.Vhd.ApplicationPath"] -NoQualifier | % Trim "." | % { join-path -Path $mountPoint -ChildPath $_ }
    }
    return $mountPoint
}

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

$vhdPath = Resolve-Path $vhds[0].FullName
Mount-DiskImage -ImagePath $vhdPath -NoDriveLetter 
$image = Get-DiskImage $vhdPath
$partitions = @(Get-Partition -DiskNumber $image.Number)
$mountPoints =  @()
$additionalPaths = @()
$firstMount = $true

For ($i=0; $i -lt $partitions.Length; $i++){
    If(EvaluateFlagDefaultTrue $OctopusParameters["OctopusVhdPartitions[" + $i +"].Mount"]){

        $mountPoint = AddMountPoint $partitions[$i]
        $mountPoints += $mountPoint

        $additionalPath = GetAdditionalPath $mountPoint $i
        If(Test-Path -Path $additionalPath){
            Write-Host "$additionalPath added to Calamari processing paths"
            $additionalPaths += $additionalPath
        }
        Else{
            Write-Host "$additionalPath not found so not added to Calamari processing paths"
        }

        if($firstMount){
            $firstMount = $false
            Set-OctopusVariable -name "OctopusVhdMountPoint" -value $mountPoint
        }

        Set-OctopusVariable -name "OctopusVhdMountPoint_$i" -value $mountPoint
        Write-Host "VHD partition $i from $vhdPath mounted to $mountPoint"
    }
}

Add-ToAdditionalPaths ($additionalPaths -join ',')