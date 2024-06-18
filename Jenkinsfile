pipeline {
    agent any
    
    environment {
        // Define the URL for Miniconda installation script
        MINICONDA_URL = 'https://repo.anaconda.com/miniconda/Miniconda3-latest-MacOSX-arm64.sh'
        // Define the installation directory for Miniconda
        MINICONDA_INSTALL_DIR = "${env.WORKSPACE}/miniconda"

        // Define the URL for cloning hashdeep repository
        HASHDEEP_REPO_URL = 'https://github.com/jessek/hashdeep.git'
        HASHDEEP_DIR = "${env.WORKSPACE}/hashdeep"
        INSTALL_PREFIX = "${env.WORKSPACE}/hashdeep_install" // Optional: Customize installation directory
    }
    
    stages {
        stage('Cleanup Workspace') {
            steps {
                script {
                    // Clean up Miniconda installation directory if it exists
                    sh "rm -rf ${MINICONDA_INSTALL_DIR}"
                    // Clean up hashdeep directory if it exists
                    sh "rm -rf ${HASHDEEP_DIR}"
                    // Clean up hashdeep installation directory if it exists
                    sh "rm -rf ${INSTALL_PREFIX}"
                }
            }
        }
        stage('Clone and Install Hashdeep') {
            steps {
                script {
                    // Clone hashdeep repository
                    sh "git clone ${HASHDEEP_REPO_URL} ${HASHDEEP_DIR}"
                    
                    // Navigate to hashdeep directory
                    dir("${HASHDEEP_DIR}") {
                        // Run bootstrap script (if needed)
                        sh "./bootstrap.sh"
                        
                        // Configure hashdeep installation
                        sh "./configure --prefix=${INSTALL_PREFIX}"
                        
                        // Build and install hashdeep
                        sh "make"
                        sh "make install"
                    }
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
                sh "rm -rf ${HASHDEEP_DIR}" // Clean up hashdeep repository
                sh "rm -rf ${INSTALL_PREFIX}" // Clean up hashdeep installation
            }
        }
    }
}
