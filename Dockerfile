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

# Activate the Conda environment
SHELL ["conda", "run", "-n", "base", "/bin/bash", "-c"]

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

# Print current working directory and list files before running build.sh
RUN echo "Current working directory before build:" && \
    pwd && \
    echo "Files and directories before build:" && \
    ls -la

# Create a build script that uses secrets
RUN echo '#!/bin/bash\n\
set -e\n\
unity_license_file="/run/secrets/unity_license"\n\
if [ -f "$unity_license_file" ]; then\n\
    echo "Unity license file found. Content:"\n\
    cat "$unity_license_file"\n\
    echo "Copying license file..."\n\
    mkdir -p /root/.local/share/unity3d/Unity\n\
    cp "$unity_license_file" /root/.local/share/unity3d/Unity/Unity_lic.ulf\n\
    echo "License file copied. Content of Unity_lic.ulf:"\n\
    cat /root/.local/share/unity3d/Unity/Unity_lic.ulf\n\
    echo "Running Unity build..."\n\
    unity-editor \
      -quit \
      -batchmode \
      -nographics \
      -logFile /unity-project/unity_build.log \
      -username "$UNITY_EMAIL" \
      -password "$UNITY_PASSWORD" \
      -projectPath "/unity-project" \
      -executeMethod UnityDriveSimulation.BuildScript.PerformMacOSBuild \
      || (echo "Unity build failed. Unity build log:" && cat /unity-project/unity_build.log && exit 1)\n\
    echo "Unity build completed."\n\
else\n\
    echo "Unity license file is missing."\n\
    exit 1\n\
fi' > /build.sh \
&& chmod +x /build.sh

# Use Docker secrets to pass sensitive information
RUN --mount=type=secret,id=unity_license \
    /build.sh

# Print current working directory and list files after running build.sh
RUN echo "Current working directory after build:" && \
    pwd && \
    echo "Files and directories after build:" && \
    ls -la

# Final stage
FROM ubuntu:22.04 as final

WORKDIR /unity-project

# Copy build artifacts and logs from the unity-build stage
COPY --from=unity-build /unity-project/output /unity-project/output
COPY --from=unity-build /unity-project/unity_build.log /unity-project/unity_build.log

# Set up entrypoint script
RUN echo '#!/bin/bash\n\
echo "Unity build log:"\n\
cat /unity-project/unity_build.log\n\
echo "\nBuild artifacts:"\n\
ls -lR /unity-project/output\n\
if [ -d /unity-project/output ] && [ "$(ls -A /unity-project/output)" ]; then\n\
    echo "Build artifacts found."\n\
    exit 0\n\
else\n\
    echo "No build artifacts found. Build may have failed."\n\
    exit 1\n\
fi' > /entrypoint.sh \
&& chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
