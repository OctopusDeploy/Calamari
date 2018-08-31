#!/bin/bash
set -e

echo "Validating nginx configuration"
sudo nginx -t

echo "Reloading nginx configuration"
sudo nginx -s reload