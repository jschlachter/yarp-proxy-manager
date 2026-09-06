#!/bin/bash

echo "==> Creating RustFS Access Key"
ACCESS_KEY="$(openssl rand -base64 36 | tr -d '\n')"
echo -n "$ACCESS_KEY" | podman secret create rfs-access-key -
echo "RustFS access key Created.."

echo "==> Create RustFS Secret Key"
SECRET_KEY="$(openssl rand -base64 60 | tr -d '\n')"
echo -n "$SECRET_KEY" | podman secret create rfs-secret-key -
echo "RustFS secret key created..."

echo ""
echo "These are your access credentials for RustFS:"
echo "  Access Key: $ACCESS_KEY"
echo "  Secret Key: $SECRET_KEY"
