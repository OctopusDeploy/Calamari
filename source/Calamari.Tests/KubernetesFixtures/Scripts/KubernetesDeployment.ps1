<#
	This selector will identify old resources based on the step, environment and tenant ids. This is how
	we originally identified resources, but it was not sufficiently unique. However existing
	deployments have these labels, and so we retain this query for compatibility.
	Note the absence of the Octopus.Kubernetes.SelectionStragegyVersion label identifies this label set
	as the V1 set.
#>
$selectorV1 = "!Octopus.Kubernetes.SelectionStrategyVersion,Octopus.Step.Id=#{Octopus.Step.Id},Octopus.Environment.Id=#{Octopus.Environment.Id | ToLower},Octopus.Deployment.Tenant.Id=#{unless Octopus.Deployment.Tenant.Id}untenanted#{/unless}#{if Octopus.Deployment.Tenant.Id}#{Octopus.Deployment.Tenant.Id | ToLower}#{/if},Octopus.Deployment.Id!=#{Octopus.Deployment.Id | ToLower}"

<#
	This selector takes the project and action ID into account to address https://github.com/OctopusDeploy/Issues/issues/5185.
#>
$selectorV2 = "Octopus.Kubernetes.SelectionStrategyVersion=SelectionStrategyVersion2,Octopus.Project.Id=#{Octopus.Project.Id | ToLower},Octopus.Step.Id=#{Octopus.Step.Id | ToLower},Octopus.Action.Id=#{Octopus.Action.Id | ToLower},Octopus.Environment.Id=#{Octopus.Environment.Id | ToLower},Octopus.Deployment.Tenant.Id=#{unless Octopus.Deployment.Tenant.Id}untenanted#{/unless}#{if Octopus.Deployment.Tenant.Id}#{Octopus.Deployment.Tenant.Id | ToLower}#{/if},Octopus.Deployment.Id!=#{Octopus.Deployment.Id | ToLower}"
<#
	This selector is used when K8S steps are called from runbooks. Note that runbooks don't have an Octopus.Deployment.Id
	variable, so we use the Octopus.RunbookRun.Id instead.
#>
$selectorRunbookV2 = "Octopus.Kubernetes.SelectionStrategyVersion=SelectionStrategyVersion2,Octopus.Project.Id=#{Octopus.Project.Id | ToLower},Octopus.Step.Id=#{Octopus.Step.Id | ToLower},Octopus.Action.Id=#{Octopus.Action.Id | ToLower},Octopus.Environment.Id=#{Octopus.Environment.Id | ToLower},Octopus.Deployment.Tenant.Id=#{unless Octopus.Deployment.Tenant.Id}untenanted#{/unless}#{if Octopus.Deployment.Tenant.Id}#{Octopus.Deployment.Tenant.Id | ToLower}#{/if},Octopus.RunbookRun.Id!=#{Octopus.RunbookRun.Id | ToLower}"

$resourceType = $OctopusParameters["Octopus.Action.KubernetesContainers.DeploymentResourceType"]
if ([string]::IsNullOrEmpty($resourceType)) {
	$resourceType = "Deployment"
}

$rolloutType = $OctopusParameters["Octopus.Action.KubernetesContainers.DeploymentStyle"]

$Kubectl_Exe = $OctopusParameters["Octopus.Action.Kubernetes.CustomKubectlExecutable"]
if ([string]::IsNullOrEmpty($Kubectl_Exe)) {
	$Kubectl_Exe = "kubectl"
}

function Is-BlueGreen() {
	return "#{Octopus.Action.KubernetesContainers.DeploymentStyle}" -ieq "BlueGreen"
}

function Is-WaitingForDeployment() {
	return "#{Octopus.Action.KubernetesContainers.DeploymentWait}" -ieq "Wait"
}

function Write-PlainError([string]$message) {
	Write-Host "##octopus[stdout-error]"
	$message.Split("`n") | ForEach {Write-Host $_}
	Write-Host "##octopus[stdout-default]"
}

function Write-Verbose([string]$message) {
	Write-Host "##octopus[stdout-verbose]"
	$message.Split("`n") | ForEach {Write-Host $_}
	Write-Host "##octopus[stdout-default]"
}

