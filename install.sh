#!/bin/bash

# This script downloads and installs the latest release of cpm.

set -ex

# Get the latest release tag from GitHub.
LATEST_RELEASE=$(curl -s "https://api.github.com/repos/0xThurling/cpm/releases/latest" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')

if [ -z "$LATEST_RELEASE" ]; then
    echo "Error: Could not get the latest release tag from GitHub."
    exit 1
fi

# Determine the OS and architecture.
OS=$(uname -s | tr '[:upper:]' '[:lower:]')
ARCH=$(uname -m)

case "$OS" in
    linux)
        case "$ARCH" in
            x86_64)
                ASSET_NAME="cpm-linux-x64"
                ;;
            aarch64 | arm64)
                ASSET_NAME="cpm-linux-arm64"
                ;;
            *)
                echo "Error: Unsupported architecture for Linux: $ARCH"
                exit 1
                ;;
        esac
        ;;
    darwin)
        case "$ARCH" in
            x86_64)
                ASSET_NAME="cpm-osx-x64"
                ;;
            arm64)
                ASSET_NAME="cpm-osx-arm64"
                ;;
            *)
                echo "Error: Unsupported architecture for macOS: $ARCH"
                exit 1
                ;;
        esac
        ;;
    *)
        echo "Error: Unsupported OS: $OS"
        exit 1
        ;;
esac

# Construct the download URL.
DOWNLOAD_URL="https://github.com/0xThurling/cpm/releases/download/${LATEST_RELEASE}/${ASSET_NAME}"

# Download the asset.
echo "Downloading ${ASSET_NAME} from ${DOWNLOAD_URL}..."
curl -L -o cpm "${DOWNLOAD_URL}"

# Check what 'cpm' is after download
ls -ld cpm

# Make the binary executable.
chmod +x cpm

# Move the binary to /usr/local/bin.
echo "Installing cpm to /usr/local/bin..."
sudo mv cpm /usr/local/bin/cpm

echo "cpm has been installed successfully!"
