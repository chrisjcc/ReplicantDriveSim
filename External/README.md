---
layout: default
title: "Traffic Architecture"
permalink: /External/
---

# Build and Install the Traffic Simulation Library

<img src="https://raw.githubusercontent.com/chrisjcc/ReplicantDriveSim/main/External/images/NISSAN-GTR_ReplicantDriveSim_Raycasting.png" alt="Nissan GTR" width="800" height="600"/>

## Verifying Git LFS Installation

To check if Git LFS is correctly installed and initialized, you can run:
```shell
git lfs version
```

Check if specific files are being tracked by Git LFS using:
```shell
git lfs ls-files
```

Fetching LFS Files:
```shell
git lfs pull
```

## Initialize Submodules
This will initialize and clone the submodule repository and check out the appropriate commit specified in the parent repository.
```shell
git submodule update --init --recursive
```

## Verify the submodule status
Ensure that the submodule is now populated correctly.
```shell
git submodule status
```

## Environment setup
```shell
conda env create -f environment.yml
conda activate drive
```

# Build and check setup
```shell
export MACOSX_DEPLOYMENT_TARGET=14.0
TRAFFIC_SIM_SOURCEDIR=$PWD python -m build -v
unzip -l dist/simulation-*.whl
```

or configure and build a stand alone traffic library
```shell
mkdir build
cd build
cmake ..
make
cd ..
```
This will build the Google Test library (libgtest.a and libgtest_main.a).

## Install Traffic Simulation
```python
pip install --force-reinstall dist/simulation-*.whl
```

### Example Usage

```python
## Import the compiled C++ module
import traffic_simulation

## Create a traffic environment
simulation = traffic_simulation.TrafficSimulation(2)

## Retrieve the current states of agents in the traffic environment
states = simulation.get_agent_positions()

## Display the states of agents in the traffic environment
for agent, state in states.items():
    print(f"{agent} state: {state}")

## Advance the environment by one step
simulation.step([1, 0], [[0.1, 0.2, 0.3], [0.0, 0.0, 0.0]])

## Update the states of agents in the traffic environment
states = simulation.get_agent_positions()

## Display the updated states of agents in the traffic environment
for agent, state in states.items():
    print(f"{agent} state: {state}")
```

### Docker build
```shell
# Build docker image
export DOCKER_BUILDKIT=1 
docker-compose up --build
# Run docker image
docker run -it --rm -v $(pwd)/data:/app/repo/data replicantdrivesim-app bash

# Alternative build method
DOCKER_BUILDKIT=1 docker build --ssh default -t replicantdrivesim-app .

# Close docker container
docker-compose down
docker-compose down --volumes
docker-compose down --volumes --remove-orphans

docker system prune -a
docker system prune -a --volumes

docker-compose ps -a
docker-compose logs

# Exec into the Running Container (app is name of service in docker-compose.yml)
docker-compose exec app bash
```

```python
python simulacrum.py

# Or

python trainer.py
```

## Monitor simulation session
```shell
mlflow ui --backend-store-uri file:mlruns
```

## Run unit tests
```shell
./build/tests/traffic_simulation_test
./build/tests/perception_module_test
```

Google Test provides robust features for writing and organizing unit tests in C++. Customize your test structure (TEST_F, TEST, etc.) as per your project requirements.


## Steps to Create a Git Tag and Push It to a Remote Repository
### Create a New Tag Locally
To create an annotated tag, use the following command. Replace `v1.0.0` with your desired tag name and customize the message as needed:
```shell
git tag -a v1.0.0 -m "Release version 1.0.0"
```
- -a `v1.0.0`: This option creates an annotated tag with the name `v1.0.0`.
- -m "Release version 1.0.0": This option adds a message to the tag, which is stored with it.

### Push the Tag to the Remote Repository
Once the tag is created locally, push it to the remote repository:
```shell
git push origin v1.0.0
```
- `origin`: The name of the remote repository (typically `origin` by default).
- `v1.0.0`: The name of the tag you created.

## Triggering the Workflow Manually via the Command Line
Trigger the Workflow Using curl:
```shell
curl -X POST \
  -H "Authorization: token GITHUB_PERSONAL_ACCESS_TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/YOUR_USERNAME/YOUR_REPOSITORY/actions/workflows/YOUR_WORKFLOW_FILE.yml/dispatches \
  -d '{"ref":"main"}'
```
- `GITHUB_PERSONAL_ACCESS_TOKEN`: Your personal access token.
- `YOUR_USERNAME`: Your GitHub username.
- `YOUR_REPOSITORY`: Your repository name.
- `YOUR_WORKFLOW_FILE.yml`: The filename of your workflow YAML (e.g., publish.yml).
- `main`: The branch you want the workflow to run against.

## Pulling Docker Images from DockerHub Registry
This section provides instructions on how to interact with DockerHub to pull Docker images, including steps for accessing private repositories and running containers with shell access.

[DockerHub Registry](https://hub.docker.com/repository/docker/chrisjcc/replicantdrivesim/general)


### Docker Login
If the Docker image repository is private, you will need to authenticate with Docker before pulling the image. To log in, use the following command:
```shell
docker login
```
Enter your Docker Hub credentials when prompted. After successfully logging in, you will have access to pull and run the private repository images.

### Running the Container with Port Mapping and Shell Access
To run the Docker container with port mapping and gain interactive shell access, use the following command:
```shell
docker run -it -p 8080:80 chrisjcc/replicantdrivesim /bin/bash
```

This command will map port 8080 on your host to port 80 in the container and start an interactive Bash shell within the container.

### Running the Container with Shell Access
If you only need shell access without port mapping, you can run the container interactively using:
```shell
docker run -it chrisjcc/replicantdrivesim /bin/bash
```
This command starts the container and opens a Bash shell, allowing you to interact directly with the container environment.


### Create a Release
Follow the instructions below to generate a Git tag, which will trigger a software release.

```shell
git fetch --tags  # Retrieve all tags from the remote repository
git tag -a v0.1.0 -m "Version 0.1.0"  # Create a new annotated tag for version 0.1.0
git push origin v0.1.0  # Push the newly created tag to the remote repository
git fetch --tags  # Refresh the local tags list
git tag -l  # List all tags to verify the new tag is present
```
