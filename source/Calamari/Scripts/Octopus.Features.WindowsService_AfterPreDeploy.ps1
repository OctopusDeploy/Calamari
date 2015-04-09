## --------------------------------------------------------------------------------------
## Configuration
## --------------------------------------------------------------------------------------

$isEnabled = $OctopusParameters["Octopus.Action.WindowsService.CreateOrUpdateService"]
if (!$isEnabled -or ![Bool]::Parse($isEnabled)) 
{
   exit 0
}

$serviceName = $OctopusParameters["Octopus.Action.WindowsService.ServiceName"]
$displayName = $OctopusParameters["Octopus.Action.WindowsService.DisplayName"]
$executablePath = $OctopusParameters["Octopus.Action.WindowsService.ExecutablePath"]
$arguments = $OctopusParameters["Octopus.Action.WindowsService.Arguments"]
$startMode = $OctopusParameters["Octopus.Action.WindowsService.StartMode"]
$serviceAccount = $OctopusParameters["Octopus.Action.WindowsService.ServiceAccount"]
$customAccountName = $OctopusParameters["Octopus.Action.WindowsService.CustomAccountName"]
$customAccountPassword = $OctopusParameters["Octopus.Action.WindowsService.CustomAccountPassword"]
$dependencies = $OctopusParameters["Octopus.Action.WindowsService.Dependencies"]
$description = $OctopusParameters["Octopus.Action.WindowsService.Description"]

## --------------------------------------------------------------------------------------
## Run
## --------------------------------------------------------------------------------------

if (!$serviceName)
{
	Write-Error "No service name was specified. Please specify a service name, or disable the Windows Service feature for this project."
	exit -2
}

$service = Get-Service $ServiceName -ErrorAction SilentlyContinue

if (!$service)
{
    Write-Host "The $serviceName service does not exist yet"
}
else
{
    Write-Host "The $serviceName service already exists; it will be stopped"
    Write-Host "Stopping the $serviceName service"

    Stop-Service $ServiceName -Force
}
