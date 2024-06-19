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
        stage('Install Build Tools') {
            steps {
                script {
                    // Install clang (default compiler for macOS)

                    sh "xcode-select --install || true"  // Attempt to install Xcode Command Line Tools
                    
                    // Verify clang installation and available
                    sh """
                        if ! command -v clang &> /dev/null; then
                            echo "Installing Xcode Command Line Tools"
                            xcode-select --switch /Library/Developer/CommandLineTools  // Set the correct path if needed
                        else
                            echo "Xcode Command Line Tools already installed"
                        fi
                    """
                    
                    // Function to extract and install a package
                    def installPackage = { packageName, url ->
                        sh "curl -fsSL ${url} -o ${packageName}.tar.gz"
                        sh "tar -xzvf ${packageName}.tar.gz"
                        def packageDir = sh(script: "ls -d ${packageName}-*", returnStdout: true).trim().split()[0]
                        dir(packageDir) {
                            sh "./configure --prefix=${INSTALL_PREFIX}"
                            sh "make"
                            sh "make install"
                        }
                        sh "rm -rf ${packageName}-* ${packageName}.tar.gz"
                    }

                    // Install m4
                    installPackage('m4', 'http://ftp.gnu.org/gnu/m4/m4-latest.tar.gz')
                    
                    // Install autoconf
                    installPackage('autoconf', 'http://ftp.gnu.org/gnu/autoconf/autoconf-latest.tar.gz')
                    
                    // Install automake
                    installPackage('automake', 'http://ftp.gnu.org/gnu/automake/automake-latest.tar.gz')
                    
                    // Install libtool
                    installPackage('libtool', 'http://ftp.gnu.org/gnu/libtool/libtool-latest.tar.gz')
                    
                    // Add INSTALL_PREFIX/bin to PATH to use installed tools
                    env.PATH = "${INSTALL_PREFIX}/bin:${env.PATH}"

                    // Verify installations
                    sh """
                        if command -v autoheader &> /dev/null && command -v aclocal &> /dev/null && command -v autoconf &> /dev/null && command -v automake &> /dev/null && command -v m4 &> /dev/null && command -v clang &> /dev/null; then
                            echo "Build tools installed successfully"
                        else
                            echo "Failed to install build tools"
                            exit 1
                        fi
                    """
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
                        // Run bootstrap script
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
