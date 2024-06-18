pipeline {
    agent any

    environment {
        CONDA_HOME = '/opt/miniconda'  // Change this to the desired installation directory
        PATH = "${CONDA_HOME}/bin:${env.PATH}"
    }

    stages {
        stage('Check and Install Conda') {
            steps {
                script {
                    // Check if conda is installed
                    def condaInstalled = sh(script: 'which conda', returnStatus: true) == 0

                    if (!condaInstalled) {
                        echo "Conda not found, installing Miniconda..."
                        // Download and install Miniconda using curl
                        sh 'curl -o miniconda.sh https://repo.anaconda.com/miniconda/Miniconda3-latest-Linux-x86_64.sh'
                        sh 'bash miniconda.sh -b -p ${CONDA_HOME}'
                        sh 'rm miniconda.sh'
                        // Initialize Conda
                        sh 'bash -c "source ${CONDA_HOME}/etc/profile.d/conda.sh && conda init"'
                        // Ensure changes take effect
                        sh 'bash -c "source ~/.bashrc"'
                    } else {
                        echo "Conda is already installed."
                    }
                }
            }
        }

        stage('Checkout') {
            steps {
                git(credentialsId: 'github-token', url: 'https://github.com/chrisjcc/ReplicantDriveSim.git', branch: 'main')
            }
        }

        stage('Install Conda Environment') {
            steps {
                script {
                    sh 'bash -c "source ${CONDA_HOME}/etc/profile.d/conda.sh && conda env create -f environment.yml"'
                }
            }
        }

        stage('Run Python Script') {
            steps {
                script {
                    sh 'bash -c "source ${CONDA_HOME}/etc/profile.d/conda.sh && conda run -n drive python simulacrum.py"'
                }
            }
        }
    }

    post {
        always {
            script {
                try {
                    sh 'bash -c "source ${CONDA_HOME}/etc/profile.d/conda.sh && conda env remove -n drive"'
                } catch (Exception e) {
                    echo "Failed to remove conda environment: ${e.getMessage()}"
                }
            }
        }
    }
}
