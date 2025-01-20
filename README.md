---
layout: default
title: "ReplicantDriveSimulation"
permalink: /External/
---

![Workflow Status](https://github.com/chrisjcc/ReplicantDriveSim/actions/workflows/deploy-gh-pages.yml/badge.svg?branch=main)

# ReplicantDriveSim Library

**ReplicantDriveSim** is an advanced and fully integrated traffic simulation library designed to bridge Unity, Python, and C++ environments, providing seamless support for autonomous driving research and multi-agent reinforcement learning (RL).

[Traffic Simulation Documentation](https://chrisjcc.github.io/ReplicantDriveSim/)

![ReplicantDriveSim](https://raw.githubusercontent.com/chrisjcc/ReplicantDriveSim/main/External/images/ReplicantDriveSim.png)

## Features and Capabilities

### 1. Pip Installable
The Unity-based traffic simulation is now registered with PyPi, making it easily installable via pip:

```bash
pip install replicantdrivesim
```

This command simplifies the setup process by downloading and installing the package along with its dependencies. Once installed, you can quickly integrate it into your projects by importing the library in your Python scripts.

```python
import replicantdrivesim
```

For more detailed usage instructions, please refer to the documentation in the repository.


### 2. Automated Release Pipeline
The project is equipped with a robust continuous integration and deployment (CI/CD) pipeline that:

- Publishes releases to:
  - DockerHub
  - GitHub Container Registry
  - PyPi
- Generates comprehensive documentation on:
  - Read the Docs
  - GitHub Pages
  - Doxygen for C++ code
- Automatically rebuilds and updates the Unity simulation and Python package when updates are made to:
  - C++ code
  - Unity environment
  - Python interface

### 3. Multi-Agent Reinforcement Learning with Ray RLlib
- The library supports **Ray's RLlib**, enabling multi-agent training for autonomous driving scenarios.
- With a simple import, you can hand over the environment to Ray for scalable and efficient agent training.
- This feature is designed for large-scale experiments, leveraging Ray's distributed architecture.

### 4. Comprehensive Documentation
Doxygen-generated documentation is available for the C++ code, and additional project documentation is published on Read the Docs and GitHub Pages, ensuring developers have clear and detailed guidance for integration and usage.

- **Doxygen**-generated documentation is available for the C++ code.
- Additional project documentation is published on:
  - Read the Docs
  - GitHub Pages
- This ensures developers have clear and detailed guidance for integration and usage.

### 5. Versioning and Release Management
The pipeline handles release notes generation and tags releases automatically, providing a fully managed versioning system for developers to track changes and improvements.

- The pipeline handles automatic release note generation and tagging of releases.
- This ensures a fully managed versioning system, allowing developers to track changes and improvements seamlessly.

With ReplicantDriveSim, you can simulate complex traffic environments, leverage reinforcement learning for training autonomous agents, and benefit from a unified system that integrates C++, Unity, and Python, all within a streamlined workflow.

### Traffic Simulation Documentation - Further Reading
For more detailed information about the ReplicantDriveSim traffic simulation, including the AI components and additional developer resources, please refer to the following documentation sources:

- [AI Components and Reinforcement Learning](https://chrisjcc.github.io/ReplicantDriveSim/External/): This page covers the artificial intelligence aspects of the simulation, including reinforcement learning, imitation learning, and how these are implemented within the environment.
- [GitHub Repository](https://github.com/chrisjcc/ReplicantDriveSim/): The official GitHub repository where the Unity codebase and related assets for ReplicantDriveSim are hosted. Developers can explore the full source code, contribute to the project, and access the latest updates.
- [Read the Docs](https://replicantdrivesim.readthedocs.io/en/latest/): Comprehensive documentation covering various aspects of the simulation, including installation, setup, API references, and examples.

These resources will provide deeper insights and technical details to aid in development and understanding of the traffic simulation system.

### Resources and Installation
- [PyPI Project](https://pypi.org/project/ReplicantDriveSim/): Python package for the Unity traffic simulation with a C++ backend, providing high-performance tools for autonomous driving research and testing.
- [DockerHub Registry](https://hub.docker.com/repository/docker/chrisjcc/replicantdrivesim/general): Pre-built Docker images for seamless deployment of the ReplicantDriveSim environment across various platforms (currently set to be a private repository).

### Docker Image
For a pre-configured environment with all dependencies, use our Docker image:
```bash
docker pull chrisjcc/replicantdrivesim:latest
docker run -it chrisjcc/replicantdrivesim:latest
```

### License
ReplicantDriveSim is licensed under the MIT License. See the [LICENSE](https://github.com/chrisjcc/ReplicantDriveSim/blob/main/LICENSE) file for more details.