function Execute-Command() {
	param
	(
		[ScriptBlock]$command,
		[Boolean]$silent = $false
	)

	$lastSetting = $ErrorActionPreference
	$ErrorActionPreference = "Continue"
	& $command 2>&1 |
			ForEach-Object {
				if ($silent -eq $false) {
					if ($_ -is [System.Management.Automation.ErrorRecord]) {
						Write-PlainError $_.ToString()
					} else {
						Write-Host $_
					}
				}
			}
	$ErrorActionPreference = $lastSetting
}

function Execute-CommandAndReturn() {
	param
	(
		[ScriptBlock]$command,
		[Boolean]$silent = $false
	)

	$lastSetting = $ErrorActionPreference
	$ErrorActionPreference = "Continue"
	$retValue = & $command 2>&1
	$ErrorActionPreference = $lastSetting
	return $retValue
}


function Deploy-FeedSecrets() {
	if ((Test-Path secret.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content secret.yml))) {
		Write-Verbose $(Get-Content -Raw secret.yml)

		Execute-Command {& $Kubectl_Exe apply -f secret.yml}

		$retValue = $LASTEXITCODE -eq 0

		$secrets = "#{Octopus.Action.KubernetesContainers.SecretNames}".Split(",") | Where {-not [string]::IsNullOrEmpty($_)}
		$secrets | ForEach {
			Set-OctopusVariable -name "FeedSecret$($secrets.indexOf($_))" -value $(& $Kubectl_Exe get secret $_ -o=json 2> $null)
		}

		return $retValue
	}

	return $true
}

function Deploy-ConfigMap() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)

	if ($OctopusParameters["Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled"] -ieq "true")
	{
		if ($DeploymentSuccess) {
			$ConfigMapDataFileArgs = @()
			# Each config map item is in its own file. The file name is stored in a variable: Octopus.Action.KubernetesContainers.ConfigMapData[key].FileName
			#{each ConfigMapData in Octopus.Action.KubernetesContainers.ConfigMapData }
			$ConfigMapDataFileArgs += "--from-file=#{ConfigMapData}=#{ConfigMapData.FileName}"
			#{/each}

			if ($ConfigMapDataFileArgs.Length -gt 0)
			{
				Execute-Command { & $Kubectl_Exe get configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}" } $true

				if ($LASTEXITCODE -eq 0)
				{
					Write-Verbose "& $Kubectl_Exe delete configmap `"#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}`""
					Execute-Command { & $Kubectl_Exe delete configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}" }
				}

				Write-Verbose "& $Kubectl_Exe create configmap `"#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}`" $ConfigMapDataFileArgs"
				Execute-Command { & $Kubectl_Exe create configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}" $ConfigMapDataFileArgs }

				if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($OctopusParameters["Octopus.Action.KubernetesContainers.ComputedLabels"]))
				{
					$Labels = ConvertFrom-Json -InputObject $OctopusParameters["Octopus.Action.KubernetesContainers.ComputedLabels"]
					foreach ($label in ($Labels | Get-Member -MemberType NoteProperty)) {
						Write-Verbose "& $Kubectl_Exe label --overwrite configmap `"#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}`" `"$( $label.Name )=$( $Labels."$( $label.Name )" )`""
						Execute-Command { & $Kubectl_Exe label --overwrite configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}" "$( $label.Name )=$( $Labels."$( $label.Name )" )" }
						if ($LASTEXITCODE -ne 0) {
							break
						}
					}
				}

				$retValue = $LASTEXITCODE -eq 0

				Set-OctopusVariable -name "ConfigMap" -value $( Execute-CommandAndReturn { & $Kubectl_Exe get configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}" -o=json } )

				return $retValue
			}
		}
	}

	return $DeploymentSuccess
}

