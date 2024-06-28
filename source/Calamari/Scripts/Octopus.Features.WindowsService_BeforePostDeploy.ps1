## --------------------------------------------------------------------------------------
## Functions
## --------------------------------------------------------------------------------------

function Start-ServiceInternal($serviceName) {
	Write-Host "Starting the $serviceName service"

	Execute-WithRetry {
		Start-Service $psServiceName
		$service.WaitForStatus('Running', '00:00:30')
	}

	If ($service.Status -ne 'Running')
	{
		Write-Warning "Service $serviceName did not start within 30 seconds"
	} Else {
		Write-Host "Service $serviceName running"
	}
}

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
$desiredStatus = $OctopusParameters["Octopus.Action.WindowsService.DesiredStatus"]
$status = $OctopusParameters["Octopus.Action.WindowsService.Status"]

## --------------------------------------------------------------------------------------
## Run
## --------------------------------------------------------------------------------------

$fullPath = (Resolve-Path $executablePath).ProviderPath

if (!(test-path $fullPath))
{
	Write-Error "The service executable file could not be found: $fullPath"
	exit -1
}

if (!$serviceName)
{
	Write-Error "No service name was specified. Please specify a service name, or disable the Windows Service feature for this project."
	exit -2
}

# Items in the fullArgumentsArray are automatically surrounded by quote when passed to the sc.exe command below
# We need to escape any existing quotes that may exist

if ($arguments)
{
	$binPath = (ConvertTo-ConsoleEscapedArgument ((ConvertTo-QuotedString $fullPath) + " $arguments")) # An extra set of escaped quotes added around the exe
}
else
{
    $binPath = (ConvertTo-ConsoleEscapedArgument ((ConvertTo-QuotedString $fullPath) + " "))
}

$fullArguments = @((ConvertTo-QuotedString (ConvertTo-ConsoleEscapedArgument $serviceName)), "binPath=", $binPath)
if ($displayName)
{
	$fullArguments += @("DisplayName=", (ConvertTo-ConsoleEscapedArgument $displayName))
}

if(!$dependencies)
{
	$dependencies = "/"
}
$fullArguments += @("depend=", (ConvertTo-QuotedString (ConvertTo-ConsoleEscapedArgument $dependencies)))

if ($startMode -and ($startMode -ne 'unchanged'))
{
	$fullArguments += @("start=", (ConvertTo-QuotedString $startMode))
}

$fullArgumentsSafeForConsole = $fullArguments
if ($serviceAccount -ne "_CUSTOM")
{
	if ($serviceAccount)
	{
		$fullArguments += @("obj=", (ConvertTo-QuotedString $serviceAccount))
	}
	$fullArgumentsSafeForConsole = $fullArguments
}

$psServiceName = (ConvertTo-PowershellEscapedArgument $serviceName)

$service = Get-Service $psServiceName -ErrorAction SilentlyContinue

if (!$service)
{
	Write-Host "The $serviceName service does not exist. It will be created."
	Write-Host "sc.exe create $fullArgumentsSafeForConsole"

	& "sc.exe" create ($fullArguments)

	if ($LastExitCode -ne 0) {
		throw "sc.exe create failed with exit code: $LastExitCode"
	}

	$service = Get-Service $psServiceName -ErrorAction SilentlyContinue
}
else
{
	Write-Host "The $serviceName service already exists, it will be reconfigured."

	If ($service.Status -ne 'Stopped')
	{
		Write-Host "Stopping the $serviceName service"

		Execute-WithRetry {
			Stop-Service $psServiceName -Force
			## Wait up to 30 seconds for the service to stop
			$service.WaitForStatus('Stopped', '00:00:30')
		}

		If ($service.Status -ne 'Stopped')
		{
			Write-Warning "Service $serviceName did not stop within 30 seconds"
		} Else {
			Write-Host "Service $serviceName stopped"
		}
	}

	Write-Host "sc.exe config $fullArgumentsSafeForConsole"
	& "sc.exe" config ($fullArguments)

	if ($LastExitCode -ne 0) {
		throw "sc.exe config failed with exit code: $LastExitCode"
	}
}

if ($serviceAccount -eq "_CUSTOM") {
	# dont use sc.exe to set the username / password, as it may be logged to the windows audit log if process creation event logs are enabled 
	Write-Host "Setting custom service credentials for '$serviceName'"
	$wmiService = Get-WmiObject win32_service -filter "name='$($serviceName -replace "'", "\'")'" -computer "."
	if ($customAccountPassword -eq "") {
        $customAccountPassword = $null
    }
	$result = $wmiService.change($null, $null, $null, $null, $null, $null, $customAccountName, $customAccountPassword, $null, $null, $null)
	if ($result.ReturnValue -ne "0") {
		#return codes: https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/change-method-in-class-win32-service#return-value
		throw "Unable to set custom service credentials for service '$serviceName'. Wmi returned $($result.ReturnValue)."
	}
}

if ($description)
{
	Write-Host "Updating the service description"
	$fullArguments = @((ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument $serviceName)), (ConvertTo-ConsoleEscapedArgument $description))
	& "sc.exe" description ($fullArguments)
	if ($LastExitCode -ne 0) {
		throw "sc.exe description failed with exit code: $LastExitCode"
	}
}

<#
    Previously the service was only started based on the start mode. This meant that a start mode of "Unchanged" or
    "Manual" left the service unstarted. This was limiting as an "Unchanged" service would always be left stopped, 
    even if it was running earlier.
    
    Now the status of the service can be specifically defined. If it is defined, we will start the service based
    on the new setting. If it is not defined, we fall back to the original behaviour which started services based
    on the start mode.
#>
if ($desiredStatus -eq "Stopped" -or $startMode -eq "disabled") {
    Write-Host "Leaving the $serviceName service stopped"
} elseif ($desiredStatus -eq "Started") {
    Start-ServiceInternal $serviceName
} elseif ($desiredStatus -eq "Unchanged") {
    # Services that were running or starting are started again after the deployment
    if ($status -eq "Running" -or $status -eq "StartPending") {
        Start-ServiceInternal $serviceName
    } else {
        # There are a lot of other possible states (https://docs.microsoft.com/en-us/dotnet/api/system.serviceprocess.servicecontrollerstatus?view=netframework-4.7.2)
        # We assume they all mean the service should not be started
        Write-Host "Leaving the $serviceName service stopped"
    }
} else {
    # This is the fallback for any steps that don't define a desired status (which is every step before the new
    # desired status option was added). It retains the old behaviour of starting services based on the start mode.

    $wmiServiceName = $serviceName -replace "'", "\'"
    $currentStartMode = Get-WMIObject win32_service -filter "name='$($serviceName -replace "'", "\'")'" -computer "." | select -expand startMode
    
    if ($startMode -eq "unchanged")
    {
        Write-Host "The $serviceName service start mode is set to unchanged, so it won't be started. You will need to start the service manually."
    }
    elseif ($currentStartMode -eq "Disabled")
    {
        Write-Host "The $serviceName service is disabled, so it won't be started."
    }
    elseif ($startMode -eq "demand")
    {
        Write-Host "The $serviceName service is set to 'Manual' start-up, so Octopus won't start it here."
    }
    else
    {
        Start-ServiceInternal $serviceName
    }
}