pipeline {
    agent any

    environment {
        CONDA_HOME = "${env.WORKSPACE}/miniconda"
        PATH = "${CONDA_HOME}/bin:${env.PATH}"
    }

    stages {
        stage('Check System Dependencies') {
            steps {
                script {
                    def checkCurl = sh(script: 'which curl', returnStatus: true)
                    if (checkCurl != 0) {
                        error("curl is not installed. Please install curl and try again.")
                    }
                }
            }
        }

        stage('Download and Install Miniconda') {
            steps {
                script {
                    def condaInstalled = sh(script: 'which conda', returnStatus: true) == 0

                    if (!condaInstalled) {
                        echo "Conda not found, installing Miniconda..."
                        sh 'rm -rf ${CONDA_HOME}'
                        // Download Miniconda installer
                        def minicondaUrl = 'https://repo.anaconda.com/miniconda/Miniconda3-latest-MacOSX-x86_64.sh'
                        sh "curl -o miniconda.sh -L ${minicondaUrl}"
                        // Install Miniconda
                        def installStatus = sh(script: 'bash miniconda.sh -b -p ${CONDA_HOME}', returnStatus: true)
                        if (installStatus != 0) {
                            echo "Installation failed. Retrying with a different installer..."
                            def altMinicondaUrl = 'https://repo.continuum.io/miniconda/Miniconda2-latest-MacOSX-x86_64.sh'
                            sh "curl -o miniconda.sh -L ${altMinicondaUrl}"
                            sh 'bash miniconda.sh -b -p ${CONDA_HOME}'
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
