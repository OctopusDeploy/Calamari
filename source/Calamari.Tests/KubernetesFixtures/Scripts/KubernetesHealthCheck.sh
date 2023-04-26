#!/bin/bash

echo kubectl version to test connectivity

kubectl version --short || exit 1

canQueryNodes=`kubectl auth can-i get nodes --all-namespaces`

if [[ $canQueryNodes == 'yes' ]]; then

nodes=""

for line in $(kubectl get nodes -o=custom-columns=NAME:.metadata.name --all-namespaces);
do
  line=$(echo $line | awk '{$1=$1};1')
if [[ $line != 'NAME' ]]; then
  nodes="$nodes \"$line\","
fi
done

nodes=${nodes:: $((${#nodes} - 1))}

read -r -d '' METADATA <<EOT
{ "type": "kubernetes", "thumbprint": "%THUMBPRINT%", "metadata": { "nodes":[$nodes] } }
EOT

parameters=""
parameters="$parameters deploymentTargetId='$(encode_servicemessagevalue "%DEPLOYMENTTARGETID%")'"
parameters="$parameters metadata='$(encode_servicemessagevalue "$METADATA")'"
echo "##octopus[set-deploymenttargetmetadata ${parameters}]"
else

read -r -d '' METADATA <<EOT
{ "type": "kubernetes", "thumbprint": "%THUMBPRINT%", "metadata": { "INSUFFICIENTPRIVILEGES": "Insufficient privileges to retrieve deployment target metadata" } }
EOT

parameters=""
parameters="$parameters deploymentTargetId='$(encode_servicemessagevalue "%DEPLOYMENTTARGETID%")'"
parameters="$parameters metadata='$(encode_servicemessagevalue "$METADATA")'"
echo "##octopus[set-deploymenttargetmetadata ${parameters}]"
fi
