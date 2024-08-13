# syntax=docker/dockerfile:1.2

# This is our first build stage, it will not persist in the final image
#FROM ubuntu:22.04 as intermediate

# Build stage for Miniforge
FROM ubuntu:22.04 AS miniforge

# Install only necessary packages for Miniforge installation
RUN apt-get update && apt-get install -y \
    wget \
    bzip2 \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install Miniforge
ARG TARGETARCH
ENV CONDA_DIR=/opt/conda
ENV PATH=$CONDA_DIR/bin:$PATH

RUN ARCH=$(echo ${TARGETARCH} | sed 's/amd64/x86_64/;s/arm64/aarch64/') \
    && wget -q https://github.com/conda-forge/miniforge/releases/latest/download/Miniforge3-Linux-${ARCH}.sh -O miniforge.sh \
    && bash miniforge.sh -b -p $CONDA_DIR \
    && rm miniforge.sh \
    && conda clean -afy

# Build stage for the application
FROM miniforge AS builder

# Install git, build tools, and CMake
RUN apt-get update && apt-get install -y \
    git \
    build-essential \
    cmake \
    && rm -rf /var/lib/apt/lists/*

# Install git
#RUN apt-get update && apt-get install -y git

# Clone the repository using HTTPS
#RUN git clone --recurse-submodules https://github.com/chrisjcc/ReplicantDriveSim.git

# Clone the repository using HTTPS
#RUN git clone --recurse-submodules https://github.com/chrisjcc/ReplicantDriveSim.git /app/repo
RUN git clone --depth 1 --recurse-submodules --shallow-submodules https://github.com/chrisjcc/ReplicantDriveSim.git /app/repo

# Initialize Conda, update, and create environment
#RUN conda init bash \
#    && . $CONDA_DIR/etc/profile.d/conda.sh \
#    && conda update -n base -c defaults conda -y \
#    && conda env create -f /app/repo/External/environment.yml \
#    && conda clean -afy

# Initialize Conda and update base environment
RUN conda init bash \
    && . $CONDA_DIR/etc/profile.d/conda.sh \
    && conda update -n base -c defaults conda -y \
    && conda clean -afy

# Debug: List contents of /app/repo/External
RUN ls -la /app/repo/External

# Create Conda environment
RUN . $CONDA_DIR/etc/profile.d/conda.sh \
    && conda env create -f /app/repo/External/environment.yml \
    && conda clean -afy

# Activate environment and install additional packages
RUN . $CONDA_DIR/etc/profile.d/conda.sh \
    && conda activate drive \
    && pip install --no-cache-dir --upgrade pip build

# Build and install the wheel
RUN . $CONDA_DIR/etc/profile.d/conda.sh \
    && conda activate drive \
    && cd /app/repo/External \
    && python -m build -v \
    && pip install --no-cache-dir --force-reinstall dist/simulation-*.whl \
    && rm -rf dist build *.egg-info

# Final image setup
FROM ubuntu:22.04

# Install only necessary runtime dependencies
RUN apt-get update && apt-get install -y \
    python3-minimal \
    libglib2.0-0 \
    libxext6 \
    libsm6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/*

# Copy Conda and the built application from the builder stage
COPY --from=builder /opt/conda /opt/conda
COPY --from=builder /app/repo/External /app/External

# Set environment variables
ENV CONDA_DIR=/opt/conda
ENV PATH=$CONDA_DIR/bin:$PATH
ENV CONDA_DEFAULT_ENV=drive

# Set working directory
WORKDIR /app/External

# Default command
CMD ["bash"]

# Install a nano text editor
#RUN apt-get update && apt-get install -y nano

# Install necessary packages
#RUN apt-get update && apt-get install -y \
#    nano \
#    python3 \
#    python3-pip \
#    python3-venv \
#    wget \
#    bzip2 \
#    ca-certificates \
#    libglib2.0-0 \
#    libxext6 \
#    libsm6 \
#    libxrender1 \
#    mercurial \
#    git

# Copy the repository from the intermediate image
#COPY --from=intermediate /ReplicantDriveSim /app/repo

# Install Miniforge
#ARG TARGETARCH
#ENV CONDA_DIR=/opt/conda

#RUN echo "Building for architecture: ${TARGETARCH}"
#RUN ARCH=$(echo ${TARGETARCH} | sed 's/amd64/x86_64/;s/arm64/aarch64/') && \
#    wget https://github.com/conda-forge/miniforge/releases/latest/download/Miniforge3-Linux-${ARCH}.sh -O ~/miniforge.sh && \
#    /bin/bash ~/miniforge.sh -b -p $CONDA_DIR && \
#    rm ~/miniforge.sh && \
#    $CONDA_DIR/bin/conda clean -afy

# Initialize Conda
#RUN /bin/bash -c "source $CONDA_DIR/bin/activate && \
#    conda init bash"

# Update Conda and create an environment
#RUN /bin/bash -c "source $CONDA_DIR/bin/activate && \
#    conda update -n base -c defaults conda -y && \
#    conda env create -f /app/repo/External/environment.yml"

# Install build tools and build the wheel
#RUN /bin/bash -c "source $CONDA_DIR/bin/activate drive && \
#    pip install --upgrade pip && \
#    pip install build && \
#    cd /app/repo/External && \
#    python -m build -v"

# Install the traffic simulation wheel
#RUN /bin/bash -c "source $CONDA_DIR/bin/activate drive && \
#    pip install --force-reinstall /app/repo/External/dist/simulation-*.whl"

# Set working directory
#WORKDIR /app/repo/External

# Default command
#CMD ["bash"]
