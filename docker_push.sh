# Explanation
# - docker build: Builds the image using Docker BuildKit.
# - docker tag: Tags the image with your Docker Hub username and repository.
# - docker login: Authenticates you with Docker Hub.
# - docker push: Uploads the tagged image to your Docker Hub repository.

# Build the Docker image using BuildKit
DOCKER_BUILDKIT=1 docker build --ssh default -t replicant_drive_sim .

# Tag the Docker image with your Docker Hub repository name
docker tag replicant_drive_sim chrisjcc/replicantdrivesim:latest

# Log in to Docker Hub
docker login

# Push the Docker image to Docker Hub
docker push chrisjcc/replicantdrivesim:latest
