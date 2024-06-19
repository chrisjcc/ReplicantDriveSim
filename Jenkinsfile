pipeline {
    agent {
        docker {
            image 'continuumio/miniconda3'
            args '-v /var/run/docker.sock:/var/run/docker.sock' // Bind Docker socket for Docker-in-Docker setup
        }
    }
    stages {
        stage('Print Docker Version') {
            steps {
                script {
                    def dockerVersion = sh(script: 'docker --version', returnStdout: true).trim()
                    echo "Docker version: ${dockerVersion}"
                }
            }
        }
        stage('Install Miniconda and Print Conda Version') {
            steps {
                script {
                    // Run the Miniconda container and execute the commands inside it
                    sh '''
                    docker run --rm continuumio/miniconda3 bash -c "
                    conda init bash &&
                    source ~/.bashrc &&
                    conda --version
                    "
                    '''
                }
            }
        }
    }
}
