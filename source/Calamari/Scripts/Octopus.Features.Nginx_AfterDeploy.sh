#!/bin/bash
set -e

nginxTempDir=$(get_octopusvariable "OctopusNginxFeatureTempDirectory")
nginxConfDir=$(get_octopusvariable "Octopus.Action.Nginx.ConfigurationsDirectory")

nginxConfRoot=${nginxConfDir:-/etc/nginx/conf.d}
echo "Copying ${nginxTempDir}/conf/* to ${nginxConfRoot}..."
sudo cp -R ${nginxTempDir}/conf/* ${nginxConfDir:-/etc/nginx/conf.d} -f

if [ -d "${nginxTempDir}/ssl" ]; then
    nginxSslDir=$(get_octopusvariable "Octopus.Action.Nginx.CertificatesDirectory")
    echo "Copying ${nginxTempDir}/ssl/* to ${nginxConfDir:-/etc/ssl}..."
    sudo cp -R ${nginxTempDir}/ssl/* ${nginxSslDir:-/etc/ssl} -f
fi

echo "Validating nginx configuration"
sudo nginx -t

echo "Reloading nginx configuration"
sudo nginx -s reload

sudo rm -rf ${nginxTempDir}