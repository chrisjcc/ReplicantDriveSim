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

        // Define the URL and path for md5 binary
        MD5_BINARY_URL = 'https://github.com/jessek/hashdeep/releases/download/v4.4/md5deep-4.4.zip'
        MD5_BINARY_PATH = "${env.WORKSPACE}/md5deep-4.4/md5deep64.exe"

        // Define the URL and path for m4 binary
        M4_URL = 'http://ftp.gnu.org/gnu/m4/m4-latest.tar.gz'
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
        stage('Install md5 and Build Tools') {
            steps {
                script {
                    // Download and unzip md5 binary (md5deep)
                    sh "curl -fsSL ${MD5_BINARY_URL} -o md5deep.zip"
                    sh "unzip -o md5deep.zip -d ${env.WORKSPACE}"
                    sh "chmod +x ${MD5_BINARY_PATH}"
                    sh "rm md5deep.zip"

                    // Install necessary build tools (m4, autoconf)
                    // Install m4 from source
                    sh """
                        if ! command -v m4 &> /dev/null; then
                            echo "m4 not found, installing from source..."
                            curl -fsSL ${M4_URL} -o m4-latest.tar.gz
                            tar -xzvf m4-latest.tar.gz
                            cd m4-*
                            ./configure --prefix=${INSTALL_PREFIX}
                            make
                            make install
                            cd ..
                            rm -rf m4-* m4-latest.tar.gz
                        fi
                    """
                    
                    // Add INSTALL_PREFIX/bin to PATH to use installed m4
                    env.PATH = "${INSTALL_PREFIX}/bin:${env.PATH}"

                    // Verify m4 installation
                    sh """
                        if command -v m4 &> /dev/null; then
                            echo "m4 installed successfully"
                        else
                            echo "Failed to install m4"
                            exit 1
                        fi
                    """
                    
                    // Install necessary build tools (GNU Autotools)
                    // Manually download and install autoconf, automake, libtool if needed
                    // Download and install autoconf
                    sh "curl -fsSL http://ftp.gnu.org/gnu/autoconf/autoconf-latest.tar.gz -o autoconf.tar.gz"
                    sh "tar -xzvf autoconf.tar.gz"
                    // Using $$ to escape $ for shell command substitution
                    def autoconfDir = sh(script: "basename \$(ls -d autoconf-*)", returnStdout: true).trim()
                    dir(autoconfDir) {
                        sh "./configure --prefix=${INSTALL_PREFIX}"
                        sh "make"
                        sh "make install"
                    }
                    sh "rm -rf autoconf-* autoconf.tar.gz"
                    
                    // Repeat similar steps for automake and libtool if necessary
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
                //sh "conda deactivate" // Deactivate Miniconda environment
                sh "rm -rf ${MINICONDA_INSTALL_DIR}" // Clean up Miniconda installation
                sh "rm -rf ${HASHDEEP_DIR}" // Clean up hashdeep repository
                sh "rm -rf ${INSTALL_PREFIX}" // Clean up hashdeep installation
            }
        }
    }
}
