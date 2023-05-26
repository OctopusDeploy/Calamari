#!/bin/bash

TRUE=0
FALSE=1
SUCCESS=0
# This selector will identify old resources based on the step, environment and tenant ids. This is how
# we originally identified resources, but it was not sufficiently unique. However existing
# deployments have these labels, and so we retain this query for compatibility.
# Note the absence of the Octopus.Kubernetes.SelectionStragegyVersion label identifies this label set
# as the V1 set.
SELECTOR_V1="!Octopus.Kubernetes.SelectionStrategyVersion,Octopus.Step.Id=#{Octopus.Step.Id},Octopus.Environment.Id=#{Octopus.Environment.Id | ToLower},Octopus.Deployment.Tenant.Id=#{unless Octopus.Deployment.Tenant.Id}untenanted#{/unless}#{if Octopus.Deployment.Tenant.Id}#{Octopus.Deployment.Tenant.Id | ToLower}#{/if},Octopus.Deployment.Id!=#{Octopus.Deployment.Id | ToLower}"
# This selector takes the project and action ID into account to address https://github.com/OctopusDeploy/Issues/issues/5185.
SELECTOR_V2="Octopus.Kubernetes.SelectionStrategyVersion=SelectionStrategyVersion2,Octopus.Project.Id=#{Octopus.Project.Id | ToLower},Octopus.Step.Id=#{Octopus.Step.Id | ToLower},Octopus.Action.Id=#{Octopus.Action.Id | ToLower},Octopus.Environment.Id=#{Octopus.Environment.Id | ToLower},Octopus.Deployment.Tenant.Id=#{unless Octopus.Deployment.Tenant.Id}untenanted#{/unless}#{if Octopus.Deployment.Tenant.Id}#{Octopus.Deployment.Tenant.Id | ToLower}#{/if},Octopus.Deployment.Id!=#{Octopus.Deployment.Id | ToLower}"
# This selector is used when K8S steps are called from runbooks. Note that runbooks don't have an Octopus.Deployment.Id
# variable, so we use the Octopus.RunbookRun.Id instead.
SELECTOR_RUNBOOK_V2="Octopus.Kubernetes.SelectionStrategyVersion=SelectionStrategyVersion2,Octopus.Project.Id=#{Octopus.Project.Id | ToLower},Octopus.Step.Id=#{Octopus.Step.Id | ToLower},Octopus.Action.Id=#{Octopus.Action.Id | ToLower},Octopus.Environment.Id=#{Octopus.Environment.Id | ToLower},Octopus.Deployment.Tenant.Id=#{unless Octopus.Deployment.Tenant.Id}untenanted#{/unless}#{if Octopus.Deployment.Tenant.Id}#{Octopus.Deployment.Tenant.Id | ToLower}#{/if},Octopus.RunbookRun.Id!=#{Octopus.RunbookRun.Id | ToLower}"

RESOURCE_TYPE=`get_octopusvariable "Octopus.Action.KubernetesContainers.DeploymentResourceType"`
if [[ -z $RESOURCE_TYPE ]]; then
	RESOURCE_TYPE="Deployment"
fi

Kubectl_Exe=`get_octopusvariable "Octopus.Action.Kubernetes.CustomKubectlExecutable"`
Deployment_Id=`get_octopusvariable "Octopus.Deployment.Id"`

if [[ -z $Kubectl_Exe ]]; then
	Kubectl_Exe="kubectl"
fi

function is_bluegreen {
	Deployment_Style="#{Octopus.Action.KubernetesContainers.DeploymentStyle}"
	if [[ ${Deployment_Style,,} == "bluegreen" ]]; then
		return $TRUE
	else
		return $FALSE
	fi
}

function is_notondelete {
	Deployment_Style="#{Octopus.Action.KubernetesContainers.DeploymentStyle}"
	if [[ ${Deployment_Style,,} != "ondelete" ]]; then
		return $TRUE
	else
		return $FALSE
	fi
}

function is_waitfordeployment {
	Deployment_Wait="#{Octopus.Action.KubernetesContainers.DeploymentWait}"
	if [[ ${Deployment_Wait,,} == "wait" ]]; then
		return $TRUE
	else
		return $FALSE
	fi
}

function write_plainerror {
	defaultIFS=$IFS
	IFS=
	echo "##octopus[stdout-error]"
	for line in $1; do
		echo "$line"
	done
	echo "##octopus[stdout-default]"
	IFS=$defaultIFS
}

