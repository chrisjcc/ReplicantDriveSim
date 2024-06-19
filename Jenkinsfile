pipeline {
    agent any

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
                    // Pull the Miniconda Docker image
                    sh 'docker pull continuumio/miniconda3'

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
