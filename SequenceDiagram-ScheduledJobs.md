## Diagram Source

This is the "source code" for the Scheduled Jobs sequence diagram 

title Deploy a Blob to IoT Edge (Scheduled Jobs)

    participant Storage Account
    participant IotEdgeDeployBlobs.Sdk
    participant Iot Hub
    participant DeployBlobsModule
    participant File System (bind)
    participant Consumer Docker

    IotEdgeDeployBlobs.Sdk->Storage Account:Upload Blob(s)

    IotEdgeDeployBlobs.Sdk<--Storage Account:SAS Key(s)

	note over IotEdgeDeployBlobs.Sdk, Iot Hub: The scheduled job can determine which\nIot Edge Devices target by filtering
    IotEdgeDeployBlobs.Sdk->Iot Hub:Deploy Blob(s) (Scheduled Job)
    Iot Hub->IotEdgeDeployBlobs.Sdk: JobId

    Iot Hub->DeployBlobsModule:Direct Method being called by the Scheduled Job\nThe payload includes the SASKey to download \nConfiguration File from Storage Account

    DeployBlobsModule->Storage Account:Download Blob(s)

    DeployBlobsModule->File System (bind):Save Blob(s)

    DeployBlobsModule-->Iot Hub:DM Confirm blob saved on file system.

    note over Consumer Docker,File System (bind):You need to notify the target module to reload the Blob(s)\nOr ensure Module watches for file updates

    IotEdgeDeployBlobs.Sdk-->Consumer Docker:Notify to Reload the Blob(s)

    Consumer Docker->File System (bind):Load the Blob(s)

    IotEdgeDeployBlobs.Sdk->Iot Hub:GetJobStatus(JobId)
    Iot Hub->IotEdgeDeployBlobs.Sdk:Job Finished

	IotEdgeDeployBlobs.Sdk ->Iot Hub: GetJobResponses(JobId)
    Iot Hub->IotEdgeDeployBlobs.Sdk: Job Responses