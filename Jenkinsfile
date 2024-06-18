pipeline {
    agent any
    
    environment {
        // Define the URL for Miniconda installation script
        MINICONDA_URL = 'https://repo.anaconda.com/miniconda/Miniconda3-latest-MacOSX-arm64.sh'
        // Define the installation directory for Miniconda
        MINICONDA_INSTALL_DIR = "${env.WORKSPACE}/miniconda"

        // Define the installation file and directory for md5 Binary
        MD5_BINARY_URL = 'https://github.com/Homebrew/homebrew-core/raw/HEAD/Formula/m/md5sha1sum.rb'
        MD5_BINARY_PATH = "${env.WORKSPACE}/md5sum"
    }
    
    stages {
        stage('Cleanup Workspace') {
            steps {
                // Clean up Miniconda installation directory if it exists
                script {
                    sh "rm -rf ${MINICONDA_INSTALL_DIR}"
                }
            }
        }
        
        stage('Install md5') {
            steps {
                script {
                    // Download md5 binary
                    sh "curl -fsSL ${MD5_BINARY_URL} -o ${MD5_BINARY_PATH}"
                    
                    // Make md5 binary executable
                    sh "chmod +x ${MD5_BINARY_PATH}"
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
                    
                    // Activate Miniconda for current shell session
                    sh "source ${MINICONDA_INSTALL_DIR}/bin/activate"
                    
                    // Add Miniconda binaries to PATH (optional)
                    sh "export PATH=${MINICONDA_INSTALL_DIR}/bin:$PATH"
                    
                    // Verify Miniconda installation
                    sh "conda --version"
                    
                    // Clean up downloaded installer
                    sh "rm miniconda.sh"
                }
            }
        }
        
        stage('Build and Test') {
            steps {
                // Example: Run your build and test commands here
                // sh 'python --version'
                // sh 'pytest'
                sh "echo 'Build and Test ...'"
            }
        }
    }
    
    post {
        always {
            // Clean up Miniconda environment (optional)
            sh "conda deactivate && rm -rf ${MINICONDA_INSTALL_DIR}"
        }
    }
}
