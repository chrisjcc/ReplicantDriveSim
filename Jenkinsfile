pipeline {
    agent any
    
    stages {
        stage('Run Miniconda Container') {
            steps {
                script {
                    // Run the Miniconda container and execute the commands inside it
                    sh '''
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