function write_verbose {
	defaultIFS=$IFS
	IFS=
	echo "##octopus[stdout-verbose]"
	for line in $1; do
		echo "$line"
	done
	echo "##octopus[stdout-default]"
	IFS=$defaultIFS
}

function check_app_exists {
	command -v $1 > /dev/null 2>&1
	if [[ $? -ne 0 ]]; then
		write_plainerror "The $1 application must be installed"
		SUCCESS=$FALSE
	fi
}

function file_exists {
	if [[ ! -f $1 ]]; then
		return $FALSE
	else
		FileContents=`cat $1`
		if [[ -z "$FileContents" ]]; then
			return $FALSE
		else
			return $TRUE
		fi
	fi
}

function deploy_feed_secrets {
	if [[ $SUCCESS -eq $TRUE ]]; then
		if file_exists secret.yml; then
			write_verbose "$(cat secret.yml)"

			${Kubectl_Exe} apply -f secret.yml

			SUCCESS=$?

			IFS="," read -ra Secrets <<< "#{Octopus.Action.KubernetesContainers.SecretNames}"
			for Secret in "${Secrets[@]}"; do
				if [[ ! -z "$Secret" ]]; then
					set_octopusvariable "Secret(${Secret})" "$(${Kubectl_Exe} get secret ${Secret} -o=json 2> /dev/null)"
				fi
			done
		fi
	fi
}

function deploy_configmap {
	if [[ $SUCCESS -eq $TRUE ]]; then
		if [[ $(get_octopusvariable "Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled") == *"True"* ]]; then
	        ConfigMapDataFileArgs=()
            # Each config map item is in its own file. The file name is stored in a variable: Octopus.Action.KubernetesContainers.ConfigMapData[key].FileName
            #{each ConfigMapData in Octopus.Action.KubernetesContainers.ConfigMapData }
            ConfigMapDataFileArgs+=('--from-file=#{ConfigMapData}=#{ConfigMapData.FileName}')
            #{/each}

			if [[ ${#ConfigMapDataFileArgs[@]} -gt 0 ]]; then

                ${Kubectl_Exe} get configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}" > /dev/null 2>&1

                if [[ $? -eq 0 ]]; then
                    write_verbose "${Kubectl_Exe} delete configmap \"#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}\""
                    ${Kubectl_Exe} delete configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}"
                fi

                write_verbose "${Kubectl_Exe} create configmap \"#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}\" ${ConfigMapDataFileArgs[*]}"
                printf "%s\n" "${ConfigMapDataFileArgs[@]}" | \
                    xargs ${Kubectl_Exe} create configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}"

                if [[ ! -z "#{Octopus.Action.KubernetesContainers.ComputedLabels}" ]]; then
                    echo $(get_octopusvariable "Octopus.Action.KubernetesContainers.ComputedLabels") | \
                        jq --arg q '"' -r '. | keys[] as $k | "\($q)\($k)=\(.[$k])\($q) "' | \
                        xargs $Kubectl_Exe label --overwrite configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}"
                fi

                SUCCESS=$?

                set_octopusvariable "ConfigMap" $($Kubectl_Exe get configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}" -o=json)
			fi
		fi
	fi
}

function deploy_secret {
	if [[ $SUCCESS -eq $TRUE ]]; then
		if [[ $(get_octopusvariable "Octopus.Action.KubernetesContainers.KubernetesSecretEnabled") == *"True"* ]]; then
	        SecretDataFileArgs=()
            # Each secret item is in its own file. The file name is stored in a variable: Octopus.Action.KubernetesContainers.SecretData[key].FileName
            #{each SecretData in Octopus.Action.KubernetesContainers.SecretData }
            SecretDataFileArgs+=('--from-file=#{SecretData}=#{SecretData.FileName}')
            #{/each}

			if [[ ${#SecretDataFileArgs[@]} -gt 0 ]]; then

                ${Kubectl_Exe} get secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}" > /dev/null 2>&1

                if [[ $? -eq 0 ]]; then
                    write_verbose "${Kubectl_Exe} delete secret \"#{Octopus.Action.KubernetesContainers.ComputedSecretName}\""
                    ${Kubectl_Exe} delete secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}"
                fi

                write_verbose "${Kubectl_Exe} create secret generic \"#{Octopus.Action.KubernetesContainers.ComputedSecretName}\" ${SecretDataFileArgs[*]}"
                printf "%s\n" "${SecretDataFileArgs[@]}" | \
                    xargs ${Kubectl_Exe} create secret generic "#{Octopus.Action.KubernetesContainers.ComputedSecretName}"

                if [[ ! -z "#{Octopus.Action.KubernetesContainers.ComputedLabels}" ]]; then
                    echo $(get_octopusvariable "Octopus.Action.KubernetesContainers.ComputedLabels") | \
                        jq --arg q '"' -r '. | keys[] as $k | "\($q)\($k)=\(.[$k])\($q) "' | \
                        xargs $Kubectl_Exe label --overwrite secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}"
                fi

                SUCCESS=$?

                set_octopusvariable "Secret" $($Kubectl_Exe get secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}" -o=json)
			fi
		fi
	fi
}

