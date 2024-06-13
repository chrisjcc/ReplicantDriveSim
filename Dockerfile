# syntax=docker/dockerfile:1.2

# Use BuildKit's support for SSH forwarding
# this is our first build stage, it will not persist in the final image
FROM ubuntu as intermediate

# install git
RUN apt-get update && apt-get install -y git openssh-client

# use the forwarded SSH key for git clone
# make sure your domain is accepted
RUN mkdir -p /root/.ssh && \
    ssh-keyscan github.com >> /root/.ssh/known_hosts

# clone the repository using SSH agent forwarding
# use the --mount=type=ssh to forward the SSH key
RUN --mount=type=ssh git clone -b feat/traffic_sim_cpp git@github.com:chrisjcc/ReplicantDriveSim.git

FROM ubuntu

# Install nano text editor
RUN apt-get update && apt-get install -y \
    nano

# Install Python
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
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
RUN apt-get update && \
    apt-get install -y wget git && \
    wget --quiet https://github.com/conda-forge/miniforge/releases/latest/download/Miniforge3-Linux-aarch64.sh -O ~/miniforge.sh && \
    /bin/bash ~/miniforge.sh -b -p $CONDA_DIR && \
    rm ~/miniforge.sh && \
    $CONDA_DIR/bin/conda clean -afy

# Initialize Conda and set up shell initialization
RUN /opt/conda/bin/conda init && \
    echo "conda activate base" >> ~/.bashrc

# Set the PATH Miniforge environment variables
ENV PATH=$CONDA_DIR/bin:$PATH

# copy the repository from the previous image
COPY --from=intermediate /ReplicantDriveSim /app/repo

# Set the default command to sh
CMD ["bash"]
