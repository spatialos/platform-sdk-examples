#!/usr/bin/env bash

set -e -x

cd "$(dirname "$0")"

WORKER_DIRS=(server_a server_b client)
BUILD_PLATFORMS=(macOS64 Windows64 Linux64)
BUILD_DIR="$(pwd)"
DOWNLOAD_DIR="${BUILD_DIR}/deps"
SDK_VERSION="13.3.0"
mkdir -p "${DOWNLOAD_DIR}"

function isLinux() {
  [[ "$(uname -s)" == "Linux" ]]
}

function isMacOS() {
  [[ "$(uname -s)" == "Darwin" ]]
}

function isWindows() {
  ! (isLinux || isMacOS)
}

# Return the target platform used by worker package names built for this OS.
function getPlatformName() {
  if isLinux; then
    echo "linux"
  elif isMacOS; then
    echo "macos"
  elif isWindows; then
    echo "win32"
  else
    echo "ERROR: Unknown platform." >&2
    exit 1
  fi
}
PLATFORM_NAME=$(getPlatformName)
BUILD_TOOL="xbuild"
if isWindows; then
  BUILD_TOOL="MSBuild.exe"
fi

# Get the tools
# * Spatial Schema compiler
# * Standard Library Schemas
# * Core SDK for all platforms to enable building workers for MacOS, Windows or Linux
# * C# SDK
# if [ ! -f "${DOWNLOAD_DIR}/schema_compiler-x86_64-${PLATFORM_NAME}.zip" ]; then
#   spatial package retrieve --force tools "schema_compiler-x86_64-${PLATFORM_NAME}" "${SDK_VERSION}" "${DOWNLOAD_DIR}/schema_compiler-x86_64-${PLATFORM_NAME}.zip"
# fi
# if [ ! -f "$DOWNLOAD_DIR/standard_library.zip" ]; then
#   spatial package retrieve --force schema standard_library "${SDK_VERSION}" "$DOWNLOAD_DIR/standard_library.zip"
# fi
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
# unzip -o "${DOWNLOAD_DIR}/schema_compiler-x86_64-${PLATFORM_NAME}.zip"
# unzip -o "${DOWNLOAD_DIR}/standard_library.zip"
unzip -o "${DOWNLOAD_DIR}/csharp.zip"
unzip -o "${DOWNLOAD_DIR}/core-dynamic-x86_64-win32.zip"
unzip -o "${DOWNLOAD_DIR}/core-dynamic-x86_64-linux.zip"
unzip -o "${DOWNLOAD_DIR}/core-dynamic-x86_64-macos.zip"
popd

# # For each worker:
# for WORKER in "${WORKER_DIRS[@]}"; do
#   pushd "${BUILD_DIR}/${WORKER}/src"
#   # Compile the schema
#   OUT_DIR=improbable/generated
#   mkdir -p "$OUT_DIR"
#   "$DOWNLOAD_DIR"/schema_compiler --schema_path="${BUILD_DIR}/SpatialOS/schema" --schema_path="$DOWNLOAD_DIR" --csharp_out="$OUT_DIR" --load_all_schema_on_schema_path "${BUILD_DIR}"/SpatialOS/schema/*.schema "${DOWNLOAD_DIR}"/improbable/*.schema

#   # Compile UserCode+SDK+C#Schema into a binary
#   mkdir -p improbable/dependencies/managed
#   mkdir -p improbable/dependencies/native
#   cp "$DOWNLOAD_DIR"/Improbable.WorkerSdkCsharp.dll improbable/dependencies/managed
#   cp "$DOWNLOAD_DIR"/*.dylib improbable/dependencies/native
#   cp "$DOWNLOAD_DIR"/*.dll improbable/dependencies/native
#   cp "$DOWNLOAD_DIR"/*.so improbable/dependencies/native
#   for PLATFORM in "${BUILD_PLATFORMS[@]}"; do
#     ${BUILD_TOOL} CsharpWorker.sln /property:Configuration=Release /property:Platform="$PLATFORM"
#     cp -r bin ..
#     rm -rf bin
#     rm -rf obj
#   done
#   popd
# done

# # Compile the schemas into a schema descriptor: compile schemas to proto, compile proto
# mkdir -p "${DOWNLOAD_DIR}/generated_protos"
# cp -r "${DOWNLOAD_DIR}"/proto/* "${DOWNLOAD_DIR}/generated_protos"
# mkdir -p "${BUILD_DIR}/SpatialOS/schema/bin"

# "${DOWNLOAD_DIR}/schema_compiler" --schema_path="${BUILD_DIR}/SpatialOS/schema" --schema_path="${DOWNLOAD_DIR}" --proto_out="${DOWNLOAD_DIR}/generated_protos" --load_all_schema_on_schema_path "${DOWNLOAD_DIR}"/improbable/*.schema "${BUILD_DIR}"/SpatialOS/schema/*.schema
# "${DOWNLOAD_DIR}/protoc" --proto_path="${DOWNLOAD_DIR}/generated_protos" --descriptor_set_out="${BUILD_DIR}/SpatialOS/schema/bin/schema.descriptor" --include_imports "${DOWNLOAD_DIR}"/generated_protos/*.proto "${DOWNLOAD_DIR}"/generated_protos/**/*.proto

# rm -r "${DOWNLOAD_DIR}/generated_protos"

# echo "Build complete"

