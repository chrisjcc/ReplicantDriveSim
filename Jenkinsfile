pipeline {
    agent {
        docker { image 'continuumio/miniconda3' }
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
                    sh 'conda init bash'
                    sh 'source ~/.bashrc'
                    def condaVersion = sh(script: 'conda --version', returnStdout: true).trim()
                    echo "Conda version: ${condaVersion}"
                }
            }
        }
    }
}
