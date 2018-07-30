#Ideally this single line would replace everything below ... But there appears to be a bug somewhere
#helm fetch --repo $Url --username $Username --password $Password --version $version --destination $TempStaging $package

$TempHelmHome = $((Resolve-Path .\).Path) +"\helm"
$TempStaging = $((Resolve-Path .\).Path) +"\staging"

mkdir $TempStaging | Out-Null

$TempRepoName="octopusfeed"

$Username=$OctopusParameters["Username"]
$Password=$OctopusParameters["Password"]
$Url=$OctopusParameters["Url"]
$version=$OctopusParameters["Version"]
$package=$OctopusParameters["Package"]

helm init --home $TempHelmHome --client-only
if($Username -eq $Null)
{
    helm repo add --home $TempHelmHome $TempRepoName $Url
} else {
    helm repo add --home $TempHelmHome --username $Username --password $Password $TempRepoName $Url
}

if(!$?) {
    exit 1
}

helm fetch --home $TempHelmHome --version $Version --destination $TempStaging $TempRepoName/$Package

#$files=(Get-ChildItem "$TempStaging/*");
#Set-OctopusVariable -name "PackageFile" -value $files[0].FullName;