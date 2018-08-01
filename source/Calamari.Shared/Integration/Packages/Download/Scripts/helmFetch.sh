
username=$(get_octopusvariable "Username")
password=$(get_octopusvariable "Password")
url=$(get_octopusvariable "Url")
version=$(get_octopusvariable "Version")
package=$(get_octopusvariable "Package")

tempHelmHome=$(pwd)/helm
tempStaging=$(pwd)/staging
tempRepoName="octopusfeed"

mkdir $tempStaging 1>/dev/null

echo "Creating local helm context"
write_verbose "helm init --home $tempHelmHome --client-only"

helm init --home $tempHelmHome --client-only

if [[ -z $username ]]; then
  helm repo add --home $tempHelmHome $tempRepoName $url
else
  helm repo add --home $tempHelmHome --username $username --password $password $tempRepoName $url
fi

if [[ $? != 0 ]]; then
  exit 1
fi

echo "Fetching Chart"
write_verbose "helm fetch --home $tempHelmHome --version $version --destination $tempStaging $tempRepoName/$package"
helm fetch --home $tempHelmHome --version $version --destination $tempStaging $tempRepoName/$package
