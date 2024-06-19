pipeline {
    agent {
        docker { image 'continuumio/miniconda3:latest' }
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

        stage('Print Node Version') {
            steps {
                sh 'node --version'
            }
        }
    }
}
