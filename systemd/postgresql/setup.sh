#!/bin/bash

echo "==> Creating Postgres SuperUser Password"
ACCESS_KEY="$(openssl rand -base64 36 | tr -d '\n')"
echo -n "$ACCESS_KEY" | podman secret create pm-db-pass -
echo "Postgres SuperUser Password Created.."

echo ""
echo "This is your access credentials for Postgres:"
echo "  Access Key: $ACCESS_KEY"
