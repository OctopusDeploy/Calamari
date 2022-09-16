$IMAGE=$OctopusParameters["Image"]

Write-Verbose "docker pull $IMAGE"
docker pull $IMAGE
