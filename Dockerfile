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
RUN git clone --recurse-submodules --shallow-submodules https://github.com/chrisjcc/ReplicantDriveSim.git /app/repo

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
ENV PATH=$CONDA_DIR/bin:$PATH

RUN echo "Building for architecture: ${TARGETARCH}"
RUN ARCH=$(echo ${TARGETARCH} | sed 's/amd64/x86_64/;s/arm64/aarch64/') && \
    wget https://github.com/conda-forge/miniforge/releases/latest/download/Miniforge3-Linux-${ARCH}.sh -O ~/miniforge.sh && \
    /bin/bash ~/miniforge.sh -b -p $CONDA_DIR && \
    rm ~/miniforge.sh && \
    $CONDA_DIR/bin/conda clean -afy

# Initialize Conda
RUN /bin/bash -c "source $CONDA_DIR/bin/activate && conda init bash"

# Update Conda
RUN conda update -n base -c defaults conda -y

# Create Conda environment without pip packages first
#RUN conda env create -f /app/repo/Assets/Plugins/TrafficSimulation/environment.yml

# Activate the Conda environment
#SHELL ["conda", "run", "-n", "drive", "/bin/bash", "-c"]

# Clean Conda
RUN conda clean -afy

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

ARG UNITY_EMAIL
ARG UNITY_PASSWORD

# Create a build script that uses secrets
RUN echo '#!/bin/bash\n\
unity_license_file="/run/secrets/unity_license"\n\
if [ -f "$unity_license_file" ]; then\n\
    mkdir -p /root/.local/share/unity3d/Unity\n\
    cp "$unity_license_file" /root/.local/share/unity3d/Unity/Unity_lic.ulf\n\
    unity-editor \
      -quit \
      -batchmode \
      -nographics \
      -username "$UNITY_EMAIL" \
      -password "$UNITY_PASSWORD" \
      -projectPath "/unity-project" \
      -executeMethod UnityDriveSimulation.BuildScript.PerformMacOSBuild \
      -logFile "/unity-project/Logs/logfile.log"\n\
    if [ -d "/unity-project/Builds/macOS" ]; then \
        mkdir -p /unity-project/output\n\
        cp -r /unity-project/Builds/macOS/* /unity-project/output/\n\
        echo "Build artifacts copied to /unity-project/output/"\n\
    else \
        echo "Build directory not found. Check Unity logs for errors."\n\
        exit 1\n\
    fi\n\
else\n\
    echo "Unity license file is missing."\n\
    exit 1\n\
fi' > /build.sh \
&& chmod +x /build.sh


# Use Docker secrets to pass sensitive information
RUN --mount=type=secret,id=unity_license \
    /build.sh

# Stage 4: Final stage (without secrets)
FROM ubuntu:22.04 as final

WORKDIR /unity-project

# Copy only the build artifacts from the unity-build stage
COPY --from=unity-build /unity-project/output /unity-project/output

# Set the entrypoint to a simple command that lists the output
ENTRYPOINT ["ls", "-l", "/unity-project/output"]
