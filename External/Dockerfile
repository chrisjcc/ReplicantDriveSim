# syntax=docker/dockerfile:1.2

# This is our first build stage, it will not persist in the final image
FROM ubuntu as intermediate

# Install git
RUN apt-get update && apt-get install -y git

# Clone the repository using HTTPS
RUN git clone https://github.com/chrisjcc/ReplicantDriveSim.git

FROM ubuntu

# Install nano text editor
RUN apt-get update && apt-get install -y nano

# Copy the repository from the previous image
COPY --from=intermediate /ReplicantDriveSim /app/repo

# Install Python and other necessary packages
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
    mercurial

# Install Miniforge (ARM64 version)
ENV CONDA_DIR=/opt/conda
RUN wget --quiet https://github.com/conda-forge/miniforge/releases/latest/download/Miniforge3-Linux-aarch64.sh -O ~/miniforge.sh && \
    /bin/bash ~/miniforge.sh -b -p $CONDA_DIR && \
    rm ~/miniforge.sh && \
    $CONDA_DIR/bin/conda clean -afy

# Initialize Conda and set up shell initialization
RUN $CONDA_DIR/bin/conda init && \
    echo "conda activate base" >> ~/.bashrc

# Set the PATH Miniforge environment variables
ENV PATH=$CONDA_DIR/bin:$PATH

# Create and activate Conda environment
RUN /bin/bash -c "source $CONDA_DIR/bin/activate && \
    conda env create -f /app/repo/environment.yml && \
    echo 'conda activate drive' >> ~/.bashrc"

# Set the PATH to include conda environment
ENV PATH=$CONDA_DIR/envs/drive/bin:$PATH

# Install build tools and build the wheel
RUN /bin/bash -c "source $CONDA_DIR/bin/activate && \
    conda activate drive && \
    pip install --upgrade pip && \
    pip install build && \
    cd /app/repo && \
    python -m build -v"

# Install the traffic simulation wheel
RUN /bin/bash -c "source $CONDA_DIR/bin/activate && \
    conda activate drive && \
    pip install --force-reinstall /app/repo/dist/simulation-*.whl"

# Set up the working directory
WORKDIR /app/repo

# To ensure the /app/repo/data directory exists
RUN mkdir -p data

# Set the default command to bash
CMD ["bash"]
