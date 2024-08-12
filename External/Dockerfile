# syntax=docker/dockerfile:1.2

# This is our first build stage, it will not persist in the final image
FROM ubuntu:22.04 as intermediate

# Install git
RUN apt-get update && apt-get install -y git

# Clone the repository using HTTPS
RUN git clone --recurse-submodules https://github.com/chrisjcc/ReplicantDriveSim.git

# Final image setup
FROM ubuntu:22.04

# Install a nano text editor
RUN apt-get update && apt-get install -y nano

# Install necessary packages
RUN apt-get update && apt-get install -y \
    nano \
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

# Copy repository from the intermediate image
COPY --from=intermediate /ReplicantDriveSim /app/repo

# Install Miniforge
#ARG TARGETARCH=x86_64
ARG TARGETARCH
ENV CONDA_DIR=/opt/conda

RUN ARCH=$(echo ${TARGETARCH} | sed 's/amd64/x86_64/;s/arm64/aarch64/') && \
    wget https://github.com/conda-forge/miniforge/releases/latest/download/Miniforge3-Linux-${ARCH}.sh -O ~/miniforge.sh && \
    /bin/bash ~/miniforge.sh -b -p $CONDA_DIR && \
    rm ~/miniforge.sh && \
    $CONDA_DIR/bin/conda clean -afy

# Initialize Conda
RUN /bin/bash -c "source $CONDA_DIR/bin/activate && \
    conda init bash"

# Update Conda and create an environment
RUN /bin/bash -c "source $CONDA_DIR/bin/activate && \
    conda update -n base -c defaults conda -y && \
    conda env create -f /app/repo/External/environment.yml"

# Install build tools and build the wheel
RUN /bin/bash -c "source $CONDA_DIR/bin/activate drive && \
    pip install --upgrade pip && \
    pip install build && \
    cd /app/repo/External && \
    python -m build -v"

# Install the traffic simulation wheel
RUN /bin/bash -c "source $CONDA_DIR/bin/activate drive && \
    pip install --force-reinstall /app/repo/External/dist/simulation-*.whl"

# Set working directory
WORKDIR /app/repo/External

# Default command
CMD ["bash"]