function deploy_customresources {
	if file_exists "#{Octopus.Action.KubernetesContainers.CustomResourceYamlFileName}"; then
		if [[ $SUCCESS -eq $TRUE ]]; then
			write_verbose "$(cat "#{Octopus.Action.KubernetesContainers.CustomResourceYamlFileName}")"

			newCustomResources=$(${Kubectl_Exe} apply -f "#{Octopus.Action.KubernetesContainers.CustomResourceYamlFileName}" -o json)
			SUCCESS=$?

            if [[ $SUCCESS -eq $TRUE ]]; then
                # kubectl apply will return a list if multiple resources were applied, or a single object.
                # We can distinguish between the two by the "kind" of the returned value
                kind=$(jq -r '.kind' <<< $newCustomResources)
            fi
			# It is possible kubectl didn't return valid JSON
            if [[ $SUCCESS -eq $TRUE ]] && [[ $? -eq 0 ]]; then
                if [[ "$kind" == "List" ]]; then
                    # Get a list of the names and kinds of the created resources
                    newCustomResources=$(jq -r '.items[]? | "\(.metadata.name):\(.kind)"' <<< $newCustomResources)
                else
                    # There is only 1 created resource
                    newCustomResources=$(jq -r '"\(.metadata.name):\(.kind)"' <<< $newCustomResources)
                fi

                IFS= read -ra CustomResources <<< $newCustomResources
                for CustomResource in "${CustomResources[@]}"; do
                    if [[ ! -z "$CustomResource" ]]; then
                        # Split the text on the colon to create an array
                        customResourceSplit=(${CustomResource//:/ })

                        # We swallowed the result of kubectl apply, so add some logging to show something happened
				        echo "${customResourceSplit[1]}/${customResourceSplit[0]} created"

                        set_octopusvariable \
                            "CustomResources(${customResourceSplit[0]})" \
                            "$(${Kubectl_Exe} get ${customResourceSplit[1]} ${customResourceSplit[0]} -o=json 2> /dev/null)"
                    fi
                done
            else
                write_plainerror "\"kubectl apply -o json\" returned invalid JSON."
                write_plainerror "---------------------------"
                write_plainerror "$newCustomResources"
                write_plainerror "---------------------------"
                write_plainerror "This can happen with older versions of kubectl. Please update to a recent version of kubectl."
                write_plainerror "See https://github.com/kubernetes/kubernetes/issues/58834 for more details."
                write_plainerror "Custom resources will not be saved as output variables, and will not be automatically cleaned up."
            fi
		fi
	fi
}

function deploy_deployment {
	if file_exists deployment.yml; then
		if [[ $SUCCESS -eq $TRUE ]]; then
			write_verbose "$(cat deployment.yml)"
			${Kubectl_Exe} apply -f deployment.yml

			# If doing a plain deployment, the success will be judged on the response from the last executable call
			SUCCESS=$?

			# When doing a blue/green deployment, the deployment resource created by old steps are deleted once the
			# new deployment is created and the service is pointed to it.
			if [[ $RESOURCE_TYPE != "Job" ]] && (is_bluegreen || (is_waitfordeployment && is_notondelete)); then
				# There can be cases where the rollout command fails when it is executed straight away,
				# so we need to wait for the deployment to be visible
				for i in `seq 1 5`; do
					$Kubectl_Exe get ${RESOURCE_TYPE} "#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}"
					if [[ $? -eq 0 ]]; then
						break
					fi
					sleep 5
				done

				${Kubectl_Exe} rollout status "${RESOURCE_TYPE}/#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}"

				# If doing a blue/green deployment, success is judged by the response of the rollout status
				SUCCESS=$?

				if [[ $SUCCESS -ne $TRUE ]]; then
					write_plainerror "The ${RESOURCE_TYPE} #{Octopus.Action.KubernetesContainers.ComputedDeploymentName} failed."
				fi
			fi

			set_octopusvariable "Deployment" "$(${Kubectl_Exe} get ${RESOURCE_TYPE} "#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}" -o=json 2> /dev/null)"
		else
			write_plainerror "The ${RESOURCE_TYPE} was not created or updated."
		fi
	fi
}

function deploy_service {
	if file_exists service.yml; then
		if [[ $SUCCESS -eq $TRUE ]]; then
			write_verbose "$(cat service.yml)"
			${Kubectl_Exe} apply -f service.yml
			SUCCESS=$?
		else
			if is_bluegreen; then
				write_plainerror "The service #{Octopus.Action.KubernetesContainers.ServiceName} was not updated, and does not point to the failed ${RESOURCE_TYPE}, meaning the blue/green swap was not performed."
			else
				write_plainerror "The service #{Octopus.Action.KubernetesContainers.ServiceName} was not updated."
			fi
		fi

		set_octopusvariable "Service" "$(${Kubectl_Exe} get service "#{Octopus.Action.KubernetesContainers.ServiceName}" -o=json 2> /dev/null)"
	fi
}

function deploy_ingress {
	if file_exists ingress.yml; then
		if [[ $SUCCESS -eq $TRUE ]]; then
			write_verbose "$(cat ingress.yml)"
			${Kubectl_Exe} apply -f ingress.yml
			SUCCESS=$?
		else
			write_plainerror "The ingress rules for #{Octopus.Action.KubernetesContainers.IngressName} were not updated."
		fi

		set_octopusvariable "Ingress" "$(${Kubectl_Exe} get ingress "#{Octopus.Action.KubernetesContainers.IngressName}" -o=json 2> /dev/null)"
	fi
}

# When doing a blue/green deployment, the deployment resource created by old steps are deleted once the
# new deployment is created and the service is pointed to it.
function clean_deployment {
	if file_exists deployment.yml; then
		if [[ $SUCCESS -eq $TRUE ]]; then
			if is_bluegreen; then
			    echo "Deleting old ${RESOURCE_TYPE}s"
			    if [[ ! -z ${Deployment_Id} ]]; then
				    ${Kubectl_Exe} delete ${RESOURCE_TYPE} -l ${SELECTOR_V1}
				    ${Kubectl_Exe} delete ${RESOURCE_TYPE} -l ${SELECTOR_V2}
				else
				    ${Kubectl_Exe} delete ${RESOURCE_TYPE} -l ${SELECTOR_RUNBOOK_V2}
				fi
				SUCCESS=$?
			fi
		else
			write_plainerror "The previous ${RESOURCE_TYPE}s were not removed."
		fi
	fi
}

# The config map resource created by old steps are deleted once the
# new deployment is created and the service is pointed to it.
function clean_configmap {
	if file_exists deployment.yml; then
		if [[ $SUCCESS -eq $TRUE ]]; then
		  echo "Deleting old ConfigMaps"
		  if [[ ! -z ${Deployment_Id} ]]; then
			    $Kubectl_Exe delete configmap -l ${SELECTOR_V1}
			    $Kubectl_Exe delete configmap -l ${SELECTOR_V2}
			else
			    $Kubectl_Exe delete configmap -l ${SELECTOR_RUNBOOK_V2}
			fi
			SUCCESS=$?
		else
			write_plainerror "The previous config maps were not removed."
		fi
	fi
}

# The secret resource created by old steps are deleted once the
# new deployment is created and the service is pointed to it.
function clean_secret {
	if file_exists deployment.yml; then
		if [[ $SUCCESS -eq $TRUE ]]; then
		  echo "Deleting old Secrets"
		  if [[ ! -z ${Deployment_Id} ]]; then
			    $Kubectl_Exe delete secret -l ${SELECTOR_V1}
			    $Kubectl_Exe delete secret -l ${SELECTOR_V2}
			else
			    $Kubectl_Exe delete secret -l ${SELECTOR_RUNBOOK_V2}
			fi
			SUCCESS=$?
		else
			write_plainerror "The previous secrets were not removed."
		fi
	fi
}

function clean_customresources {
	if [[ $SUCCESS -eq $TRUE ]]; then
		if [[ ! -z $newCustomResources ]]; then
		    if file_exists deployment.yml; then
                IFS= read -ra CustomResources <<< $newCustomResources
                for CustomResource in "${CustomResources[@]}"; do
                    if [[ ! -z "${CustomResource}" ]]; then
                        # Split the text on the colon to create an array
                        customResourceSplit=(${CustomResource//:/ })
                        if [[ ! -z "${customResourceSplit[1]}" ]]; then
                            echo "Deleting old ${customResourceSplit[1]} resources"
                            if [[ ! -z ${Deployment_Id} ]]; then
                                $Kubectl_Exe delete ${customResourceSplit[1]} -l ${SELECTOR_V1}
                                $Kubectl_Exe delete ${customResourceSplit[1]} -l ${SELECTOR_V2}
                            else
                                $Kubectl_Exe delete ${customResourceSplit[1]} -l ${SELECTOR_RUNBOOK_V2}
                            fi
                            if [[ $SUCCESS -eq $TRUE ]]; then
                                SUCCESS=$?
                            fi
                        fi
                    fi
                done
            fi
		fi
	else
		write_plainerror "The previous custom resources were not removed."
	fi
}

function write_failuremessage {
	if [[ $SUCCESS -ne $TRUE ]]; then
		write_plainerror "The deployment process failed. The resources created by this step will be passed to \"kubectl describe\" and logged below."

		if file_exists deployment.yml; then
            echo "The Deployment resource description: $Kubectl_Exe describe ${RESOURCE_TYPE} #{Octopus.Action.KubernetesContainers.ComputedDeploymentName}"
            $Kubectl_Exe describe ${RESOURCE_TYPE} "#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}"

            # Get the first 10 non-running pods. A deployment could be hundreds of pods, so we don't want to spend time
            # describing them all.
            kubectl get replicasets  -o json  | jq -r '.items[]? | select(.metadata.ownerReferences[]? | select(.name == "#{Octopus.Action.KubernetesContainers.ComputedDeploymentName}" and .kind == "Deployment")) | .metadata.name' | \
                xargs -I{} -n 1 sh -c "kubectl get pods -o json | jq -r '.items[]? | select(.metadata.ownerReferences[]? | select(.name == \"{}\" and .kind == \"ReplicaSet\")) | select (.status.phase != \"Running\") | .metadata.name'" | \
                head -n 10 | \
                xargs -I{} -n 1 sh -c "echo \"The Pod resource description: $Kubectl_Exe describe pod {}\"; $Kubectl_Exe describe pod {}"
        fi

        if file_exists service.yml; then
            echo "The Service resource description: $Kubectl_Exe describe service #{Octopus.Action.KubernetesContainers.ServiceName}"
            $Kubectl_Exe describe service "#{Octopus.Action.KubernetesContainers.ServiceName}"
        fi

        if file_exists ingress.yml; then
            echo "The Ingress resource description: $Kubectl_Exe describe ingress #{Octopus.Action.KubernetesContainers.IngressName}"
            $Kubectl_Exe describe ingress "#{Octopus.Action.KubernetesContainers.IngressName}"
        fi

        if [[ $(get_octopusvariable "Octopus.Action.KubernetesContainers.KubernetesSecretEnabled") == *"True"* ]]; then
            echo "The Secret resource description: $Kubectl_Exe describe secret #{Octopus.Action.KubernetesContainers.ComputedSecretName}"
            $Kubectl_Exe describe secret "#{Octopus.Action.KubernetesContainers.ComputedSecretName}"
        fi

        if [[ $(get_octopusvariable "Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled") == *"True"* ]]; then
            echo "The ConfigMap resource description: $Kubectl_Exe describe configmap #{Octopus.Action.KubernetesContainers.ComputedConfigMapName}"
            $Kubectl_Exe describe configmap "#{Octopus.Action.KubernetesContainers.ComputedConfigMapName}"
        fi

        if file_exists "#{Octopus.Action.KubernetesContainers.CustomResourceYamlFileName}"; then
            IFS= read -ra CustomResources <<< $newCustomResources
            for CustomResource in "${CustomResources[@]}"; do
                if [[ ! -z "$CustomResource" ]]; then
                    customResourceSplit=(${CustomResource//:/ })
                    echo "The custom resource description: $Kubectl_Exe describe ${customResourceSplit[1]} ${customResourceSplit[1]}"
                    $Kubectl_Exe describe ${customResourceSplit[1]} ${customResourceSplit[0]}
                fi
            done
        fi

		exit 1
	fi
}

check_app_exists jq
check_app_exists xargs

deploy_feed_secrets
deploy_configmap
deploy_secret
deploy_customresources
deploy_deployment
deploy_service
deploy_ingress
clean_deployment
clean_configmap
clean_secret
clean_customresources
write_failuremessage

# Kubectl can return with 1 if an apply results in no change.
# https://github.com/kubernetes/kubernetes/issues/58212
# We want a clean exit here though, regardless of the last exit code.
exit 0