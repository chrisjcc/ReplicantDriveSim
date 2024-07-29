#!/bin/bash

# How to run example:
# automating the versioning process using Git tags or commit hashes
# semantic versioning, 1.1.0
# >> VERSION=$(git rev-parse --short HEAD)
# >> ./docker_push.sh $VERSION

# Check if version is provided as an argument
if [ -z "$1" ]; then
  echo "Usage: $0 <version>"
  exit 1
fi

# Set the version number
VERSION=$1

# Explanation
# - docker build: Builds the image using Docker BuildKit.
# - docker tag: Tags the image with your Docker Hub username and repository.
# - docker login: Authenticates you with Docker Hub.
# - docker push: Uploads the tagged image to your Docker Hub repository.

# Build the Docker image using BuildKit
DOCKER_BUILDKIT=1 docker build --ssh default -t replicant_drive_sim .

# Tag the Docker image with your Docker Hub repository name and version
docker tag replicant_drive_sim chrisjcc/replicantdrivesim:$VERSION

# Tag the Docker image as the latest version
docker tag replicant_drive_sim chrisjcc/replicantdrivesim:latest

# Log in to Docker Hub
docker login

# Push the Docker image with the version tag to Docker Hub
docker push chrisjcc/replicantdrivesim:$VERSION

# Push the Docker image with the latest tag to Docker Hub
docker push chrisjcc/replicantdrivesim:latest

echo "Docker images chrisjcc/replicantdrivesim:$VERSION and chrisjcc/replicantdrivesim:latest pushed successfully."

