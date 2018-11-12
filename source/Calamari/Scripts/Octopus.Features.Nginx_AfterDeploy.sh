#!/bin/bash
set -e

nginxTempDir=$(get_octopusvariable "OctopusNginxFeatureTempDirectory")
nginxConfDir=$(get_octopusvariable "Octopus.Action.Nginx.ConfigurationsDirectory")

# Always remove the temporary NGINX config folder
trap 'echo "Removing temporary folder ${nginxTempDir}..." && sudo rm -rf $nginxTempDir' exit

nginxConfRoot=${nginxConfDir:-/etc/nginx/conf.d}
echo "Copying $nginxTempDir/conf/* to $nginxConfRoot..."
nginxConfDir=${nginxConfDir:-/etc/nginx/conf.d}
sudo cp -R $nginxTempDir/conf/* $nginxConfDir -f

if [ -d "$nginxTempDir/ssl" ]; then
    nginxSslDir=$(get_octopusvariable "Octopus.Action.Nginx.CertificatesDirectory")
    nginxSslDir=${nginxSslDir:-/etc/ssl}
    echo "Copying $nginxTempDir/ssl/* to nginxSslDir..."
    sudo cp -R $nginxTempDir/ssl/* $nginxSslDir -f
fi

echo "Validating nginx configuration"
sudo nginx -t

echo "Reloading nginx configuration"
sudo nginx -s reload
