pipeline {
    agent any

    environment {
        CONDA_HOME = "${env.WORKSPACE}/miniconda"
        PATH = "${CONDA_HOME}/bin:${env.PATH}"
    }

    stages {
        stage('Download and Install Miniconda') {
            steps {
                script {
                    echo "Checking if Conda is already installed..."
                    def condaInstalled = sh(script: 'which conda', returnStatus: true) == 0

                    if (!condaInstalled) {
                        echo "Conda not found, installing Miniconda..."

                        // Remove the existing Miniconda directory if it exists
                        sh 'rm -rf ${CONDA_HOME}'

                        // Download Miniconda installer for macOS ARM architecture
                        def minicondaUrl = 'https://repo.anaconda.com/miniconda/Miniconda3-latest-MacOSX-arm64.sh'
                        sh "curl -o miniconda.sh -L ${minicondaUrl}"

                        // Make the installer executable
                        sh 'chmod +x miniconda.sh'

                        // Install Miniconda, skipping the md5 check
                        def installCommand = 'bash miniconda.sh -b -p ${CONDA_HOME} -f'
                        def installStatus = sh(script: installCommand, returnStatus: true)

                        if (installStatus != 0) {
                            error("Miniconda installation failed.")
                        }

                        // Clean up
                        sh 'rm miniconda.sh'

                        // Initialize Conda
                        sh 'bash -c "source ${CONDA_HOME}/etc/profile.d/conda.sh && conda init"'
                        sh 'bash -c "source ~/.bash_profile"'
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
