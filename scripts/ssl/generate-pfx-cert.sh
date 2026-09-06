#!/bin/bash

# Script to generate a self-signed certificate PFX file using localhost.conf
#
# Usage: ./generate-pfx-cert.sh [-o output.pfx] [-p password]
#   -o  Output PFX filename (default: localhost.pfx)
#   -p  Password to protect the PFX file (default: none)
#   -h  Show this help message

set -e

# Configuration
CONFIG_FILE="localhost.conf"
KEY_FILE="localhost.key"
CERT_FILE="localhost.crt"
PFX_FILE="localhost.pfx"
DAYS_VALID=365
PFX_PASSWORD=""  # Empty password for no password protection

usage() {
    echo "Usage: $0 [-o output.pfx] [-p password]"
    echo ""
    echo "  -o FILE      Output PFX filename (default: localhost.pfx)"
    echo "  -p PASSWORD  Password to protect the PFX file (default: none)"
    echo "  -h           Show this help message"
    exit 1
}

while getopts "o:p:h" opt; do
    case "$opt" in
        o) PFX_FILE="$OPTARG" ;;
        p) PFX_PASSWORD="$OPTARG" ;;
        h) usage ;;
        *) usage ;;
    esac
done

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Generating self-signed certificate...${NC}"

# Check if config file exists
if [ ! -f "$CONFIG_FILE" ]; then
    echo "Error: Configuration file '$CONFIG_FILE' not found!"
    exit 1
fi

# Generate private key and self-signed certificate
openssl req -x509 \
    -newkey rsa:4096 \
    -keyout "$KEY_FILE" \
    -out "$CERT_FILE" \
    -days $DAYS_VALID \
    -nodes \
    -config "$CONFIG_FILE"

# Convert to PFX format
if [ -z "$PFX_PASSWORD" ]; then
    # No password
    openssl pkcs12 -export \
        -out "$PFX_FILE" \
        -inkey "$KEY_FILE" \
        -in "$CERT_FILE" \
        -passout pass:
else
    # With password
    openssl pkcs12 -export \
        -out "$PFX_FILE" \
        -inkey "$KEY_FILE" \
        -in "$CERT_FILE" \
        -passout pass:"$PFX_PASSWORD"
fi

# Clean up intermediate files
rm -f "$KEY_FILE" "$CERT_FILE"

echo -e "${GREEN}✓ Certificate generation complete!${NC}"
echo ""
echo "Generated file:"
echo "  PFX File:    $PFX_FILE"
echo "  Valid for:   $DAYS_VALID days"
echo "  Password:    ${PFX_PASSWORD:-<none>}"
echo ""
echo "To view certificate details, run:"
echo "  openssl pkcs12 -in $PFX_FILE -nokeys -passin pass: | openssl x509 -text -noout"
