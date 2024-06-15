pipeline {
    agent any

    environment {
        CONDA_HOME = '/opt/miniconda'  // Update this if you installed conda in a different path
        PATH = "${CONDA_HOME}/bin:${env.PATH}"
    }

    stages {
        stage('Checkout') {
            steps {
                // Checkout code from Git repository using credentials
                git credentialsId: 'github-token', url: 'git@github.com:chrisjcc/ReplicantDriveSim.git', branch: 'main'
            }
        }

        stage('Install Conda Environment') {
            steps {
                // Install conda environment from environment.yml
                sh 'conda env create -f environment.yml'
            }
        }

        stage('Run Python Script') {
            steps {
                // Activate conda environment and run the python script
                sh 'conda run -n drive python simulacrum.py'
            }
        }
    }

    post {
        always {
            // Clean up conda environment
            script {
                try {
                    sh 'conda env remove -n drive'
                } catch (Exception e) {
                    echo "Failed to remove conda environment: ${e.getMessage()}"
                }
            }
        }
    }
}
