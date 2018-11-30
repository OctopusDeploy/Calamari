#Ideally this single line would replace everything below ... But there appears to be a bug somewhere
#helm fetch --repo $Url --username $Username --password $Password --version $version --destination $TempStaging $package

if (!$(Get-Command helm -errorAction SilentlyContinue))
{
    Write-Error "The helm client tool does not appear to be available on the current path.`nSee http://g.octopushq.com/HelmLimitations for more details."
    Exit 1
}

$TempHelmHome = $((Resolve-Path .\).Path) +"\helm"
$TempStaging = $((Resolve-Path .\).Path) +"\staging"

mkdir $TempStaging | Out-Null

$TempRepoName="octopusfeed"

$Username=$OctopusParameters["Username"]
$Password=$OctopusParameters["Password"]
$Url=$OctopusParameters["Url"]
$version=$OctopusParameters["Version"]
$package=$OctopusParameters["Package"]

Write-Host "Creating local helm context"
Write-Verbose "helm init --home $TempHelmHome --client-only"
helm init --home $TempHelmHome --client-only  | Write-Verbose
if($Username -eq $Null)
{
    helm repo add --home $TempHelmHome $TempRepoName $Url | Write-Verbose
} else {
    helm repo add --home $TempHelmHome --username $Username --password $Password $TempRepoName $Url | Write-Verbose
}

if(!$?) {
    exit 1
}

Write-Host "Fetching Chart"
Write-Verbose "helm fetch --home $TempHelmHome --version $Version --destination $TempStaging $TempRepoName/$Package"
helm fetch --home $TempHelmHome --version $Version --destination $TempStaging $TempRepoName/$Package