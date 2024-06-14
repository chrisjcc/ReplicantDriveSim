pipeline {
  agent any
  stages {
    stage('Checkout') {
      steps {
        git(credentialsId: '05078083-bc9f-4fc3-9aeb-ba55c3ff4be5', url: 'git@github.com:chrisjcc/ReplicantDriveSim.git', branch: 'main')
      }
    }

    stage('Install Conda Environment') {
      steps {
        sh 'conda env create -f environment.yml'
      }
    }

    stage('Run Python Script') {
      steps {
        sh 'source activate drive && python simulacrum.py'
      }
    }

  }
  environment {
    CONDA_HOME = '/opt/miniconda'
    PATH = "${CONDA_HOME}/bin:${env.PATH}"
  }
  post {
    always {
      sh 'conda env remove -n drive'
    }

  }
}