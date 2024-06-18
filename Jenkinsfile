pipeline {
    agent any

    environment {
        CONDA_HOME = '/opt/miniconda'
        PATH = "${CONDA_HOME}/bin:${env.PATH}"
    }

    stages {
        stage('Checkout') {
            steps {
                git(credentialsId: 'github-token', url: 'https://github.com/chrisjcc/ReplicantDriveSim.git', branch: 'main')
            }
        }

        stage('Install Conda Environment') {
            steps {
                script {
                    sh 'echo $PATH'  // Print PATH to debug
                    sh 'conda --version'  // Check if conda command is found
                    sh 'conda env create -f environment.yml'
                }
            }
        }

        stage('Run Python Script') {
            steps {
                sh 'conda run -n drive python simulacrum.py'
            }
        }
    }

    post {
        always {
            script {
                try {
                    sh '${CONDA_HOME}/bin/conda env remove -n drive'  // Use full path to conda
                } catch (Exception e) {
                    echo "Failed to remove conda environment: ${e.getMessage()}"
                }
            }
        }
    }
}
