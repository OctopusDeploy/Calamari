param(
  [string]$OctopusKey
)

$ErrorActionPreference = 'Stop'

$powershellEngine = [powershell]::Create([System.Management.Automation.RunspaceMode]::NewRunspace)

Write-Host "##octopus[stdout-warning]"
Write-Host "The Powershell execution engine is waiting for a PowerShell script debugger to attach."
Write-Host "##octopus[stdout-default]"
Write-Host "Use the following commands to begin debugging this script:"
Write-Host "Enter-PSSession -ComputerName $($env:computername) -Credential `<credentials`>"
Write-Host "Enter-PSHostProcess -Id $pid"
Write-Host "Debug-Runspace -Id $($powershellEngine.Runspace.Id)"
Write-Host ""
Write-Host "For more information, please see https://g.octopushq.com/DebuggingPowershellScripts."

$powershellEngine.AddScript(". '{{BootstrapFile}}' $OctopusKey").Invoke()

Write-Host "Debugging session ended"
