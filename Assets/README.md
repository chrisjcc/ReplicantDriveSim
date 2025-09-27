---
layout: default
title: "ReplicantDriveSim"
permalink: /
---

![Workflow Status](https://github.com/chrisjcc/ReplicantDriveSim/actions/workflows/deploy-gh-pages.yml/badge.svg?branch=main)

# Unity Traffic Simulation - Developer Guide

This README is intended for developers interested in contributing to the Unity codebase of the ReplicantDriveSim traffic simulation project. Below are instructions and guidelines for setting up the development environment, updating the codebase, and managing dependencies for the Unity portion of the simulation.

![Nissan GTR](https://raw.githubusercontent.com/chrisjcc/ReplicantDriveSim/main/External/images/NISSAN-GTR_ReplicantDriveSim.png)

## Accessing ReplicantDriveSim Library via PyPI

The `ReplicantDriveSim` library is available on the Python Package Index (PyPI). You can find it at [PyPI-ReplicantDriveSim](https://pypi.org/project/ReplicantDriveSim/).

# Project Setup

## Required Unity Version

The Unity project was developed using Unity 2022.3.39f1 (LTS). To maintain compatibility, it is recommended to use this version or a newer Long-Term Support (LTS) version. The required version can be downloaded via Unity Hub or the Unity Archive.


### Installation Steps:

1. **Clone the repository**:
    ```shell
    git clone git@github.com:chrisjcc/ReplicantDriveSim.git
    ```
2. **Open the project in Unity Hub**:
   - Add the project to Unity Hub and select Unity 2022.3.39f1 as the editor version.
3. **Install dependencies**:
   - Unity will automatically prompt you to install any required packages when the project is opened. Let it install these to ensure full functionality.

## Documentation Generation with Doxygen

To generate code documentation for the Unity codebase, Doxygen is used. Ensure you have Doxygen installed, and follow these steps to generate documentation:

1. Navigate to the project root.
2. Run the Doxygen configuration file to generate the documentation:
   ```bash
   doxygen Doxyfile
   ```
   This will produce HTML and LaTeX documentation for the project's source code, [Doxygen Documentation](https://chrisjcc.github.io/ReplicantDriveSim/External/docs/html/).

## Building the Unity Project

To automate the build process without manually opening the Unity Editor, you can build the project from the command line. The following command can be used to create an executable directly:
 
```bash
/Applications/Unity/Hub/Editor/2022.3.39f1/Unity.app/Contents/MacOS/Unity \
-quit \
-batchmode \
-nographics \
-projectPath "/path/to/your/unity_project" \
-executeMethod UnityBuilderAction.BuildScript.PerformMacOSBuild \
-logFile "Logs/build_logfile.log"
```

The build process will start, and Unity will generate the executable based on your project settings and the build script you've defined in `UnityBuilderAction.BuildScript.PerformMacOSBuild`. This command enables headless builds, making it easier to integrate with CI/CD pipelines or automated workflows

### Command Breakdown

Let's break down what each part of this command does:

1. `/Applications/Unity/Hub/Editor/2022.3.33f1/Unity.app/Contents/MacOS/Unity`
   - This is the path to the Unity executable on macOS. Adjust this path if your Unity installation is in a different location or if you're using a different version.

2. `-quit`
   - This parameter tells Unity to quit after executing the build process. It ensures that Unity doesn't stay open after the build is complete.

3. `-batchmode`
   - This runs Unity in batch mode, which means it operates without launching the graphical user interface. This is essential for automated builds and running Unity from the command line.

4. `-projectPath "/path/to/your/unity_project"`
   - This specifies the path to your Unity project. Replace this with the actual path to your project on your machine.

5. `-executeMethod UnityBuilderAction.BuildScript.PerformMacOSBuild`
   - This tells Unity which method to execute to perform the build. In this case, it's calling the `PerformMacOSBuild` method from the `BuildScript` class in the `UnityDriveSimulation.BuildTools` namespace.

6. `-logFile "Logs/logfile.log"`
   - This specifies where Unity should output its log file. This is useful for debugging if something goes wrong during the build process.

**Note**: Ensure that the build script (`BuildScript.cs`) is properly set up in your Unity project and that the `PerformMacOSBuild` method is correctly implemented to build your project for macOS.


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

## Contributing to the Unity Codebase

For developers looking to contribute, please ensure:
- All code changes follow the existing coding standards and guidelines.
- Proper documentation is provided for new features or modifications.
- Any new dependencies are documented, and Unity package files are updated accordingly.

Before submitting a pull request, please ensure your changes are thoroughly tested within the simulation environment.
