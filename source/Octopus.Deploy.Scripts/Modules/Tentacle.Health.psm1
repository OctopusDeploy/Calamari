function Invoke-HealthCheck()
{
	$hostname = Get-WMIObject Win32_ComputerSystem | Select-Object -ExpandProperty name
	Write-Verbose "Host Name: $hostname"

	$domainName = [Environment]::UserDomainName
	$userName = [Environment]::UserName
	
	$windowsIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
	$windowsPrincipal = new-object 'System.Security.Principal.WindowsPrincipal' $windowsIdentity
     
	$isAdmin = $windowsPrincipal.IsInRole("Administrators")
	Write-Verbose "Running As: $domainName\$userName (Local Administrator: $isAdmin)"
	
	$freeDiskSpaceThreshold = 1024 * 1024 * 1024 * 5L # 5 GB
	Get-WmiObject win32_logicaldisk | ? { $_.DriveType -eq 3 } | % {
		if($_.FreeSpace -lt $freeDiskSpaceThreshold) {
			Write-Warning $("Drive {0} on {1} only has {2} of available free space remaining" -f $_.DeviceID, $hostName, $(Get-FileSizeString $_.FreeSpace))
		} else {
			Write-Host $("Drive {0} has {1} of available free space remaining" -f $_.DeviceID, $(Get-FileSizeString $_.FreeSpace))
		}
	}

	$process = Get-WmiObject Win32_Process -Filter "ProcessID='$($PID)'"
	$parentProcess = Get-WmiObject Win32_Process -Filter "ProcessID='$($process.ParentProcessID)'"
	$version = (Get-Item $parentProcess.Path).VersionInfo.FileVersion
	Set-OctopusVariable "Version" $version
}