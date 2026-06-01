#!/bin/bash

# Exit on error
set -e

# Determine the directory where compile.sh is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Default values
OUTPUT_DIR="$HOME/.local/bin"
LINK_NAME="forge-dev"

# Parse command-line arguments (optional custom output directory or link name)
if [ -n "$1" ]; then
    OUTPUT_DIR="$1"
fi
if [ -n "$2" ]; then
    LINK_NAME="$2"
fi

# Create the output directory if it doesn't exist
mkdir -p "$OUTPUT_DIR"

echo "Compiling project using Forge build system..."
# Run the forge build system
forge build

# Check if build/forge was successfully created
if [ ! -f "build/forge" ]; then
    echo "Error: Build output build/forge not found!"
    exit 1
fi

# Create the symlink in the output directory
echo "Linking executable to $OUTPUT_DIR/$LINK_NAME..."
ln -sf "$SCRIPT_DIR/build/forge" "$OUTPUT_DIR/$LINK_NAME"

echo "Compilation and linking complete. You can run the dev executable via: $LINK_NAME"
