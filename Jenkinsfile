node {
  stage('CheckOut'){
    checkout([$class: 'GitSCM', branches: [[name: '*/master']], doGenerateSubmoduleConfigurations: false, extensions: [[$class: 'SubmoduleOption', disableSubmodules: false, parentCredentials: false, recursiveSubmodules: true, reference: '', trackingSubmodules: false]], submoduleCfg: [], userRemoteConfigs: [[url: 'https://github.com/southernwind/ElectricPowerMonitorService']]])
  }

  stage('Configuration'){
    configFileProvider([configFile(fileId: 'b202977b-aca6-4c36-9d5f-d4202e71311f', targetLocation: 'ElectricPowerMonitorService/appsettings.json')]) {}
  }

  stage('Build ElectricPowerMonitorService'){
    dotnetBuild configuration: 'Release', project: 'ElectricPowerMonitorService.sln', runtime: 'linux-x64', sdk: '.NET3.1', unstableIfWarnings: true
  }

  withCredentials( \
      bindings: [sshUserPrivateKey( \
        credentialsId: 'ac005f9d-9b4b-496f-873c-1c610df01c03', \
        keyFileVariable: 'SSH_KEY', \
        usernameVariable: 'SSH_USER')]) {
    stage('Deploy ElectricPowerMonitorService'){
      sh 'scp -pr -i ${SSH_KEY} ./ElectricPowerMonitorService/bin/Release/netcoreapp3.1/linux-x64/* ${SSH_USER}@home-server.localnet:/opt/electric-power-monitor-service'
    }

    stage('Restart ElectricPowerMonitorService'){
      sh 'ssh home-server.localnet -t -l ${SSH_USER} -i ${SSH_KEY} sudo service electric_power_monitor restart'
    }
  }

  stage('Notify Slack'){
    sh 'curl -X POST --data-urlencode "payload={\\"channel\\": \\"#jenkins-deploy\\", \\"username\\": \\"jenkins\\", \\"text\\": \\"電力消費量測定サービスのデプロイが完了しました。\\nBuild:${BUILD_URL}\\"}" ${WEBHOOK_URL}'
  }
}