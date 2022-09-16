param(
  [string]$OctopusKey
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 5)
{
    throw "PowerShell debugging is only supported in PowerShell versions 5 and above. This server is currently running PowerShell version $($PSVersionTable.PSVersion.ToString())."
}

$powershellEngine = [powershell]::Create([System.Management.Automation.RunspaceMode]::NewRunspace)

Write-Host "##octopus[stdout-warning]"
Write-Host "The Powershell execution engine is waiting for a PowerShell script debugger to attach."
Write-Host "##octopus[stdout-default]"
Write-Host ""
Write-Host "If you want to connect remotely, and you have PSRemoting set up, use the following commands to begin remotely debugging this script:"
Write-Host "Enter-PSSession -ComputerName $($env:computername) -Credential `<credentials`>"
Write-Host "Enter-PSHostProcess -Id $pid"
Write-Host "Debug-Runspace -Id $($powershellEngine.Runspace.Id)"
Write-Host ""
Write-Host "Otherwise, RDP to the server, and use the following commands to begin debugging this script:"
Write-Host "Enter-PSHostProcess -Id $pid"
Write-Host "Debug-Runspace -Id $($powershellEngine.Runspace.Id)"
Write-Host ""
Write-Host "For more information, please see https://g.octopushq.com/DebuggingPowershellScripts."

$powershellEngine.AddScript(". '{{BootstrapFile}}' $OctopusKey").Invoke()

Write-Host "Debugging session ended"
