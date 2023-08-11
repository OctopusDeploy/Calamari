## --------------------------------------------------------------------------------------
## Configuration
## --------------------------------------------------------------------------------------

function Is-DeploymentTypeDisabled($value) {
	return !$value -or ![Bool]::Parse($value)
}

$deployAsWebSite = !(Is-DeploymentTypeDisabled $OctopusParameters["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"])
$deployAsWebApplication = !(Is-DeploymentTypeDisabled $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.CreateOrUpdate"])
$deployAsVirtualDirectory = !(Is-DeploymentTypeDisabled $OctopusParameters["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"])


if (!$deployAsVirtualDirectory -and !$deployAsWebSite -and !$deployAsWebApplication)
{
	Write-Host "Skipping IIS deployment. Neither Website nor Virtual Directory nor Web Application deployment type has been enabled." 
	exit 0
}

try {
	$iisFeature = Get-WindowsFeature Web-WebServer -ErrorAction Stop
	if ($iisFeature -eq $null -or $iisFeature.Installed -eq $false) {
		Write-Error "It looks like IIS is not installed on this server and the deployment is likely to fail."
		Write-Error "Tip: You can use PowerShell to ensure IIS is installed: 'Install-WindowsFeature Web-WebServer'"
		Write-Error "     You are likely to want more IIS features than just the web server. Run 'Get-WindowsFeature *web*' to see all of the features you can install."
		exit 1
	}
	else {
		$iisVersion = Get-ItemProperty HKLM:\SOFTWARE\Microsoft\InetStp\  | Select VersionString
		Write-Verbose "Detected IIS $($iisVersion.VersionString)"
	}
} catch {
	Write-Verbose "Call to `Get-WindowsFeature Web-WebServer` failed."
	Write-Verbose "Unable to determine if IIS is installed on this server but will optimistically continue."
}

try {
	Add-PSSnapin WebAdministration -ErrorAction Stop
} catch {
    try {
		 Import-Module WebAdministration -ErrorAction Stop
		} catch {
			Write-Warning "We failed to load the WebAdministration module. This usually resolved by doing one of the following:"
			Write-Warning "1. Install IIS via Add Roles and Features, Web Server (IIS)"
			Write-Warning "2. Install .NET Framework 3.5.1"
			Write-Warning "3. Upgrade to PowerShell 3.0 (or greater)"
			Write-Warning "4. On Windows 2008 you might need to install PowerShell SnapIn for IIS from http://www.iis.net/downloads/microsoft/powershell#additionalDownloads"
			throw ($error | Select-Object -First 1)
    }
}

function Wait-OnMutex {
	param(
	[parameter(Mandatory = $true)][string] $mutexId
	)

	Try	{
		$mutex = New-Object System.Threading.Mutex -ArgumentList false, $mutexId

		while (-not $mutex.WaitOne(5000))
		{
			Write-Verbose "Cannot start this IIS website related task yet. There is already another task running that cannot be run in conjunction with any other task. Please wait..."
		}
		
		Write-Verbose "Acquired mutex $mutexId"
		return $mutex
	}
	Catch [System.Threading.AbandonedMutexException] {
		return Wait-OnMutex $mutexId
	}
	Catch [System.SystemException]{
		Write-Verbose "Wait-OnMutex had a major issue, possibly not running with sufficient privileges recover the mutex, details: $_.Exception.Message"
	}
}

function Determine-Path($path) {
	if (!$path) {
		$path = "."
	}

	return (resolve-path $path).ProviderPath
}

$maxFailures = $OctopusParameters["Octopus.Action.IISWebSite.MaxRetryFailures"]
if ($maxFailures -Match "^\d+$") {
	$maxFailures = [int]$maxFailures
} else {
	$maxFailures = 5
}

$sleepBetweenFailures = $OctopusParameters["Octopus.Action.IISWebSite.SleepBetweenRetryFailuresInSeconds"]
if ($sleepBetweenFailures -Match "^\d+$") {
	$sleepBetweenFailures = [int]$sleepBetweenFailures
} else {
	$sleepBetweenFailures = Get-Random -minimum 1 -maximum 4
}

if ($sleepBetweenFailures -gt 60) {
	Write-Host "Invalid Sleep time between failures.  Setting to max of 60 seconds"
	$sleepBetweenFailures = 60
}

# Not available on Server 2008
$hasWebCommitDelay = $false
If(Get-Command "Start-WebCommitDelay" -ErrorAction SilentlyContinue){
    $hasWebCommitDelay = $true
}
	
function Execute-WithRetry([ScriptBlock] $command, $noLock) {

	function Core-Execute-WithRetry([ScriptBlock] $command) {
		$attemptCount = 0
		$operationIncomplete = $true
	
		while ($operationIncomplete -and $attemptCount -lt $maxFailures) {
			$attemptCount = ($attemptCount + 1)
	
			if ($attemptCount -ge 2) {
				Write-Host "Waiting for $sleepBetweenFailures seconds before retrying..."
				Start-Sleep -s $sleepBetweenFailures
				Write-Host "Retrying..."
			}
	
			try {
				& $command
	
				$operationIncomplete = $false
			} catch [System.Exception] {
	            if($hasWebCommitDelay){
	                Stop-WebCommitDelay -Commit $false -ErrorAction SilentlyContinue
	            }
				if ($attemptCount -lt ($maxFailures)) {
					Write-Host ("Attempt $attemptCount of $maxFailures failed: " + $_.Exception.Message)
				} else {
					throw
				}
			}
		}
	}

	if($noLock) {
		Core-Execute-WithRetry -command $command
	} else {
		$mutexId = 'Global\Octopus-IIS-Metabase-Mutex'
		$mutex = Wait-OnMutex $mutexId
		Try {
			Core-Execute-WithRetry -command $command
		}
		Finally {
			$mutex.ReleaseMutex()
			$mutex.Close()
		}
	}
}

function SetUp-ApplicationPool($applicationPoolName, $applicationPoolIdentityType, 
								$applicationPoolUsername, $applicationPoolPassword,
								$applicationPoolFrameworkVersion,  $startPool) 
{

	$appPoolPath = ("IIS:\AppPools\" + $applicationPoolName)

	# Set App Pool
	Execute-WithRetry { 
		Write-Verbose "Loading Application pool"
		$exists = Test-Path $appPoolPath -ErrorAction SilentlyContinue
		if (!$exists) { 
			Write-Host "Application pool `"$applicationPoolName`" does not exist, creating..." 
			New-Item $appPoolPath -confirm:$false
		} else {
			Write-Host "Application pool `"$applicationPoolName`" already exists"
		}
        # Confirm it's there. Get-Item can pause if the app-pool is suspended, so use Get-WebAppPoolState
        $pool = Get-WebAppPoolState $applicationPoolName

		if ($startPool -eq $false) {
			if ($pool.Value -eq "Started") {
				Write-Host "Application pool is started. Attempting to stop..."
				Stop-WebAppPool $applicationPoolName
			}
		}
	}

	# Set App Pool Identity
	Execute-WithRetry { 
		Write-Host "Set application pool identity: $applicationPoolIdentityType"
		if ($applicationPoolIdentityType -eq "SpecificUser") {
			Set-ItemProperty $appPoolPath -name processModel -value @{identitytype="SpecificUser"; username="$applicationPoolUsername"; password="$applicationPoolPassword"}
		} else {
			Set-ItemProperty $appPoolPath -name processModel -value @{identitytype="$applicationPoolIdentityType"}
		}
	}

	# Set .NET Framework
	Execute-WithRetry { 
		Write-Host "Set .NET framework version: $applicationPoolFrameworkVersion" 
		if($applicationPoolFrameworkVersion -eq "No Managed Code")
		{
			Set-ItemProperty $appPoolPath managedRuntimeVersion ""
		}
		else
		{
			Set-ItemProperty $appPoolPath managedRuntimeVersion $applicationPoolFrameworkVersion
		}
	}
}

function Assign-ToApplicationPool($iisPath, $applicationPoolName) {
	Execute-WithRetry { 
		Write-Verbose "Loading Site"
		$pool = Get-ItemProperty $iisPath -name applicationPool
		if ($applicationPoolName -ne $pool) {
			Write-Host "Assigning `"$iisPath`" to application pool `"$applicationPoolName`"..."
			Set-ItemProperty $iisPath -name applicationPool -value $applicationPoolName
		} else {
			Write-Host "Application pool `"$applicationPoolName`" already assigned to `"$iisPath`""
		}
	}
}

function Start-ApplicationPool($applicationPoolName) {
	# It can take a while for the App Pool to come to life (#490)
	Start-Sleep -s 1

	# Start App Pool
	Execute-WithRetry { 
		$state = Get-WebAppPoolState $applicationPoolName
		if ($state.Value -eq "Stopped") {
			Write-Host "Application pool is stopped. Attempting to start..."
			Start-WebAppPool $applicationPoolName
		}
	} -noLock $true
}

function Get-FullPath($root, $segments)
{
	return $root +  "\" + ($segments -join "\")
}

function Assert-ParentSegmentsExist($sitePath, $virtualPathSegments) {
	$fullPathToVirtualPathSegment = $sitePath
	for($i = 0; $i -lt $virtualPathSegments.Length - 1; $i++) {
		$fullPathToVirtualPathSegment = $fullPathToVirtualPathSegment + "\" + $virtualPathSegments[$i]
		$segment = Get-Item $fullPathToVirtualPathSegment -ErrorAction SilentlyContinue
		if (!$segment) {
			$fullPath = Get-FullPath -root $sitePath -segments $virtualPathSegments
			throw "Virtual path `"$fullPathToVirtualPathSegment`" does not exist. Please make sure all parent segments of $fullPath exist."
		}
	}
}

function Assert-WebsiteExists($SitePath, $SiteName)
{
	Execute-WithRetry { 
		Write-Verbose "Looking for the parent Site `"$SiteName`" at `"$SitePath`"..."
		$site = Get-Item $SitePath -ErrorAction SilentlyContinue
		if (!$site) 
		{ 
			throw "The Web Site `"$SiteName`" does not exist in IIS and this step cannot create the Web Site because the necessary details are not available. Add a step which makes sure the parent Web Site exists before this step attempts to add a child to it." 
		}
	}
}

function Convert-ToPathSegments($VirtualPath)
{
	return $VirtualPath.Split(@('\', '/'), [System.StringSplitOptions]::RemoveEmptyEntries)
}

function Set-Path($virtualPath, $physicalPath)
{
	Execute-WithRetry { 
		Write-Host ("Setting physical path of $virtualPath to $physicalPath")
		Set-ItemProperty $virtualPath -name physicalPath -value "$physicalPath"
	}
}

function Is-Directory($Path){
	return Test-Path -Path $Path -PathType Container
}

if ($deployAsVirtualDirectory) 
{
	$webSiteName = $OctopusParameters["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"]
	$physicalPath = Determine-Path $OctopusParameters["Octopus.Action.IISWebSite.VirtualDirectory.PhysicalPath"]
	$virtualPath = $OctopusParameters["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"]

	Write-Host "Making sure a Virtual Directory `"$virtualPath`" is configured as a child of `"$webSiteName`" at `"$physicalPath`"..."
    
    pushd IIS:\

	$sitePath = "IIS:\Sites\$webSiteName"

	Assert-WebsiteExists -SitePath $sitePath -SiteName $webSiteName

	[array]$virtualPathSegments =  Convert-ToPathSegments -VirtualPath $virtualPath
	Assert-ParentSegmentsExist -sitePath $sitePath -virtualPathSegments $virtualPathSegments

	$fullPathToLastVirtualPathSegment = Get-FullPath -root $sitePath -segments $virtualPathSegments
	$lastSegment = Get-Item $fullPathToLastVirtualPathSegment -ErrorAction SilentlyContinue

	if (!$lastSegment) {
		Write-Host "`"$virtualPath`" does not exist. Creating Virtual Directory pointing to $fullPathToLastVirtualPathSegment ..."
		Execute-WithRetry { 
			New-Item $fullPathToLastVirtualPathSegment -type VirtualDirectory -physicalPath $physicalPath
		}
	} else {
		if ($lastSegment.ElementTagName -eq 'virtualDirectory') {
			Write-Host "Virtual Directory `"$virtualPath`" already exists, no need to create it."
		} elseif ($lastSegment.ElementTagName -eq 'application') {
			# It looks like the only reliable way to do the conversion is to delete the exsting application and then create a new virtual directory. http://stackoverflow.com/questions/16738995/powershell-convertto-webapplication-on-iis
			# We don't want to delete anything as the customer might have handcrafted the settings and has no way of retrieving them.
			throw "`"$virtualPath`" already exists in IIS and points to a Web Application. We cannot automatically change this to a Virtual Directory on your behalf. Please delete it and then re-deploy the project."
		} else {
			if (!(Is-Directory -Path $physicalPath)) {
				throw "`"$virtualPath`" already exists in IIS and points to an unknown item which isn't a directory. Please delete it and then re-deploy the project. If you used the Custom Installation Directory feature to target this path we recommend removing the Custom Installation Directory feature, instead allowing Octopus to unpack the files into the default location and update the Physical Path of the Virtual Directory on your behalf."
			}
			
			Write-Host "`"$virtualPath`" already exists in IIS and points to an unknown item which seems to be a directory. We will try to convert it to a Virtual Directory. If you used the Custom Installation Directory feature to target this path we recommend removing the Custom Installation Directory feature, instead allowing Octopus to unpack the files into the default location and update the Physical Path of the Virtual Directory on your behalf."
			Execute-WithRetry { 
				New-Item $fullPathToLastVirtualPathSegment -type VirtualDirectory -physicalPath $physicalPath
			}
		}

		Set-Path -virtualPath $fullPathToLastVirtualPathSegment -physicalPath $physicalPath
	}

    popd	
} 

if ($deployAsWebApplication)
{
	$webSiteName = $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.WebSiteName"]
	$physicalPath = Determine-Path $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.PhysicalPath"]
	$virtualPath = $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.VirtualPath"]
	$startAppPool = if ($OctopusParameters.ContainsKey("Octopus.Action.IISWebSite.StartApplicationPool")) { $OctopusParameters["Octopus.Action.IISWebSite.StartApplicationPool"] } else { $true }

	Write-Host "Making sure a Web Application `"$virtualPath`" is configured as a child of `"$webSiteName`" at `"$physicalPath`"..."
    
    pushd IIS:\

	$applicationPoolName = $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolName"]
	$applicationPoolIdentityType = $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolIdentityType"]
	$applicationPoolUsername = $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolUsername"]
	$applicationPoolPassword = $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolPassword"]
	$applicationPoolFrameworkVersion = $OctopusParameters["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolFrameworkVersion"]

	$sitePath = ("IIS:\Sites\" + $webSiteName)

	Assert-WebsiteExists -SitePath $sitePath -SiteName $webSiteName

	[array]$virtualPathSegments =  Convert-ToPathSegments -VirtualPath $virtualPath
	Assert-ParentSegmentsExist -sitePath $sitePath -virtualPathSegments $virtualPathSegments

	$fullPathToLastVirtualPathSegment = Get-FullPath -root $sitePath -segments $virtualPathSegments
	$lastSegment = Get-Item $fullPathToLastVirtualPathSegment -ErrorAction SilentlyContinue

	SetUp-ApplicationPool -applicationPoolName $applicationPoolName -applicationPoolIdentityType $applicationPoolIdentityType -applicationPoolUsername $applicationPoolUsername -applicationPoolPassword $applicationPoolPassword -applicationPoolFrameworkVersion $applicationPoolFrameworkVersion -startPool $startAppPool

	if (!$lastSegment) {
		Write-Host "`"$virtualPath`" does not exist. Creating Web Application pointing to $fullPathToLastVirtualPathSegment ..."
		Execute-WithRetry { 
			New-Item $fullPathToLastVirtualPathSegment -type Application -physicalPath $physicalPath
		}
	} else {
		if ($lastSegment.ElementTagName -eq 'application') {
			Write-Host "Web Application `"$virtualPath`" already exists, no need to create it."
		} elseif ($lastSegment.ElementTagName -eq 'virtualDirectory') {
			# It looks like the only reliable way to do the conversion is to delete the exsting web application and then create a new virtual directory. http://stackoverflow.com/questions/16738995/powershell-convertto-webapplication-on-iis
			# We don't want to delete anything as the customer might have handcrafted the settings and has no way of retrieving them.
			throw "`"$virtualPath`" already exists in IIS and points to a Virtual Directory. We cannot automatically change this to a Web Application on your behalf. Please delete it and then re-deploy the project."
		} else {
			if (!(Is-Directory -Path $physicalPath)) {
				throw "`"$virtualPath`" already exists in IIS and points to an unknown item which isn't a directory. Please delete it and then re-deploy the project. If you used the Custom Installation Directory feature to target this path we recommend removing the Custom Installation Directory feature, instead allowing Octopus to unpack the files into the default location and update the Physical Path of the Web Application on your behalf."
			}
			
			Write-Host "`"$virtualPath`" already exists in IIS and points to an unknown item which seems to be a directory. We will try to convert it to a Web Application. If you used the Custom Installation Directory feature to target this path we recommend removing the Custom Installation Directory feature, instead allowing Octopus to unpack the files into the default location and update the Physical Path of the Web Application on your behalf."
			Execute-WithRetry { 
				New-Item $fullPathToLastVirtualPathSegment -type Application -physicalPath $physicalPath
			}

		}

		Set-Path -virtualPath $fullPathToLastVirtualPathSegment -physicalPath $physicalPath
	}

	Assign-ToApplicationPool -iisPath $fullPathToLastVirtualPathSegment -applicationPoolName $applicationPoolName					
	
	if($startAppPool -eq $true) {
		Start-ApplicationPool $applicationPoolName
    }

    popd
}


if ($deployAsWebSite)
{	
	$webSiteName = $OctopusParameters["Octopus.Action.IISWebSite.WebSiteName"]
	$applicationPoolName = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolName"]
	$bindingString = $OctopusParameters["Octopus.Action.IISWebSite.Bindings"]
	$existingBindings = $OctopusParameters["Octopus.Action.IISWebSite.ExistingBindings"]
	$webRoot =  Determine-Path $OctopusParameters["Octopus.Action.IISWebSite.WebRoot"]
	$enableWindows = $OctopusParameters["Octopus.Action.IISWebSite.EnableWindowsAuthentication"]
	$enableBasic = $OctopusParameters["Octopus.Action.IISWebSite.EnableBasicAuthentication"]
	$enableAnonymous = $OctopusParameters["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"]
	$applicationPoolIdentityType = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"]
	$applicationPoolUsername = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolUsername"]
	$applicationPoolPassword = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolPassword"]
	$applicationPoolFrameworkVersion = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"]
	$startAppPool = if ($OctopusParameters.ContainsKey("Octopus.Action.IISWebSite.StartApplicationPool")) { $OctopusParameters["Octopus.Action.IISWebSite.StartApplicationPool"] } else { $true }
	$startWebSite = if ($OctopusParameters.ContainsKey("Octopus.Action.IISWebSite.StartWebSite")) { $OctopusParameters["Octopus.Action.IISWebSite.StartWebSite"] } else { $true }
	
	Write-Host "Making sure a Website `"$webSiteName`" is configured in IIS..."

	#Assess SNI support (IIS 8 or greater)
	$iis = get-itemproperty HKLM:\SOFTWARE\Microsoft\InetStp\  | select setupstring 
	$iisVersion = ($iis.SetupString.Substring(4)) -as [double]
	$supportsSNI = $iisVersion -ge 8

	$wsbindings = new-object System.Collections.ArrayList

	function Write-IISBinding($message, $bindingObject) {
		if(-not ($bindingObject -is [PSObject])) {
			Write-Host "$message @{$([String]::Join("; ", ($bindingObject.Keys | % { return "$($_)=$($bindingObject[$_])" })))}"
		} else {
			Write-Host "$message $bindingObject"
		}
	}

	if(Get-Command ConvertFrom-Json -errorAction SilentlyContinue){
		$bindingArray = (ConvertFrom-Json $bindingString)
	} else {
		add-type -assembly system.web.extensions
		$serializer=new-object system.web.script.serialization.javascriptSerializer
		$bindingArray = ($serializer.DeserializeObject($bindingString))
	}

	ForEach($binding in $bindingArray){
		if(![Bool]::Parse($binding.enabled)) {
    		Write-IISBinding "Ignore binding: " $binding
    		continue
    	}

		$sslFlagPart = @{$true=1;$false=0}[[Bool]::Parse($binding.requireSni)]  
		$bindingIpAddress =  @{$true="*";$false=$binding.ipAddress}[[string]::IsNullOrEmpty($binding.ipAddress)]
		$bindingInformation = $bindingIpAddress+":"+$binding.port+":"+$binding.host

		$bindingObj = @{
			protocol=(($binding.protocol, "http") -ne $null)[0].ToLower();
			ipAddress=$bindingIpAddress;
			port=$binding.port;
			host=$binding.host;
			bindingInformation=$bindingInformation;
		};

		if ($binding.certificateVariable) {
			$bindingObj.certificateVariable = $binding.certificateVariable.Trim();
		} elseif ($binding.thumbprint -and ($null -ne $binding.thumbprint)){
			$bindingObj.thumbprint=$binding.thumbprint.Trim();
		}

		if ([Bool]::Parse($supportsSNI)) {
			$bindingObj.sslFlags=$sslFlagPart;
		}

		$wsbindings.Add($bindingObj) | Out-Null
	}

	# For any HTTPS bindings, ensure the certificate is configured for the IP/port combination
	$wsbindings | where-object { $_.protocol -eq "https" } | foreach-object {

		# If an Octopus-managed certificate variable is supplied, it will have been installed in the store earlier
		# in the deployment process
		if ($_.certificateVariable) {
			$sslCertificateThumbprint = $OctopusParameters[$_.certificateVariable + ".Thumbprint"]
		} elseif ($_.thumbprint){
			# Otherwise, the certificate thumbprint was supplied directly in the binding
			$sslCertificateThumbprint = $_.thumbprint.Trim()
		} else {
			[Console]::Error.WriteLine("To configure an HTTPS binding please choose a certificate which is managed by Octopus, or provide the thumbprint for a certificate which is already available in the Machine certificate store.")
			exit 1
		}

		Write-Host "Finding SSL certificate with thumbprint $sslCertificateThumbprint"
		foreach ($certStore in (Get-ChildItem Cert:\LocalMachine)) {
			try {
				$certs = Get-ChildItem "Cert:\LocalMachine\$($certStore.Name)" -ErrorAction Stop
				$certificate = $certs | Where-Object { $_.Thumbprint -eq $sslCertificateThumbprint -and $_.HasPrivateKey -eq $true } | Select-Object -first 1
				if ($certificate) {
					break
				}
			} catch {
				Write-Host "Skipping inaccessible certificate store '$($certStore.Name)': $_"
			}
		}
		if (! $certificate) 
		{
			throw "Could not find certificate under Cert:\LocalMachine with thumbprint $sslCertificateThumbprint. Make sure that the certificate is installed to the Local Machine context and that the private key is available."
		}

		$certPathParts = $certificate.PSParentPath.Split('\')
		$certStoreName = $certPathParts[$certPathParts.Length-1]

		Write-Host ("Found certificate: " + $certificate.Subject + " in: " + $certStoreName)

		$ipAddress = $_.ipAddress;
		if ((! $ipAddress) -or ($ipAddress -eq '*')) {
			$ipAddress = "0.0.0.0"
		}
		$port = $_.port
		$hostname = $_.host
		Execute-WithRetry { 
	
			#If we are supporting SNI then we need to bind cert to hostname instead of ip
			if($_.sslFlags -eq 1){
		
				$existing = & netsh http show sslcert hostnameport="$($hostname):$port"
				if ($LastExitCode -eq 0) {
					$hasThumb = ($existing | Where-Object { $_.IndexOf($certificate.Thumbprint, [System.StringComparison]::OrdinalIgnoreCase) -ne -1 })
					if ($hasThumb -eq $null) {
						Write-Host "A different binding exists for the Hostname/port combination, replacing..."
					
						& netsh http delete sslcert hostnameport="$($hostname):$port"
						if ($LastExitCode -ne 0 ){
							throw
						}

						$appid = [System.Guid]::NewGuid().ToString("b")
						& netsh http add sslcert hostnameport="$($hostname):$port" certhash="$($certificate.Thumbprint)" appid="$appid" certstorename="$certStoreName"
						if ($LastExitCode -ne 0 ){
							throw
						}

					} else {
						Write-Host "The required certificate binding is already in place"
					}
				} else {
					$appid = [System.Guid]::NewGuid().ToString("b")
					& netsh http add sslcert hostnameport="$($hostname):$port" certhash="$($certificate.Thumbprint)" appid="$appid" certstorename="$certStoreName"
					if ($LastExitCode -ne 0 ){
						throw
					}
				}	
			} else {
				$existing = & netsh http show sslcert ipport="$($ipAddress):$port"
				if ($LastExitCode -eq 0) {
					$hasThumb = ($existing | Where-Object { $_.IndexOf($certificate.Thumbprint, [System.StringComparison]::OrdinalIgnoreCase) -ne -1 })
					if ($hasThumb -eq $null) {
						Write-Host "A different binding exists for the IP/port combination, replacing..."
					
						& netsh http delete sslcert ipport="$($ipAddress):$port"
						if ($LastExitCode -ne 0 ){
							throw
						}

						$appid = [System.Guid]::NewGuid().ToString("b")
						& netsh http add sslcert ipport="$($ipAddress):$port" certhash="$($certificate.Thumbprint)" appid="$appid" certstorename="$certStoreName"
						if ($LastExitCode -ne 0 ){
							throw
						}
					
					} else {
						Write-Host "The required certificate binding is already in place"
					}
				} else {
					Write-Host "Adding a new SSL certificate binding..."
					$appid = [System.Guid]::NewGuid().ToString("b")
					& netsh http add sslcert ipport="$($ipAddress):$port" certhash="$($certificate.Thumbprint)" appid="$appid" certstorename="$certStoreName"
					if ($LastExitCode -ne 0 ){
						Write-Host "Failed adding new SSL binding for certificate with thumbprint '$($certificate.Thumbprint)'. Exit code: $LastExitCode"
						throw
					}
				}	
			}	
		}
	}

	## --------------------------------------------------------------------------------------
	## Run
	## --------------------------------------------------------------------------------------

	pushd IIS:\
	
	SetUp-ApplicationPool -applicationPoolName $applicationPoolName -applicationPoolIdentityType $applicationPoolIdentityType -applicationPoolFrameworkVersion $applicationPoolFrameworkVersion -applicationPoolUsername $applicationPoolUsername -applicationPoolPassword $applicationPoolPassword -startPool $startAppPool

	$sitePath = ("IIS:\Sites\" + $webSiteName)

	$odTempBinding = ":81:od-temp.example.com"

	# Create Website
	Execute-WithRetry { 
		Write-Verbose "Loading Site"
		$site = Get-Item $sitePath -ErrorAction SilentlyContinue
		if (!$site) { 
			Write-Host "Site `"$webSiteName`" does not exist, creating..." 
			$id = (dir iis:\sites | foreach {$_.id} | sort -Descending | select -first 1) + 1
			new-item $sitePath -bindings @{protocol="http";bindingInformation=$odTempBinding} -id $id -physicalPath $webRoot -confirm:$false
		} else {
			Write-Host "Site `"$webSiteName`" already exists"
		}
	}

	if($startWebSite -eq $false) {
		# Stop Website
		Execute-WithRetry { 
			$state = Get-WebsiteState $webSiteName
			if ($state.Value -eq "Started") {
				Write-Host "Web site is started. Attempting to stop..."
				Stop-Website $webSiteName
			}
		} -noLock $true
	}

	Assign-ToApplicationPool -iisPath $sitePath -applicationPoolName $applicationPoolName
	Set-Path -virtualPath $sitePath -physicalPath $webRoot

	function Convert-ToHashTable($bindingArray) {
		$hash = @{}
		$bindingArray | %{
		    $key = Get-BindingKey $_
		    $hash[$key] = $_
		}
		return $hash
	}

	function Get-BindingKey($binding) {
		return $binding.protocol + "|" + $binding.bindingInformation + "|" + $binding.sslFlags
	}

    if($existingBindings -eq "Merge") {
        # Merge existing bindings into the configured collection. This allows the following code to be the same regardless of this options
        $configuredBindingsLookup = Convert-ToHashTable $wsbindings
        $existingBindings = Get-ItemProperty $sitePath -name bindings
        $bindingsToMerge = $existingBindings.Collection | where { ($configuredBindingsLookup[(Get-BindingKey $_)] -eq $null) -and ($_.bindingInformation -ne $odTempBinding) } | ForEach-Object { $wsbindings.Add($_) }
    }

	# Returns $true if existing IIS bindings are as specified in configuration, otherwise $false
	function Bindings-AreCorrect($existingBindings, $configuredBindings, [System.Collections.ArrayList] $bindingsToRemove) {
		$existingBindingsLookup = Convert-ToHashTable $existingBindings.Collection
		$configuredBindingsLookup = Convert-ToHashTable $configuredBindings
	
		# Are there existing assigned bindings that are not configured
		for ($i = 0; $i -lt $existingBindings.Collection.Count; $i = $i+1) {
			$binding = $existingBindings.Collection[$i]
			$bindingKey = Get-BindingKey $binding

			$matching = $configuredBindingsLookup[$bindingKey]
		
			if ($matching -eq $null) {
				Write-Host "Found existing non-configured binding: $($binding.protocol) $($binding.bindingInformation)"
				$bindingsToRemove.Add($binding) | Out-Null
			}
		}

		if($bindingsToRemove.Length -gt 0){
            return $false
        }

		# Are there configured bindings which are not assigned
		for ($i = 0; $i -lt $configuredBindings.Count; $i = $i+1) {
			$wsbinding = $configuredBindings[$i]
            $wsBindingKey = Get-BindingKey $wsbinding

			$matching = $existingBindingsLookup[$wsBindingKey]

			if ($matching -eq $null) {
				Write-Host "Found configured binding which is not assigned: $($wsbinding.protocol) $($wsbinding.bindingInformation)"
				return $false
			}
		}        

		Write-Host "Looks OK"

		return $true
	}

	# Set Bindings
	Execute-WithRetry { 
		Write-Host "Comparing existing IIS bindings with configured bindings..."
		$existingBindings = Get-ItemProperty $sitePath -name bindings
        $bindingsToRemove = new-object System.Collections.ArrayList

		if (-not (Bindings-AreCorrect $existingBindings $wsbindings $bindingsToRemove)) {
			Write-Host "Existing IIS bindings do not match configured bindings."
			Write-Host "Clearing IIS bindings"
			Clear-ItemProperty $sitePath -name bindings

            If($hasWebCommitDelay){
                Start-WebCommitDelay
            }
       		for ($i = 0; $i -lt $wsbindings.Count; $i = $i+1) {
				Write-Host ("Assigning binding: " + ($wsbindings[$i].protocol + " " + $wsbindings[$i].bindingInformation))
				New-ItemProperty $sitePath -name bindings -value ($wsbindings[$i])
			}
            If($hasWebCommitDelay){
                Stop-WebCommitDelay -Commit $true
            }
		} else {
			Write-Host "Bindings are as configured. No changes required."
		}

		# try to remove ssl cert bindings for IIS bindings that are being removed
		$bindingsToRemove | where-object { $_.protocol -eq "https" } | foreach-object {
			$bindingParts = $_.bindingInformation.Split(':')
			$ipAddress = $bindingParts[0]
			if (!$ipAddress) {
				$ipAddress = "*"
			}
			$port = $bindingParts[1]
			$hostname = $bindingParts[2]

			if($_.sslFlags -eq 1){ # SNI on so we will have created against the hostname
				$existing = & netsh http show sslcert hostnameport="$($hostname):$port"
				if ($LastExitCode -eq 0) {
					Write-Host ("Removing unused SSL certificate binding: $($hostname):$port")
					& netsh http delete sslcert hostnameport="$($hostname):$port"
					if ($LastExitCode -ne 0 ){
						throw
					}
				}
			} else { # SNI off so we will have created against the ip
				# check if there are any other bindings to the same IP:Port so that
				# we don't remove an ssl cert that's used by any other sites on the server
				$existing = Get-WebBinding -IPAddress $ipAddress -Port $port -Protocol "https"
				if (!$existing) {
					Write-Host ("Removing unused SSL certificate binding: $($ipAddress):$port")
					if ($ipAddress -eq '*') {
						$ipAddress = "0.0.0.0"
					}
					& netsh http delete sslcert ipport="$($ipAddress):$port"

					if ($LastExitCode -ne 0 ){
						throw
					}
				}
			}
		}
	}

	$appCmdPath = $env:SystemRoot + "\system32\inetsrv\appcmd.exe"
	if ((Test-Path $appCmdPath) -eq $false) {
		throw "Could not find appCmd.exe at $appCmdPath"
	}

	try {
		Execute-WithRetry { 
			Write-Host "Anonymous authentication enabled: $enableAnonymous"
			& $appCmdPath set config "$webSiteName" -section:"system.webServer/security/authentication/anonymousAuthentication" /enabled:"$enableAnonymous" /commit:apphost
			if ($LastExitCode -ne 0 ){
				throw
			}		
		}

		Execute-WithRetry { 
			Write-Host "Basic authentication enabled: $enableBasic"
			& $appCmdPath set config "$webSiteName" -section:"system.webServer/security/authentication/basicAuthentication" /enabled:"$enableBasic" /commit:apphost
			if ($LastExitCode -ne 0 ){
				throw
			}		
		}

		Execute-WithRetry { 
			Write-Host "Windows authentication enabled: $enableWindows"
			& $appCmdPath set config "$webSiteName" -section:"system.webServer/security/authentication/windowsAuthentication" /enabled:"$enableWindows" /commit:apphost
			if ($LastExitCode -ne 0 ){
				throw
			}		
		
		}
	} catch [System.Exception] {
		Write-Host "Authentication options could not be set. This can happen when there is a problem with your application's web.config. For example, you might be using a section that requires an extension that is not installed on this web server (such as URL Rewriting). It can also happen when you have selected an authentication option and the appropriate IIS module is not installed (for example, for Windows authentication, you need to enable the Windows Authentication module in IIS/Windows first)"
		throw
	}

	if($startAppPool -eq $true) {
		Start-ApplicationPool $applicationPoolName
	}

	if($startWebSite -eq $true) {
		if ($wsbindings.Count -eq 0) {
			Write-Warning "The deployment has been configured to start the web site $webSiteName but no bindings are enabled."
		}
		# Start Website
		Execute-WithRetry { 
			$state = Get-WebsiteState $webSiteName
			if ($state.Value -eq "Stopped") {
				Write-Host "Web site is stopped. Attempting to start..."
				Start-Website $webSiteName
			} elseif ($state -eq "Undefined") {
				Write-Warning "Unable to retrieve the state of the web site $webSiteName. The web site will not be started. This is commonly caused by an invalid web site configuration."
			}
		} -noLock $true
	}

    popd
}


Write-Host "IIS configuration complete"
