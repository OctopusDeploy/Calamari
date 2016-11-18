$vhds = @(Get-ChildItem * -Include *.vhd, *.vhdx)

Foreach($vhd in $vhds)
{
	Dismount-VHD $vhd -ErrorAction Continue
	Write-Host  "VHD at $vhdPath dismounted"
}