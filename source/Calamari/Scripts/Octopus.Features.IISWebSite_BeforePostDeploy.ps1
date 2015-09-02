## --------------------------------------------------------------------------------------
## Configuration
## --------------------------------------------------------------------------------------

$isEnabled = $OctopusParameters["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"]
if (!$isEnabled -or ![Bool]::Parse($isEnabled))
{
   exit 0
}

$WebSiteName = $OctopusParameters["Octopus.Action.IISWebSite.WebSiteName"]
$ApplicationPoolName = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolName"]
$bindingString = $OctopusParameters["Octopus.Action.IISWebSite.Bindings"]
$appPoolFrameworkVersion = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"]
$webRoot = $OctopusParameters["Octopus.Action.IISWebSite.WebRoot"]
$enableWindows = $OctopusParameters["Octopus.Action.IISWebSite.EnableWindowsAuthentication"]
$enableBasic = $OctopusParameters["Octopus.Action.IISWebSite.EnableBasicAuthentication"]
$enableAnonymous = $OctopusParameters["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"]
$applicationPoolIdentityType = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"]
$applicationPoolUsername = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolUsername"]
$applicationPoolPassword = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolPassword"]

if (! $webRoot) {
	$webRoot = "."
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
    Write-Output "Invalid Sleep time between failures.  Setting to max of 60 seconds"
    $sleepBetweenFailures = 60
}

# Helper to run a block with a retry if things go wrong
function Execute-WithRetry([ScriptBlock] $command) {
	$attemptCount = 0
	$operationIncomplete = $true

	while ($operationIncomplete -and $attemptCount -lt $maxFailures) {
		$attemptCount = ($attemptCount + 1)

		if ($attemptCount -ge 2) {
			Write-Output "Waiting for $sleepBetweenFailures seconds before retrying..."
			Start-Sleep -s $sleepBetweenFailures
			Write-Output "Retrying..."
		}

		try {
			& $command

			$operationIncomplete = $false
		} catch [System.Exception] {
			if ($attemptCount -lt ($maxFailures)) {
				Write-Output ("Attempt $attemptCount of $maxFailures failed: " + $_.Exception.Message)
			} else {
				throw
			}
		}
	}
}

$webRoot = (resolve-path $webRoot).ProviderPath

#Assess SNI support (IIS 8 or greater)
$iis = get-itemproperty HKLM:\SOFTWARE\Microsoft\InetStp\  | select setupstring 
$iisVersion = ($iis.SetupString.Substring(4)) -as [double]
$supportsSNI = $iisVersion -ge 8


$wsbindings = new-object System.Collections.ArrayList

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
		$sslFlagPart = 0
		if($bindingParts.Length -ge 5){
			if (![String]::IsNullOrEmpty($bindingParts[4]) -and [Bool]::Parse($bindingParts[4]) -eq $true){
				$sslFlagPart = 1
			}
		}
		if ($supportsSNI -eq $true ){
			$wsbindings.Add(@{ protocol=$bindingParts[0];bindingInformation=$bindingParts[1];thumbprint=$bindingParts[2].Trim();sslFlags=$sslFlagPart }) | Out-Null
			}
		else{
			$wsbindings.Add(@{ protocol=$bindingParts[0];bindingInformation=$bindingParts[1];thumbprint=$bindingParts[2].Trim() }) | Out-Null
		}
	} else {
		Write-Output "Ignore binding: $_"
	}
}


Add-PSSnapin WebAdministration -ErrorAction SilentlyContinue
Import-Module WebAdministration -ErrorAction SilentlyContinue

