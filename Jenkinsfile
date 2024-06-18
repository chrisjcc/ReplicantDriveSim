pipeline {
    agent any
    
    environment {
        // Define the URL for Miniconda installation script
        MINICONDA_URL = 'https://repo.anaconda.com/miniconda/Miniconda3-latest-MacOSX-arm64.sh'
        // Define the installation directory for Miniconda
        MINICONDA_INSTALL_DIR = "${env.WORKSPACE}/miniconda"

        // Define the installation file and directory for md5 Binary
        MD5_BINARY_URL = 'https://github.com/dhobsd/md5/archive/v1.3.tar.gz'
        MD5_BINARY_DIR = "${env.WORKSPACE}/md5"
    }
    
    stages {
        stage('Install md5') {
            steps {
                script {
                    // Download md5 binary
                    sh "curl -fsSL ${MD5_BINARY_URL} | tar -xz -C ${MD5_BINARY_DIR} --strip-components=1"
                    
                    // Build and install md5
                    dir("${MD5_BINARY_DIR}") {
                        sh "make"
                        sh "make install PREFIX=${MINICONDA_INSTALL_DIR}/bin"
                    }
                    
                    // Verify md5 installation
                    sh "${MINICONDA_INSTALL_DIR}/bin/md5 --version"
                }
            }
        }
        stage('Checkout') {
            steps {
                // Checkout your repository if needed
                // Example: git 'https://github.com/your/repo.git'
                // dir('your-subdirectory') {
                //     git 'https://github.com/your/repo.git'
                // }
                sh "echo 'Checkout ...'"
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
