param (
    [Parameter(Mandatory=$true)][string]$vhdpath,
    [Parameter(Mandatory=$true)][string]$copyfolder
 )

$drive = (New-VHD -path $vhdpath -SizeBytes 10MB -Fixed | `
    Mount-VHD -Passthru |  `
    get-disk -number {$_.DiskNumber} | `
    Initialize-Disk -PartitionStyle MBR -PassThru | `
    New-Partition -UseMaximumSize -AssignDriveLetter:$False -MbrType IFS | `
    Format-Volume -Confirm:$false -FileSystem NTFS -force | `
    get-partition | `
    Add-PartitionAccessPath -AssignDriveLetter -PassThru | `
    get-volume).DriveLetter 

$drive = $drive + ":\"
Copy-Item -Path $copyFolder -Destination $drive –Recurse
 
Dismount-VHD $vhdpath
