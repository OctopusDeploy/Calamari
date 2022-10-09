#!/bin/bash
set -e
nginxTempDir=$(get_octopusvariable "OctopusNginxFeatureTempDirectory")
if [ ! -d "$nginxTempDir" ]; then
    echo >&2 "Unable to find temporary folder '$nginxTempDir'."
    exit 1
fi
nginxConfDir=$(get_octopusvariable "Octopus.Action.Nginx.ConfigurationsDirectory")

# Always remove the temporary NGINX config folder
trap 'echo "Removing temporary folder ${nginxTempDir}..." && sudo rm -rf $nginxTempDir' exit

nginxConfRoot=${nginxConfDir:-/etc/nginx/conf.d}

echo "Clearing the existing locations dir"
for dir in $nginxTempDir/conf/*
do
    fixedDir=${dir##*/}
    if [[ -d "$dir" && ! -L "$dir" && -d "$nginxConfRoot/$fixedDir" ]]
    then
        sudo rm -rf $nginxConfRoot/$fixedDir
    fi
done

echo "Copying $nginxTempDir/conf/* to $nginxConfRoot..."
sudo cp -R $nginxTempDir/conf/* $nginxConfRoot -f

if [ -d "$nginxTempDir/ssl" ]; then
    nginxSslDir=$(get_octopusvariable "Octopus.Action.Nginx.CertificatesDirectory")
    nginxSslDir=${nginxSslDir:-/etc/ssl}
    echo "Copying $nginxTempDir/ssl/* to $nginxSslDir..."
    sudo cp -R $nginxTempDir/ssl/* $nginxSslDir -f
fi

echo "Validating nginx configuration"
echo "##octopus[stdout-verbose]"
sudo nginx -t 2>&1
echo "##octopus[stdout-default]"

echo "Reloading nginx configuration"
echo "##octopus[stdout-verbose]"
sudo nginx -s reload 2>&1
echo "##octopus[stdout-default]"