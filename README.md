---
layout: default
title: "ReplicantDriveSim"
permalink: /
---

# ReplicantDriveSim

ReplicantDriveSim is an advanced traffic simulation project designed for autonomous driving research. It leverages reinforcement learning, imitation learning, and computer vision to create realistic traffic scenarios and synthetic driving data. The simulation environment is built using Pygame for visualization and Miniforge for Python package management, ensuring a seamless development and deployment experience. This Docker image provides a fully configured environment with all necessary dependencies, enabling efficient experimentation and development of autonomous driving algorithms.

[Traffic Simulation Documentation](https://chrisjcc.github.io/ReplicantDriveSim/)

[Doxygen Documentation](https://chrisjcc.github.io/ReplicantDriveSim/External/docs/html/)

<!-- Try the first image source -->
![Jekyll Alt text]({{ site.baseurl }}/External/images/NISSAN-GTR_ReplicantDriveSim.png)

<!-- Fallback to the second image source -->
![HTML Alt text](/External/images/NISSAN-GTR_ReplicantDriveSim.png)

## Project Setup

### Required Unity Version

This project was developed using Unity 2022.3.39f1 (LTS). To ensure compatibility, please use this version or later Long-Term Support (LTS) versions of Unity. You can download the specific version from the Unity Hub or the Unity Archive.

#### Installation:

1. Clone the repository:
    ```shell
    git clone git@github.com:chrisjcc/ReplicantDriveSim.git
    ```
2. Open the project in Unity Hub and select Unity version 2022.3.39f1.
3. Let Unity install any necessary packages and dependencies.

## Generate Doxygen Documentation

To generate the documentation for this project using Doxygen, follow these steps:

### Prerequisites

Make sure Doxygen is installed on your local machine. You can install it using the following commands:

- **Ubuntu:**
    ```bash
    sudo apt-get install doxygen
    sudo apt-get install graphviz
    ```
- **macOS:**
    ```bash
    brew install doxygen
    brew install graphviz
    ```

### Generate Documentation

1. Navigate to the root directory of your project where the Doxyfile is located.
2. Run the Doxygen command with your Doxyfile to generate the documentation:
    ```bash
    doxygen Doxyfile
    ```
3. The generated HTML files can be found in the directory specified by the OUTPUT_DIRECTORY setting in the Doxyfile (typically docs/html).
