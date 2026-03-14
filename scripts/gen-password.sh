#!/usr/bin/env bash
# Generates a cryptographically secure 16-character password

LC_ALL=C tr -dc 'A-Za-z0-9\-_\.~!$&^' < /dev/urandom | head -c 16
echo
