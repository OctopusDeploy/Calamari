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
			Write-Warning "1. Install .NET Framework 3.5.1"
			Write-Warning "2. Upgrade to PowerShell 3.0 (or greater)"
			Write-Warning "3. On Windows 2008 you might need to install PowerShell SnapIn for IIS from http://www.iis.net/downloads/microsoft/powershell#additionalDownloads"
			throw ($error | Select-Object -First 1)
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

function Execute-WithRetry([ScriptBlock] $command) {
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
			if ($attemptCount -lt ($maxFailures)) {
				Write-Host ("Attempt $attemptCount of $maxFailures failed: " + $_.Exception.Message)
			} else {
				throw
			}
		}
	}
}

function SetUp-ApplicationPool($applicationPoolName, $applicationPoolIdentityType, 
								$applicationPoolUsername, $applicationPoolPassword,
								$applicationPoolFrameworkVersion) 
{

	$appPoolPath = ("IIS:\AppPools\" + $applicationPoolName)

	# Set App Pool
	Execute-WithRetry { 
		Write-Verbose "Loading Application pool"
		$pool = Get-Item $appPoolPath -ErrorAction SilentlyContinue
		if (!$pool) { 
			Write-Host "Application pool `"$applicationPoolName`" does not exist, creating..." 
			new-item $appPoolPath -confirm:$false
			$pool = Get-Item $appPoolPath
		} else {
			Write-Host "Application pool `"$applicationPoolName`" already exists"
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
	}
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
	Write-Verbose "Looking for the parent Site `"$SiteName`" at `"$SitePath`"..."
	$site = Get-Item $SitePath -ErrorAction SilentlyContinue
	if (!$site) 
	{ 
		throw "The Web Site `"$SiteName`" does not exist in IIS and this step cannot create the Web Site because the necessary details are not available. Add a step which makes sure the parent Web Site exists before this step attempts to add a child to it." 
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

	SetUp-ApplicationPool -applicationPoolName $applicationPoolName -applicationPoolIdentityType $applicationPoolIdentityType -applicationPoolUsername $applicationPoolUsername -applicationPoolPassword $applicationPoolPassword -applicationPoolFrameworkVersion $applicationPoolFrameworkVersion

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
	Start-ApplicationPool $applicationPoolName
    
    popd
}


if ($deployAsWebSite)
{	
	$webSiteName = $OctopusParameters["Octopus.Action.IISWebSite.WebSiteName"]
	$applicationPoolName = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolName"]
	$bindingString = $OctopusParameters["Octopus.Action.IISWebSite.Bindings"]
	$webRoot =  Determine-Path $OctopusParameters["Octopus.Action.IISWebSite.WebRoot"]
	$enableWindows = $OctopusParameters["Octopus.Action.IISWebSite.EnableWindowsAuthentication"]
	$enableBasic = $OctopusParameters["Octopus.Action.IISWebSite.EnableBasicAuthentication"]
	$enableAnonymous = $OctopusParameters["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"]
	$applicationPoolIdentityType = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"]
	$applicationPoolUsername = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolUsername"]
	$applicationPoolPassword = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolPassword"]
	$applicationPoolFrameworkVersion = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"]

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

	if($bindingString.StartsWith("[{")) {
	
		if(Get-Command ConvertFrom-Json -errorAction SilentlyContinue){
			$bindingArray = (ConvertFrom-Json $bindingString)
		} else {
			add-type -assembly system.web.extensions
			$serializer=new-object system.web.script.serialization.javascriptSerializer
			$bindingArray = ($serializer.DeserializeObject($bindingString))
		}

		ForEach($binding in $bindingArray){
			$sslFlagPart = @{$true=1;$false=0}[[Bool]::Parse($binding.requireSni)]  
			$bindingIpAddress =  @{$true="*";$false=$binding.ipAddress}[[string]::IsNullOrEmpty($binding.ipAddress)]
			$bindingInformation = $bindingIpAddress+":"+$binding.port+":"+$binding.host
		
			if([Bool]::Parse($binding.enabled)) {
				Write-IISBinding "Found binding: " $binding
				if ([Bool]::Parse($supportsSNI)) {
					$wsbindings.Add(@{ 
						protocol=$binding.protocol;
						ipAddress=$bindingIpAddress;
						port=$binding.port;
						host=$binding.host;
						bindingInformation=$bindingInformation;
						thumbprint=$binding.thumbprint.Trim();
						sslFlags=$sslFlagPart }) | Out-Null
				} else {
					$wsbindings.Add(@{ 
						protocol=$binding.protocol;
						ipAddress=$bindingIpAddress;
						port=$binding.port;
						host=$binding.host;
						bindingInformation=$bindingInformation;
						thumbprint=$binding.thumbprint.Trim() }) | Out-Null
				}
			} else {
				Write-IISBinding "Ignore binding: " $binding
			}
		}

	} else {	
		# Each binding string consists of a protocol/binding information (IP, port, hostname)/SSL thumbprint/enabled/sslFlags
		# Binding strings are pipe (|) separated to allow multiple to be specified
		$bindingString.Split("|") | foreach-object {
			$bindingParts = $_.split("/")
			$skip = $false
			if ($bindingParts.Length -ge 4) {
				if (![String]::IsNullOrEmpty($bindingParts[3]) -and [Bool]::Parse($bindingParts[3]) -eq $false) {
					$skip = $true
				}
			}
		
			if ($skip -eq $false) {
				$addressParts = $bindingParts[1].split(':')
				$sslFlagPart = 0
				if($bindingParts.Length -ge 5){
					if (![String]::IsNullOrEmpty($bindingParts[4]) -and [Bool]::Parse($bindingParts[4]) -eq $true){
						$sslFlagPart = 1
					}
				}
				if ($supportsSNI -eq $true ){
					$wsbindings.Add(@{ 
						protocol=$bindingParts[0];
						bindingInformation=$bindingParts[1];
						thumbprint=$bindingParts[2].Trim();
						ipAddress=$addressParts[0];
						port= $addressParts[1];
						host=$addressParts[2];
						sslFlags=$sslFlagPart }) | Out-Null
					}
				else{
					$wsbindings.Add(@{ 
						protocol=$bindingParts[0];
						bindingInformation=$bindingParts[1];
						ipAddress=$addressParts[0];
						port= $addressParts[1];
						host=$addressParts[2];
						thumbprint=$bindingParts[2].Trim() }) | Out-Null
				}
			} else {
				Write-Host "Ignore binding: $_"
			}
		}
	}


	# For any HTTPS bindings, ensure the certificate is configured for the IP/port combination
	$wsbindings | where-object { $_.protocol -eq "https" } | foreach-object {
		$sslCertificateThumbprint = $_.thumbprint.Trim()
		Write-Host "Finding SSL certificate with thumbprint $sslCertificateThumbprint"
    
		$certificate = Get-ChildItem Cert:\LocalMachine -Recurse | Where-Object { $_.Thumbprint -eq $sslCertificateThumbprint -and $_.HasPrivateKey -eq $true } | Select-Object -first 1
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
	
	SetUp-ApplicationPool -applicationPoolName $applicationPoolName -applicationPoolIdentityType $applicationPoolIdentityType -applicationPoolFrameworkVersion $applicationPoolFrameworkVersion -applicationPoolUsername $applicationPoolUsername -applicationPoolPassword $applicationPoolPassword 

	$sitePath = ("IIS:\Sites\" + $webSiteName)

	# Create Website
	Execute-WithRetry { 
		Write-Verbose "Loading Site"
		$site = Get-Item $sitePath -ErrorAction SilentlyContinue
		if (!$site) { 
			Write-Host "Site `"$webSiteName`" does not exist, creating..." 
			$id = (dir iis:\sites | foreach {$_.id} | sort -Descending | select -first 1) + 1
			new-item $sitePath -bindings @{protocol="http";bindingInformation=":81:od-temp.example.com"} -id $id -physicalPath $webRoot -confirm:$false
		} else {
			Write-Host "Site `"$webSiteName`" already exists"
		}
	}

	Assign-ToApplicationPool -iisPath $sitePath -applicationPoolName $applicationPoolName
	Set-Path -virtualPath $sitePath -physicalPath $webRoot

	function Bindings-AreEqual($bindingA, $bindingB) {
		return ($bindingA.protocol -eq $bindingB.protocol) -and ($bindingA.bindingInformation -eq $bindingB.bindinginformation) -and ($bindingA.sslFlags -eq $bindingB.sslFlags)
	}

	# Returns $true if existing IIS bindings are as specified in configuration, otherwise $false
	function Bindings-AreCorrect($existingBindings, $configuredBindings) {
		# Are there existing assigned bindings that are not configured
		for ($i = 0; $i -lt $existingBindings.Collection.Count; $i = $i+1) {
			$binding = $existingBindings.Collection[$i]

			$matching = $configuredBindings | Where-Object {Bindings-AreEqual $binding $_ }
		
			if ($matching -eq $null) {
				Write-Host "Found existing non-configured binding: $($binding.protocol) $($binding.bindingInformation)"
				return $false
			}
		}

		# Are there configured bindings which are not assigned
		for ($i = 0; $i -lt $configuredBindings.Count; $i = $i+1) {
			$wsbinding = $configuredBindings[$i]

			$matching = $existingBindings.Collection | Where-Object {Bindings-AreEqual $wsbinding $_ }

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

		if (-not (Bindings-AreCorrect $existingBindings $wsbindings)) {
			Write-Host "Existing IIS bindings do not match configured bindings."
			Write-Host "Clearing IIS bindings"
			Clear-ItemProperty $sitePath -name bindings

       		for ($i = 0; $i -lt $wsbindings.Count; $i = $i+1) {
				Write-Host ("Assigning binding: " + ($wsbindings[$i].protocol + " " + $wsbindings[$i].bindingInformation))
				New-ItemProperty $sitePath -name bindings -value ($wsbindings[$i])
			}
		} else {
			Write-Host "Bindings are as configured. No changes required."
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

	Start-ApplicationPool $applicationPoolName

	# Start Website
	Execute-WithRetry { 
		$state = Get-WebsiteState $webSiteName
		if ($state.Value -eq "Stopped") {
			Write-Host "Web site is stopped. Attempting to start..."
			Start-Website $webSiteName
		}
	}

    popd
}


Write-Host "IIS configuration complete"
