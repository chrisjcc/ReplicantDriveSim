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
    }
}
