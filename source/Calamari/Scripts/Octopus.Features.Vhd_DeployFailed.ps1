if (Get-Command Dismount-DiskImage -errorAction SilentlyContinue)
{
    $vhds = @(Get-ChildItem * -Include *.vhd, *.vhdx)

    Foreach($vhd in $vhds)
    {
	    Dismount-DiskImage -ImagePath $vhd -ErrorAction Continue
	    Write-Host  "VHD at $vhdPath dismounted"
    }
}