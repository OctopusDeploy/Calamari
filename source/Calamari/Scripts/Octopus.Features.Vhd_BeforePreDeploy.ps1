function EnsureVHDState 
{
    [CmdletBinding(DefaultParametersetName="Mounted")] 
    param(        
        
        [parameter(Mandatory=$false,ParameterSetName = "Mounted")]
        [switch]$Mounted,
        [parameter(Mandatory=$false,ParameterSetName = "Dismounted")]  
        [switch]$Dismounted,
        [parameter(Mandatory=$true)]
        $vhdPath 
        )

        if ( -not ( Get-Module -ListAvailable Hyper-v))
        {
            throw "Hyper-v-Powershell Windows Feature is required to run this resource. Please install Hyper-v feature and try again"
        }
        if ($PSCmdlet.ParameterSetName -eq 'Mounted')
        {
             # Try mounting the VHD.
            $mountedVHD = Mount-VHD -Path $vhdPath -Passthru -ErrorAction SilentlyContinue -ErrorVariable var

            # If mounting the VHD failed. Dismount the VHD and mount it again.
            if ($var)
            {
                Write-Verbose "Mounting Failed. Attempting to dismount and mount it back"
                Dismount-VHD $vhdPath 
                $mountedVHD = Mount-VHD -Path $vhdPath -Passthru -ErrorAction SilentlyContinue

                return $mountedVHD            
            }
            else
            {
                return $mountedVHD
            }
        }
        else
        {
            Dismount-VHD $vhdPath -ea SilentlyContinue
                
        }
}

$vhdPath = $OctopusParameters["Octopus.Action.Vhd.MountPath"]

$mountVHD = EnsureVHDState -Mounted -vhdPath $vhdPath

$mountedDrive =  $mountVHD | Get-Disk | Get-Partition | Get-Volume
$letterDrive  = (-join $mountedDrive.DriveLetter) + ":\"

# write this to a variable...

write-host $letterDrive