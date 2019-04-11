$IMAGE=$OctopusParameters["Image"]
$dockerUsername=$OctopusParameters["DockerUsername"]
$dockerPassword=$OctopusParameters["DockerPassword"]
$feedUri=$OctopusParameters["FeedUri"]

Write-Verbose $(docker -v)

if (-not ([string]::IsNullOrEmpty($dockerUsername))) {
    # docker 17.07 throws a warning to stderr if you use the --password param
    $dockerVersion = & docker version --format '{{.Client.Version}}'
    $parsedVersion = [Version]($dockerVersion -split '-')[0]
    $dockerNeedsPasswordViaStdIn = (($parsedVersion.Major -gt 17) -or (($parsedVersion.Major -eq 17) -and ($parsedVersion.Minor -gt 6)))
    if ($dockerNeedsPasswordViaStdIn) {
        echo $dockerPassword | docker login --username $dockerUsername --password-stdin $feedUri
    } else {
        docker login --username $dockerUsername --password $dockerPassword $feedUri
    }
    
    if(!$?)
    {
        Write-Error "Login Failed"
        exit 1
    }
}

Write-Verbose "docker pull $IMAGE"
docker pull $IMAGE