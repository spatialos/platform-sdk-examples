#!/usr/bin/env bash

set -e -x

cd "$(dirname "$0")"

BUILD_DIR="$(pwd)"
DOWNLOAD_DIR="${BUILD_DIR}/deps"
SDK_VERSION="13.4.0-b6699-4526b-WORKER-SNAPSHOT"
mkdir -p "${DOWNLOAD_DIR}"

# Download C# Worker SDK
if [ ! -f "$DOWNLOAD_DIR/csharp.zip" ]; then
  spatial package retrieve --force worker_sdk csharp "${SDK_VERSION}" "$DOWNLOAD_DIR/csharp.zip"
fi
if [ ! -f "${DOWNLOAD_DIR}/core-dynamic-x86_64-win32.zip" ]; then
  spatial package retrieve --force worker_sdk core-dynamic-x86_64-win32 "${SDK_VERSION}" "${DOWNLOAD_DIR}/core-dynamic-x86_64-win32.zip"
fi
if [ ! -f "${DOWNLOAD_DIR}/core-dynamic-x86_64-linux.zip" ]; then
  spatial package retrieve --force worker_sdk core-dynamic-x86_64-linux "${SDK_VERSION}" "${DOWNLOAD_DIR}/core-dynamic-x86_64-linux.zip"
fi
if [ ! -f "${DOWNLOAD_DIR}/core-dynamic-x86_64-macos.zip" ]; then
  spatial package retrieve --force worker_sdk core-dynamic-x86_64-macos "${SDK_VERSION}" "${DOWNLOAD_DIR}/core-dynamic-x86_64-macos.zip"
fi

pushd "$DOWNLOAD_DIR"
unzip -o "${DOWNLOAD_DIR}/csharp.zip"
unzip -o "${DOWNLOAD_DIR}/core-dynamic-x86_64-win32.zip"
unzip -o "${DOWNLOAD_DIR}/core-dynamic-x86_64-linux.zip"
unzip -o "${DOWNLOAD_DIR}/core-dynamic-x86_64-macos.zip"
popd
echo "Worker SDK dependencies downloaded"
