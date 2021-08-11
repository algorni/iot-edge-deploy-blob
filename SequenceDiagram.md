# Sequence Diagrams

## The Tool

Sequence diagrams is frequently challanging to draw...  and in case of changes in your code even more messy to maintain!!

Recently i've discovered this free online editor: https://sequencediagram.org/

With that tool you can just create your diagram by declaring the interactions and it will render a nice visual reppresentation on the fly!

Just like this:


## My Diagram Source Code

This is the "source code" for the main sequence diagram 

    title Deploy a Blob to IoT Edge 

    participant Storage Account
    participant DeployBlobSDK
    participant Iot Hub
    participant BlobProxyModule
    participant File System (bind)
    participant Consumer Docker

    DeployBlobSDK->Storage Account:Upload Blob(s)

    DeployBlobSDK<--Storage Account:SAS Key(s)

    DeployBlobSDK->Iot Hub:Deploy Blob(s) (via DM)

    Iot Hub->BlobProxyModule:Direct Method Call with the SASKey to download \nConfiguration File from Storage Account

    BlobProxyModule->Storage Account:Download Blob(s)

    BlobProxyModule->File System (bind):Save Blob(s)

    BlobProxyModule-->Iot Hub:DM Confirm blob saved on file system.

    Iot Hub-->DeployBlobSDK:DM Completed

    note over Consumer Docker,File System (bind):You need to notify the target module to reload the Blob(s)\nOr ensure Module watches for file updates

    DeployBlobSDK-->Consumer Docker:Notify to Reload the Blob(s)

    Consumer Docker->File System (bind):Load the Blob(s)