# For any HTTPS bindings, ensure the certificate is configured for the IP/port combination
$wsbindings | where-object { $_.protocol -eq "https" } | foreach-object {
    $sslCertificateThumbprint = $_.thumbprint.Trim()
    Write-Output "Finding SSL certificate with thumbprint $sslCertificateThumbprint"
    
    $certificate = Get-ChildItem Cert:\LocalMachine -Recurse | Where-Object { $_.Thumbprint -eq $sslCertificateThumbprint -and $_.HasPrivateKey -eq $true } | Select-Object -first 1
    if (! $certificate) 
    {
        throw "Could not find certificate under Cert:\LocalMachine with thumbprint $sslCertificateThumbprint. Make sure that the certificate is installed to the Local Machine context and that the private key is available."
    }

    $certPathParts = $certificate.PSParentPath.Split('\')
    $certStoreName = $certPathParts[$certPathParts.Length-1]

    Write-Output ("Found certificate: " + $certificate.Subject + " in: " + $certStoreName)

    $bindingInfo = $_.bindingInformation
    $bindingParts = $bindingInfo.split(':')
    $ipAddress = $bindingParts[0]
    if ((! $ipAddress) -or ($ipAddress -eq '*')) {
        $ipAddress = "0.0.0.0"
    }
    $port = $bindingParts[1]
	$hostname = $bindingParts[2]
	Execute-WithRetry { 
	
		#If we are supporting SNI then we need to bind cert to hostname instead of ip
		if($_.sslFlags -eq 1){
		
			$existing = & netsh http show sslcert hostnameport="$($hostname):$port"
			if ($LastExitCode -eq 0) {
				$hasThumb = ($existing | Where-Object { $_.IndexOf($certificate.Thumbprint, [System.StringComparison]::OrdinalIgnoreCase) -ne -1 })
				if ($hasThumb -eq $null) {
					Write-Output "A different binding exists for the Hostname/port combination, replacing..."
					
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
					Write-Output "The required certificate binding is already in place"
				}
			} else {
				Write-Output "Adding a new SSL certificate binding..."

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
					Write-Output "A different binding exists for the IP/port combination, replacing..."
					
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
					Write-Output "The required certificate binding is already in place"
				}
			} else {
				Write-Output "Adding a new SSL certificate binding..."

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

$appPoolPath = ("IIS:\AppPools\" + $ApplicationPoolName)

Execute-WithRetry { 
	$pool = Get-Item $appPoolPath -ErrorAction SilentlyContinue
	if (!$pool) { 
		Write-Output "Application pool `"$ApplicationPoolName`" does not exist, creating..." 
		new-item $appPoolPath -confirm:$false
		$pool = Get-Item $appPoolPath
	} else {
		Write-Output "Application pool `"$ApplicationPoolName`" already exists"
	}
}

Execute-WithRetry { 
	Write-Output "Set application pool identity: $applicationPoolIdentityType"
	if ($applicationPoolIdentityType -eq "SpecificUser") {
		Set-ItemProperty $appPoolPath -name processModel -value @{identitytype="SpecificUser"; username="$applicationPoolUsername"; password="$applicationPoolPassword"}
	} else {
		Set-ItemProperty $appPoolPath -name processModel -value @{identitytype="$applicationPoolIdentityType"}
	}
}

Execute-WithRetry { 
	Write-Output "Set .NET framework version: $appPoolFrameworkVersion" 
	Set-ItemProperty $appPoolPath managedRuntimeVersion $appPoolFrameworkVersion
}

$sitePath = ("IIS:\Sites\" + $webSiteName)

Execute-WithRetry { 
	$site = Get-Item $sitePath -ErrorAction SilentlyContinue
	if (!$site) { 
		Write-Output "Site `"$WebSiteName`" does not exist, creating..." 
		$id = (dir iis:\sites | foreach {$_.id} | sort -Descending | select -first 1) + 1
		new-item $sitePath -bindings @{protocol="http";bindingInformation=":81:od-temp.example.com"} -id $id -physicalPath $webRoot -confirm:$false
	} else {
		Write-Output "Site `"$WebSiteName`" already exists"
	}
}

$cmd = { 
	Write-Output "Assigning website to application pool..."
	Set-ItemProperty $sitePath -name applicationPool -value $ApplicationPoolName
}
Execute-WithRetry -Command $cmd

Execute-WithRetry { 
	Write-Output ("Home directory: " + $webRoot)
	Set-ItemProperty $sitePath -name physicalPath -value "$webRoot"
}

function Bindings-AreEqual($bindingA, $bindingB) {
    return ($bindingA.protocol -eq $bindingB.protocol) -and ($bindingA.bindingInformation -eq $bindingB.bindinginformation) -and ($bindingA.sslFlags -eq $bindingB.sslFlags)
}

Execute-WithRetry { 
	Write-Output "Assigning bindings to website..."
	$existingBindings = Get-ItemProperty $sitePath -name bindings

	#remove existing bindings not configured
	for ($i = 0; $i -lt $existingBindings.Collection.Count; $i = $i+1) {
		$binding = $existingBindings.Collection[$i]

		$matching = $wsbindings | Where-Object {Bindings-AreEqual $binding $_ }

		if (-not $matching) {
			Write-Host "Removing binding: $($binding.protocol) $($binding.bindingInformation)"
			Remove-WebBinding -Name $webSiteName -BindingInformation $binding.bindingInformation
		}
	}

	#add new bindings that don't already exist
	for ($i = 0; $i -lt $wsbindings.Count; $i = $i+1) {
		$wsbinding = $wsbindings[$i]

		$matching = $existingBindings.Collection | Where-Object {Bindings-AreEqual $wsbinding $_ }

		if (-not $matching) {
			Write-Host "Adding binding: $($wsbinding.protocol) $($wsbinding.bindingInformation)"
			New-ItemProperty $sitePath -name bindings -value ($wsbinding)
		}
	}
}

$appCmdPath = $env:SystemRoot + "\system32\inetsrv\appcmd.exe"
if ((Test-Path $appCmdPath) -eq $false) {
	throw "Could not find appCmd.exe at $appCmdPath"
}

try {
	Execute-WithRetry { 
		Write-Output "Anonymous authentication enabled: $enableAnonymous"
		& $appCmdPath set config "$WebSiteName" -section:"system.webServer/security/authentication/anonymousAuthentication" /enabled:"$enableAnonymous" /commit:apphost
		if ($LastExitCode -ne 0 ){
			throw
		}		
	}

	Execute-WithRetry { 
		Write-Output "Basic authentication enabled: $enableBasic"
		& $appCmdPath set config "$WebSiteName" -section:"system.webServer/security/authentication/basicAuthentication" /enabled:"$enableBasic" /commit:apphost
		if ($LastExitCode -ne 0 ){
			throw
		}		
	}

	Execute-WithRetry { 
		Write-Output "Windows authentication enabled: $enableWindows"
		& $appCmdPath set config "$WebSiteName" -section:"system.webServer/security/authentication/windowsAuthentication" /enabled:"$enableWindows" /commit:apphost
		if ($LastExitCode -ne 0 ){
			throw
		}		
		
	}
} catch [System.Exception] {
	Write-Output "Authentication options could not be set. This can happen when there is a problem with your application's web.config. For example, you might be using a section that requires an extension that is not installed on this web server (such as URL Rewrtiting). It can also happen when you have selected an authentication option and the appropriate IIS module is not installed (for example, for Windows authentication, you need to enable the Windows Authentication module in IIS/Windows first)"
	throw
}

# It can take a while for the App Pool to come to life (#490)
Start-Sleep -s 1

Execute-WithRetry { 
	$state = Get-WebAppPoolState $ApplicationPoolName
	if ($state.Value -eq "Stopped") {
		Write-Output "Application pool is stopped. Attempting to start..."
		Start-WebAppPool $ApplicationPoolName
	}
}

Execute-WithRetry { 
	$state = Get-WebsiteState $WebSiteName
	if ($state.Value -eq "Stopped") {
		Write-Output "Web site is stopped. Attempting to start..."
		Start-Website $WebSiteName
	}
}

popd

Write-Output "IIS configuration complete"
