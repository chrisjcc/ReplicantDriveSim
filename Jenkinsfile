pipeline {
  agent any
  stages {
    stage('Checkout') {
      steps {
        git(credentialsId: 'github-token', url: 'https://github.com/chrisjcc/ReplicantDriveSim.git', branch: 'main')
      }
    }

    stage('Install Conda Environment') {
      steps {
        sh 'conda env create -f environment.yml'
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
          sh 'conda env remove -n drive'
        } catch (Exception e) {
          echo "Failed to remove conda environment: ${e.getMessage()}"
        }
      }

    }

  }
}