function Deploy-Secret() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)

	if ($OctopusParameters["Octopus.Action.KubernetesContainers.KubernetesSecretEnabled"] -ieq "true") {
		if ($DeploymentSuccess) {
			$SecretDataFileArgs = @()
			# Each secret item is in its own file. The file name is stored in a variable: Octopus.Action.KubernetesContainers.SecretData[key].FileName
			#{each SecretData in Octopus.Action.KubernetesContainers.SecretData }
			$SecretDataFileArgs += "--from-file=#{SecretData}=#{SecretData.FileName}"
			#{/each}

			if ($SecretDataFileArgs.Length -gt 0) {
				Execute-Command {& $Kubectl_Exe get secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}"} $true

				if ($LASTEXITCODE -eq 0) {
					Write-Verbose "& $Kubectl_Exe delete secret `"#{Octopus.Action.KubernetesContainers.ComputedSecretName}`""
					Execute-Command {& $Kubectl_Exe delete secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}"}
				}

				Write-Verbose "& $Kubectl_Exe create secret generic `"#{Octopus.Action.KubernetesContainers.ComputedSecretName}`" $SecretDataFileArgs"
				Execute-Command {& $Kubectl_Exe create secret generic "#{Octopus.Action.KubernetesContainers.ComputedSecretName}" $SecretDataFileArgs}

				if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($OctopusParameters["Octopus.Action.KubernetesContainers.ComputedLabels"])) {
					$Labels = ConvertFrom-Json -InputObject $OctopusParameters["Octopus.Action.KubernetesContainers.ComputedLabels"]
					foreach ($label in ($Labels | Get-Member -MemberType NoteProperty)) {
						Write-Verbose "& $Kubectl_Exe label --overwrite secret `"#{Octopus.Action.KubernetesContainers.ComputedSecretName}`" `"$($label.Name)=$($Labels."$($label.Name)")`""
						Execute-Command {& $Kubectl_Exe label --overwrite secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}" "$($label.Name)=$($Labels."$($label.Name)")"}
						if ($LASTEXITCODE -ne 0) {
							break
						}
					}
				}

				$retValue = $LASTEXITCODE -eq 0

				Set-OctopusVariable -name "Secret" -value $(Execute-CommandAndReturn {& $Kubectl_Exe get secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}" -o=json})

				return $retValue
			}
		}
	}

	return $DeploymentSuccess
}

function Deploy-CustomResources() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)

	if ((Test-Path "#{Octopus.Action.KubernetesContainers.CustomResourceYamlFileName}") -and -not [string]::IsNullOrWhiteSpace($(Get-Content "#{Octopus.Action.KubernetesContainers.CustomResourceYamlFileName}"))) {
		if ($DeploymentSuccess)
		{
			Write-Verbose $(Get-Content -Raw "#{Octopus.Action.KubernetesContainers.CustomResourceYamlFileName}")
			$applyResult = Execute-CommandAndReturn {& $Kubectl_Exe apply -f "#{Octopus.Action.KubernetesContainers.CustomResourceYamlFileName}" -o json}
			$retValue = $LASTEXITCODE -eq 0
			if(!$retValue) {
				Write-Warning "$applyResult"
				Write-CustomResourceErrorMessage
				return
			}
			try {
				$json = $applyResult | ConvertFrom-Json -ErrorAction Stop
			} catch {
				Write-CustomResourceErrorMessage $applyResult
			}
			if ($null -ne $json) {
				# kubectl apply will return a list if multiple resources were applied, or a single object.
				# We can distinguish between the two by the "kind" of the returned value
				if ($json.kind -eq "List") {
					# Get a list of the names and kinds of the created resources
					$script:newCustomResources = $json |
							Select -ExpandProperty items |
							% { @{Name = $_.metadata.name; Kind = $_.kind}}
				} else {
					# There is only 1 created resource
					$script:newCustomResources = @{Name = $json.metadata.name; Kind = $json.kind}
				}

				# We swallowed the result of kubectl apply, so add some logging to show something happened
				$script:newCustomResources | % {Write-Host "$($_.Kind)/$($_.Name) created"}
			}
		} else {
			Write-PlainError "The custom resources were not deployed."
			$retValue = $DeploymentSuccess
		}

		# The names and kinds of the resources are saved in Octopus.Action.KubernetesContainers.CustomResourceNames
		# each name/kind pair is saved as a colon separated pair i.e. MyResource:NetworkPolicy
		$script:newCustomResources |
				% {Set-OctopusVariable -name "CustomResources($($_.Name))" -value $(Execute-CommandAndReturn {& $Kubectl_Exe get $($_.Kind) $($_.Name) -o=json})}

		return $retValue
	}

	return $DeploymentSuccess
}

