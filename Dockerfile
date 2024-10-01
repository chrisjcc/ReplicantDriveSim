# syntax=docker/dockerfile:1.2

# Stage 1: Intermediate C++ build stage
FROM ubuntu:22.04 as cpp-intermediate

# Install necessary packages
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    wget \
    bzip2 \
    ca-certificates \
    libglib2.0-0 \
    libxext6 \
    libsm6 \
    libxrender1 \
    mercurial \
    git

# Clone the repository using HTTPS
RUN git clone  --recurse-submodules --shallow-submodules https://github.com/chrisjcc/ReplicantDriveSim.git /app/repo

# Stage 2: C++ build stage
FROM ubuntu:22.04 as cpp-build

# Install wget, build tools, and other necessary dependencies
RUN apt-get update && apt-get install -y \
    wget \
    bzip2 \
    ca-certificates \
    build-essential \
    cmake \
    git

# Copy the repository from the intermediate image
COPY --from=cpp-intermediate /app/repo /app/repo

# Install Miniforge
ARG TARGETARCH
ENV CONDA_DIR=/opt/conda

RUN echo "Building for architecture: ${TARGETARCH}"
RUN ARCH=$(echo ${TARGETARCH} | sed 's/amd64/x86_64/;s/arm64/aarch64/') && \
    wget https://github.com/conda-forge/miniforge/releases/latest/download/Miniforge3-Linux-${ARCH}.sh -O ~/miniforge.sh && \
    /bin/bash ~/miniforge.sh -b -p $CONDA_DIR && \
    rm ~/miniforge.sh && \
    $CONDA_DIR/bin/conda clean -afy

# Initialize Conda and create environment
RUN /bin/bash -c "source $CONDA_DIR/bin/activate && \
    conda init bash && \
    conda update -n base -c defaults conda -y && \
    ls -larth /app/repo/Assets/Plugins/TrafficSimulation/ && \
    conda env create -f /app/repo/Assets/Plugins/TrafficSimulation/environment.yml && \
    conda clean -afy"

# Stage 3: Unity build stage
FROM unityci/editor:ubuntu-2022.3.3f1-linux-il2cpp-2.0.0 as unity-build

# Install necessary dependencies
RUN apt-get update && apt-get install -y \
    wget \
    unzip \
    coreutils \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /unity-project

# Copy your Unity project files and C++ build artifacts
COPY . .
COPY --from=cpp-build /app/repo/Builds/macOS /unity-project/Assets/Plugins/

# Create a build script
RUN echo '#!/bin/bash\n\
unity-editor \
  -quit \
  -batchmode \
  -nographics \
  -projectPath "/unity-project" \
  -executeMethod UnityDriveSimulation.BuildScript.PerformMacOSBuild \
  -logFile "/unity-project/Logs/logfile.log"\n\
mkdir -p /unity-project/output\n\
if [ -d "/unity-project/Builds/macOS" ]; then \
    ls -larth /unity-project/Builds/macOS/\n\
    cp -r /unity-project/Builds/macOS/* /unity-project/output/\n\
else \
    echo "Build directory not found. Check Unity logs for errors."\n\
fi' > /build.sh \
&& chmod +x /build.sh

# Set the entrypoint to the build script
ENTRYPOINT ["/build.sh"]
