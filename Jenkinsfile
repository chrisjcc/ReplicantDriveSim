pipeline {
    agent {
        docker {
            image 'jenkinsci/jnlp-slave:alpine' // Using a base Jenkins agent image
            args '-v /var/run/docker.sock:/var/run/docker.sock' // Bind Docker socket for Docker-in-Docker setup
        }
    }

    stages {
        stage('Setup') {
            steps {
                script {
                    // Pull the Miniconda Docker image
                    sh 'docker pull continuumio/miniconda3'
                }
            }
        }

        stage('Run Miniconda Container') {
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

        stage('Print Docker Version') {
            steps {
                script {
                    def dockerVersion = sh(script: 'docker --version', returnStdout: true).trim()
                    echo "Docker version: ${dockerVersion}"
                }
            }
        }
    }
}
