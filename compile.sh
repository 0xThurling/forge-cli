#!/bin/bash

# Default values
TARGET_OS=""
OUTPUT_DIR="$HOME/.local/bin" # Changed to ./.local/bin
RID=""

# Parse command-line arguments
if [ -n "$1" ]; then
    TARGET_OS="$1"
fi

case "$TARGET_OS" in
    "windows")
        RID="win-x64"
        OUTPUT_DIR="$HOME/.local/bin/"
        ;;
    "macos")
        RID="osx-x64"
        OUTPUT_DIR="$HOME/.local/bin/"
        ;;
    "linux")
        RID="linux-x64"
        OUTPUT_DIR="$HOME/.local/bin/"
        ;;
    "")
        echo "Usage: $0 <windows|macos|linux>"
        echo "No target OS specified. Attempting to compile for current OS."
        # Attempt to determine current OS if no argument is provided
        case "$(uname -s)" in
            Linux*)     RID="linux-x64"; OUTPUT_DIR="$HOME/.local/bin";;
            Darwin*)    RID="osx-x64"; OUTPUT_DIR="$HOME/.local/bin";;
            CYGWIN*|MINGW32*|MSYS*|MINGW*) RID="win-x64"; OUTPUT_DIR="$HOME/.local/bin";;
            *)          echo "Unknown OS. Please specify target OS or add support for your OS."; exit 1;;
        esac
        ;;
    *)
        echo "Invalid target OS: $TARGET_OS"
        echo "Usage: $0 <windows|macos|linux>"
        exit 1
        ;;
esac

# Create the output directory if it doesn't exist
mkdir -p "$OUTPUT_DIR"

echo "Compiling for $TARGET_OS ($RID) into $OUTPUT_DIR"

# Publish the project for a single file executable
dotnet publish -c Release -r "$RID" -o "$OUTPUT_DIR" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

echo "Compilation complete. Executable is in $OUTPUT_DIR"
