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
                git credentialsId: '05078083-bc9f-4fc3-9aeb-ba55c3ff4be5', url: 'git@github.com:chrisjcc/agents.git', branch: 'main'
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
                sh 'source activate drive && python simulacrum.py'
            }
        }
    }

    post {
        always {
            // Clean up conda environment
            sh 'conda env remove -n drive'
        }
    }
}
