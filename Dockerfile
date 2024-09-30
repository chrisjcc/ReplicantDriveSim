# Use Unity's official image as a base
FROM unityci/editor:ubuntu-2022.3.3f1-linux-il2cpp-2.0.0

# Install necessary dependencies
RUN apt-get update && apt-get install -y \
    wget \
    unzip \
    coreutils \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /unity-project

# Copy your Unity project files
COPY . .

# Create a build script
RUN echo '#!/bin/bash\n\
unity-editor \
  -quit \
  -batchmode \
   -nographics \
  -projectPath "/unity-project" \
  -executeMethod UnityDriveSimulation.BuildTools.BuildScript.PerformMacOSBuild \
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
