#!/bin/bash
set -ex

REPO_ROOT="$(dirname "${BASH_SOURCE[0]}")/.."
DOCKERFILE_PATH=$REPO_ROOT/Dockerfile
DOCKER_IMAGE=csharp/scenarios

docker build -t $DOCKER_IMAGE $REPO_ROOT
docker run \
    --env IMPROBABLE_REFRESH_TOEKN=$IMPROBABLE_REFRESH_TOEKN \
    $DOCKER_IMAGE
