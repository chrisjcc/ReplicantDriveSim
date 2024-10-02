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

# Set working directory
WORKDIR /app/repo/Assets/Plugins/TrafficSimulation

# Build C++ binary executable
RUN mkdir build && \
    cd build && \
    cmake .. && \
    make && \
    cd ..

# Final stage
FROM ubuntu:22.04

# Copy the built executable from the cpp-build stage
COPY --from=cpp-build /app/repo/Assets/Plugins/TrafficSimulation/build /app/build

# Set the working directory
WORKDIR /app

# This Dockerfile is intended for use in a GitHub workflow to build the C++ library.
# No ENTRYPOINT or CMD is specified as the resulting image is not intended to be run directly.
