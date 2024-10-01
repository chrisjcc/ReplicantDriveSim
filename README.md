---
layout: default
title: "ReplicantDriveSim"
permalink: /
---

![Workflow Status](https://github.com/chrisjcc/ReplicantDriveSim/actions/workflows/publish-gh-pages.yml/badge.svg?branch=main)

# ReplicantDriveSim

ReplicantDriveSim is an advanced traffic simulation project designed for autonomous driving research. It leverages reinforcement learning, imitation learning, and computer vision to create realistic traffic scenarios and synthetic driving data. The simulation environment is built using Pygame for visualization and Miniforge for Python package management, ensuring a seamless development and deployment experience. This Docker image provides a fully configured environment with all necessary dependencies, enabling efficient experimentation and development of autonomous driving algorithms.

[Traffic Simulation Documentation](https://chrisjcc.github.io/ReplicantDriveSim/)

[Doxygen Documentation](https://chrisjcc.github.io/ReplicantDriveSim/External/docs/html/)

[AI Page](https://chrisjcc.github.io/ReplicantDriveSim/rl/)

[GitHub Page](https://github.com/chrisjcc/ReplicantDriveSim/)

[Read the Docs](https://replicantdrivesim.readthedocs.io/en/latest/)

![Nissan GTR](https://raw.githubusercontent.com/chrisjcc/ReplicantDriveSim/main/External/images/NISSAN-GTR_ReplicantDriveSim.png)

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


#### Building the Unity Project from Command Line

To generate an executable for this Unity project without opening the Unity Editor, you can use the following command in your terminal:

```bash
/Applications/Unity/Hub/Editor/2022.3.33f1/Unity.app/Contents/MacOS/Unity \
-quit \
-batchmode \
-nographics \
-projectPath "/Users/christiancontrerascampana/Desktop/project/unity_traffic_simulation/reduce_git_lfs/UnityDriveSimulation" \
-executeMethod UnityDriveSimulation.BuildTools.BuildScript.PerformMacOSBuild \
-logFile "Logs/logfile.log"
```

##### Command Breakdown

Let's break down what each part of this command does:

1. `/Applications/Unity/Hub/Editor/2022.3.33f1/Unity.app/Contents/MacOS/Unity`
   - This is the path to the Unity executable on macOS. Adjust this path if your Unity installation is in a different location or if you're using a different version.

2. `-quit`
   - This parameter tells Unity to quit after executing the build process. It ensures that Unity doesn't stay open after the build is complete.

3. `-batchmode`
   - This runs Unity in batch mode, which means it operates without launching the graphical user interface. This is essential for automated builds and running Unity from the command line.

4. `-projectPath "/Users/christiancontrerascampana/Desktop/project/unity_traffic_simulation/reduce_git_lfs/UnityDriveSimulation"`
   - This specifies the path to your Unity project. Replace this with the actual path to your project on your machine.

5. `-executeMethod UnityDriveSimulation.BuildTools.BuildScript.PerformMacOSBuild`
   - This tells Unity which method to execute to perform the build. In this case, it's calling the `PerformMacOSBuild` method from the `BuildScript` class in the `UnityDriveSimulation.BuildTools` namespace.

6. `-logFile "Logs/logfile.log"`
   - This specifies where Unity should output its log file. This is useful for debugging if something goes wrong during the build process.

##### Usage

To use this command:

1. Open a terminal window.
2. Navigate to the directory containing your Unity project.
3. Copy and paste the above command, making sure to adjust the Unity executable path and project path as necessary for your system.
4. Press Enter to run the command.

The build process will start, and Unity will generate the executable based on your project settings and the build script you've defined in `UnityDriveSimulation.BuildTools.BuildScript.PerformMacOSBuild`.

#### Note

Ensure that the build script (`BuildScript.cs`) is properly set up in your Unity project and that the `PerformMacOSBuild` method is correctly implemented to build your project for macOS.


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
