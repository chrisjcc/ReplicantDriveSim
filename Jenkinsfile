pipeline {
    agent {
        docker {
            // Mount Docker socket to enable Docker commands within the container
            args '-v /var/run/docker.sock:/var/run/docker.sock'
        }
    }

    environment {
        CONDA_HOME = "${env.WORKSPACE}/miniconda"
        PATH = "${CONDA_HOME}/bin:${env.PATH}"
    }

    stages {
        stage('Test Docker') {
            steps {
                script {
                    // Example Docker command to verify Docker installation
                    sh 'docker --version'
                    // Add more Docker commands as needed
                    sh 'docker pull ubuntu:latest'
                }
            }
        }
        
        stage('Download and Install Miniconda') {
            steps {
                script {
                    echo "Checking if Conda is already installed..."
                    def condaInstalled = sh(script: 'which conda', returnStatus: true) == 0

                    if (!condaInstalled) {
                        echo "Conda not found, installing Miniconda..."

                        // Remove the existing Miniconda directory if it exists
                        sh "rm -rf ${CONDA_HOME}"

                        // Download Miniconda installer for Linux
                        def minicondaUrl = 'https://repo.anaconda.com/miniconda/Miniconda3-latest-Linux-x86_64.sh'
                        sh "curl -o miniconda.sh -L ${minicondaUrl}"

                        // Make the installer executable
                        sh 'chmod +x miniconda.sh'

                        // Verify the SHA-256 checksum of the downloaded installer
                        def expectedSha256 = 'insert-correct-sha256-here'  // Replace with the correct SHA-256 checksum
                        def actualSha256 = sh(script: 'sha256sum miniconda.sh | awk \'{print $1}\'', returnStdout: true).trim()
                        
                        if (actualSha256 != expectedSha256) {
                            error("SHA-256 checksum mismatch: expected ${expectedSha256}, got ${actualSha256}")
                        }

                        // Install Miniconda
                        def installStatus = sh(script: 'bash miniconda.sh -b -p ${CONDA_HOME}', returnStatus: true)

                        if (installStatus != 0) {
                            error("Miniconda installation failed.")
                        }

                        // Clean up
                        sh 'rm miniconda.sh'

                        // Initialize Conda
                        sh 'bash -c "source ${CONDA_HOME}/etc/profile.d/conda.sh && conda init bash"'
                        sh 'bash -c "source ~/.bashrc"'
                    } else {
                        echo "Conda is already installed."
                    }
                }
            }
        }

        stage('Activate Base Environment') {
            steps {
                script {
                    try {
                        sh 'bash -c "source ${CONDA_HOME}/etc/profile.d/conda.sh && conda activate base"'
                    } catch (Exception e) {
                        echo "Failed to activate conda environment: ${e.getMessage()}"
                    }
                }
            }
        }

        stage('Verify Conda Installation') {
            steps {
                script {
                    sh 'conda --version'
                }
            }
        }
    }
}