function Write-CustomResourceErrorMessage() {
	Param
	(
		[Parameter(ValueFromPipeline=$True, Position=0)]
		[string] $json
	)

	Write-PlainError "`"kubectl apply -o json`" returned invalid JSON:"
	Write-PlainError "---------------------------"
	Write-PlainError "$json"
	Write-PlainError "---------------------------"
	Write-PlainError "This can happen with older versions of kubectl. Please update to a recent version of kubectl."
	Write-PlainError "See https://github.com/kubernetes/kubernetes/issues/58834 for more details."
	Write-PlainError "Custom resources will not be saved as output variables, and will not be automatically cleaned up."
}

function Deploy-Deployment() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)

	if ((Test-Path deployment.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content deployment.yml))) {
		if ($DeploymentSuccess) {
			Write-Verbose $(Get-Content -Raw deployment.yml)
			Execute-Command {& $Kubectl_Exe apply -f deployment.yml}

			# If doing a plain deployment, the success will be judged on the response from the last executable call
			$retValue = $LASTEXITCODE -eq 0

			# When doing a blue/green deployment, the deployment resource created by old steps are deleted once the
			# new deployment is created and the service is pointed to it.
			if (((Is-BlueGreen) -or (Is-WaitingForDeployment)) -and $rolloutType -ine "OnDelete" -and $retValue -and $resourceType -ine "Job")
			{
				# There can be cases where the rollout command fails when it is executed straight away,
				# so we need to wait for the deployment to be visible
				For ($i=0; $i -lt 5; $i++) {
					Execute-Command {& $Kubectl_Exe get $resourceType "#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}"} $true
					if ($LASTEXITCODE -eq 0) {
						break
					}
					Start-Sleep 5
				}
				Execute-Command {& $Kubectl_Exe rollout status "$resourceType/#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}"}

				# If doing a blue/green deployment, success is judged by the response of the rollout status
				$retValue = $LASTEXITCODE -eq 0

				if ($LASTEXITCODE -ne 0) {
					Write-PlainError "The $resourceType #{Octopus.Action.KubernetesContainers.ComputedDeploymentName} failed."
				}
			}

			Set-OctopusVariable -name "Deployment" -value $(Execute-CommandAndReturn {& $Kubectl_Exe get $resourceType "#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}" -o=json})

			return $retValue
		} else {
			Write-PlainError "The $resourceType was not created or updated."
		}
	}

	return $DeploymentSuccess
}

function Deploy-Service() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)

	if ((Test-Path service.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content service.yml))) {
		if ($DeploymentSuccess)
		{
			Write-Verbose $(Get-Content -Raw service.yml)
			Execute-Command {& $Kubectl_Exe apply -f service.yml}
			$retValue = $LASTEXITCODE -eq 0
		} else {
			if (Is-BlueGreen)  {
				Write-PlainError "The service #{Octopus.Action.KubernetesContainers.ServiceName} was not updated, and does not point to the failed $resourceType, meaning the blue/green swap was not performed."
			} else  {
				Write-PlainError "The service #{Octopus.Action.KubernetesContainers.ServiceName} was not updated."
			}
			$retValue = $DeploymentSuccess
		}

		Set-OctopusVariable -name "Service" -value $(Execute-CommandAndReturn {& $Kubectl_Exe get service "#{Octopus.Action.KubernetesContainers.ServiceName}" -o=json})

		return $retValue
	}

	return $DeploymentSuccess
}

function Deploy-Ingress() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)

	if ((Test-Path ingress.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content ingress.yml))) {
		if ($DeploymentSuccess) {
			Write-Verbose $(Get-Content -Raw ingress.yml)
			Execute-Command {& $Kubectl_Exe apply -f ingress.yml}
			$retValue = $LASTEXITCODE -eq 0
		} else {
			Write-PlainError "The ingress rules for #{Octopus.Action.KubernetesContainers.IngressName} were not updated."
			$retValue = $DeploymentSuccess
		}

		Set-OctopusVariable -name "Ingress" -value $(Execute-CommandAndReturn {& $Kubectl_Exe get ingress "#{Octopus.Action.KubernetesContainers.IngressName}" -o=json})

		return $retValue
	}

	return $DeploymentSuccess
}


# When doing a blue/green deployment, the deployment resource created by old steps are deleted once the
# new deployment is created and the service is pointed to it.
function Clean-Deployment() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)
	if ((Test-Path deployment.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content deployment.yml))) {
		if ($DeploymentSuccess) {
			if (Is-BlueGreen) {
				Write-Host "Deleting old $($resourceType)s"
				if (-not [string]::IsNullOrEmpty($OctopusParameters["Octopus.Deployment.Id"]))
				{
					Execute-Command {& $Kubectl_Exe delete $resourceType -l $selectorV1}
					Execute-Command {& $Kubectl_Exe delete $resourceType -l $selectorV2}
				}
				else
				{
					Execute-Command {& $Kubectl_Exe delete $resourceType -l $selectorRunbookV2}
				}
				return $LASTEXITCODE -eq 0
			}
		} else {
			Write-PlainError "The previous $($resourceType)s were not removed."
		}
	}

	return $DeploymentSuccess
}

# The config map resource created by old steps are deleted once the
# new deployment is created and the service is pointed to it.
function Clean-ConfigMap() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)
	if ((Test-Path deployment.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content deployment.yml))) {
		if ($DeploymentSuccess) {
			Write-Host "Deleting old ConfigMaps"
			if (-not [string]::IsNullOrEmpty($OctopusParameters["Octopus.Deployment.Id"]))
			{
				Execute-Command { & $Kubectl_Exe delete configmap -l $selectorV1 }
				Execute-Command { & $Kubectl_Exe delete configmap -l $selectorV2 }
			}
			else
			{
				Execute-Command { & $Kubectl_Exe delete configmap -l $selectorRunbookV2 }
			}
			return $LASTEXITCODE -eq 0
		} else {
			Write-PlainError "The previous config maps were not removed."
		}
	}

	return $DeploymentSuccess
}

# The secret resource created by old steps are deleted once the
# new deployment is created and the service is pointed to it.
function Clean-Secret() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)
	if ((Test-Path deployment.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content deployment.yml))) {
		if ($DeploymentSuccess) {
			Write-Host "Deleting old Secrets"
			if (-not [string]::IsNullOrEmpty($OctopusParameters["Octopus.Deployment.Id"]))
			{
				Execute-Command { & $Kubectl_Exe delete secret -l $selectorV1 }
				Execute-Command { & $Kubectl_Exe delete secret -l $selectorV2 }
			}
			else
			{
				Execute-Command { & $Kubectl_Exe delete secret -l $selectorRunbookV2 }
			}
			return $LASTEXITCODE -eq 0
		} else {
			Write-PlainError "The previous secrets were not removed."
		}
	}

	return $DeploymentSuccess
}

function Clean-CustomResources() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)

	if ($DeploymentSuccess) {
		if ((Test-Path deployment.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content deployment.yml))) {
			return ($script:newCustomResources |
			# Get the kind
			% { $_.Kind } |
			# We only need the unique kinds
			Get-Unique |
			# Ignore empty strings
			? { -not [string]::IsNullOrWhitespace($_) } |
			# Use each hashtable to remove old resources
			% {
				Write-Host "Deleting old $_ resources"
				if (-not [string]::IsNullOrEmpty($OctopusParameters["Octopus.Deployment.Id"]))
				{
					# delete the resources that with the old label set
					Execute-Command { & $Kubectl_Exe delete $_ -l $selectorV1 }
					# delete the resources that with the new label set
					Execute-Command { & $Kubectl_Exe delete $_ -l $selectorV2 }
				}
				else
				{
					Execute-Command { & $Kubectl_Exe delete $_ -l $selectorRunbookV2 }
				}
				# Make a note of the return code
				$LASTEXITCODE
			} |
			# Did any of the commands return a non-zero code?
			? { $_ -ne 0 }).Count -eq 0
		}
	} else {
		Write-PlainError "The previous custom resources were not removed."
	}

	return $DeploymentSuccess
}

function Write-FailureMessage() {
	Param
	(
		[Parameter(Mandatory=$true, ValueFromPipeline=$True, Position=0)]
		[bool] $DeploymentSuccess
	)

	if (-not $DeploymentSuccess) {
		Write-PlainError "The deployment process failed. The resources created by this step will be passed to `"kubectl describe`" and logged below."

		# Describe the deployment
		if ((Test-Path deployment.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content deployment.yml))) {
			Write-Host "The Deployment resource description: $Kubectl_Exe describe deployment #{Octopus.Action.KubernetesContainers.ComputedDeploymentName}"
			Execute-Command {& $Kubectl_Exe describe $resourceType "#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}"}

			$replicaSets = Execute-CommandAndReturn {& $Kubectl_Exe get replicasets -o json} -Silent $true |
					ConvertFrom-Json |
					Select -ExpandProperty items |
					? {($_.metadata.ownerReferences | ? {$_.name -eq "#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}" -and $_.kind -eq "Deployment"}).Count -ne 0} |
					% {$_.metadata.name}

			# Get the first 10 non-running pods. A deployment could be hundreds of pods, so we don't want to spend time
			# describing them all.
			$pods = Execute-CommandAndReturn {& $Kubectl_Exe get pods -o json} -Silent $true |
					ConvertFrom-Json |
					Select -ExpandProperty items |
					? {($_.metadata.ownerReferences | ?{$replicaSets -Contains $_.name -and $_.kind -eq "ReplicaSet"}).Count -ne 0 -and $_.status.phase -ne "Running"} |
					Select -First 10

			$pods | % {
				Write-Host "The Pod resource description: $Kubectl_Exe describe pod $($_.metadata.name)"
				Execute-Command {& $Kubectl_Exe describe pod $_.metadata.name}
			}
		}

		# Describe the service
		if ((Test-Path service.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content service.yml))) {
			Write-Host "The Service resource description: $Kubectl_Exe describe service #{Octopus.Action.KubernetesContainers.ServiceName}"
			Execute-Command {& $Kubectl_Exe describe service "#{Octopus.Action.KubernetesContainers.ServiceName}"}
		}

		# Describe the ingress
		if ((Test-Path ingress.yml) -and -not [string]::IsNullOrWhiteSpace($(Get-Content ingress.yml))) {
			Write-Host "The Ingress resource description: $Kubectl_Exe describe ingress #{Octopus.Action.KubernetesContainers.IngressName}"
			Execute-Command {& $Kubectl_Exe describe ingress "#{Octopus.Action.KubernetesContainers.IngressName}"}
		}

		# Describe the secret
		if ($OctopusParameters["Octopus.Action.KubernetesContainers.KubernetesSecretEnabled"] -ieq "true") {
			Write-Host "The Secret resource description: $Kubectl_Exe describe secret #{Octopus.Action.KubernetesContainers.ComputedSecretName}"
			Execute-Command {& $Kubectl_Exe describe secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}"}
		}

		# Describe the configmap
		if ($OctopusParameters["Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled"] -ieq "true") {
			Write-Host "The ConfigMap resource description: $Kubectl_Exe describe configmap #{Octopus.Action.KubernetesContainers.ComputedConfigMapName}"
			Execute-Command {& $Kubectl_Exe describe configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}"}
		}

		# Describe any custom resources
		if (-not [string]::IsNullOrWhitespace($OctopusParameters["Octopus.Action.KubernetesContainers.CustomResourceNames"])) {
			$OctopusParameters["Octopus.Action.KubernetesContainers.CustomResourceNames"].Split(',') | % {
				Write-Host "The custom resource description: $Kubectl_Exe describe $($_.Split(':')[1]) $($_.Split(':')[0])"
				Execute-Command {& $Kubectl_Exe describe $_.Split(':')[1] $_.Split(':')[0]}
			}
		}

		Exit 1
	}
}

$ErrorActionPreference = 'Stop'

Deploy-FeedSecrets |
		Deploy-ConfigMap |
		Deploy-Secret |
		Deploy-CustomResources |
		Deploy-Deployment |
		Deploy-Service |
		Deploy-Ingress |
		Clean-Deployment |
		Clean-ConfigMap |
		Clean-Secret |
		Clean-CustomResources |
		Write-FailureMessage

# Kubectl can return with 1 if an apply results in no change.
# https://github.com/kubernetes/kubernetes/issues/58212
# We want a clean exit here though, regardless of the last exit code.
$LASTEXITCODE = 0