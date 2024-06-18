pipeline {
    agent any
    
    environment {
        // Define the URL for Miniconda installation script
        MINICONDA_URL = 'https://repo.anaconda.com/miniconda/Miniconda3-latest-MacOSX-arm64.sh'
        // Define the installation directory for Miniconda
        MINICONDA_INSTALL_DIR = "${env.WORKSPACE}/miniconda"

        // Define the installation file and directory for md5 Binary
        MD5_BINARY_URL = 'https://github.com/jessek/hashdeep/archive/refs/tags/release-4.4.zip'
        MD5_BINARY_PATH = "${env.WORKSPACE}/hashdeep-release-4.4"
    }
    
    stages {
        stage('Cleanup Workspace') {
            steps {
                script {
                    // Clean up Miniconda installation directory if it exists
                    sh "rm -rf ${MINICONDA_INSTALL_DIR}"
                }
            }
        }
        stage('Install md5') {
            steps {
                script {
                    // Download md5 binary
                    sh "curl -fsSL ${MD5_BINARY_URL} -o md5deep.zip"
                    
                    // Unzip md5 binary
                    sh "unzip -o md5deep.zip -d ${env.WORKSPACE}"
                    
                    // Make md5 binary executable
                    sh "chmod +x ${MD5_BINARY_PATH}"
                    
                    // Clean up downloaded zip file
                    sh "rm md5deep.zip"
                }
            }
        }
        stage('Install Miniconda') {
            steps {
                script {
                    // Download Miniconda installer
                    sh "curl -o miniconda.sh ${MINICONDA_URL}"
                    
                    // Install Miniconda silently
                    sh "bash miniconda.sh -b -p ${MINICONDA_INSTALL_DIR}"
                    
                    // Activate Miniconda for the current shell session
                    sh "${MINICONDA_INSTALL_DIR}/bin/conda --version" // Verify conda command availability
                    
                    // Clean up downloaded installer
                    sh "rm miniconda.sh"

                    // Update PATH to include Miniconda binaries
                    env.PATH = "${MINICONDA_INSTALL_DIR}/bin:${env.PATH}"
                }
            }
        }
        stage('Build and Test') {
            steps {
                script {
                    sh "echo 'Build and Test ...'"
                    sh "python --version" // Example command
                    sh "pytest" // Example command
                }
            }
        }
    }
    
    post {
        always {
            script {
               // Clean up Miniconda environment (optional)
                sh "conda deactivate" // Deactivate Miniconda environment
                sh "rm -rf ${MINICONDA_INSTALL_DIR}" // Clean up Miniconda installation
            }
        }
    }
}
