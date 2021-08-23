# Sequence Diagrams

## The Tool

Sequence diagrams is frequently challanging to draw...  and in case of changes in your code even more messy to maintain!!

Recently i've discovered this free online editor: https://sequencediagram.org/

With that tool you can just create your diagram by declaring the interactions and it will render a nice visual reppresentation on the fly!

Just like this:

![image](https://user-images.githubusercontent.com/45007019/129010495-6a119bc8-2e96-4c00-a1af-27f00738cb46.png)


## My Diagram Source Code

This is the "source code" for the main sequence diagram 

title Deploy a Blob to IoT Edge 

    participant Storage Account
    participant IotEdgeDeployBlobs.Sdk
    participant Iot Hub
    participant DeployBlobsModule
    participant File System (bind)
    participant Consumer Docker

    IotEdgeDeployBlobs.Sdk->Storage Account:Upload Blob(s)

    IotEdgeDeployBlobs.Sdk<--Storage Account:SAS Key(s)

    IotEdgeDeployBlobs.Sdk->Iot Hub:Deploy Blob(s) (via DM)

    Iot Hub->DeployBlobsModule:Direct Method Call with the SASKey to download \nConfiguration File from Storage Account

    DeployBlobsModule->Storage Account:Download Blob(s)

    DeployBlobsModule->File System (bind):Save Blob(s)

    DeployBlobsModule-->Iot Hub:DM Confirm blob saved on file system.

    Iot Hub-->IotEdgeDeployBlobs.Sdk:DM Completed

    note over Consumer Docker,File System (bind):You need to notify the target module to reload the Blob(s)\nOr ensure Module watches for file updates

    IotEdgeDeployBlobs.Sdk-->Consumer Docker:Notify to Reload the Blob(s)

    Consumer Docker->File System (bind):Load the Blob(s)


