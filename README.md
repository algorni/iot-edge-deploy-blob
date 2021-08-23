# IoT Edge Deploy Blobs
A Scalable way to deploy arbitrary Blob(s) to an IoT Edge installation (e.g. configuration files). There are two ways to deploy a certain blob over IoT Edge Devices running the DeployBlobsModule IoT Edge module.

Two tipical scenarios where you need to provide certain configuration files are:
* When using the [Opc Publisher](https://docs.microsoft.com/en-us/azure/industrial-iot/tutorial-publisher-deploy-opc-publisher-standalone) module in standalone mode and using a [Configuration File](https://docs.microsoft.com/en-us/azure/industrial-iot/tutorial-publisher-configure-opc-publisher#configuration-via-configuration-file) to define the published nodes. 
* When using [Azure Stream Analytics as an IoT Edge module](https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-deploy-stream-analytics?view=iotedge-2020-11) and need [Reference Data](https://docs.microsoft.com/en-us/azure/stream-analytics/stream-analytics-use-reference-data#iot-edge-jobs), as only local reference data is supported for Stream Analytics edge jobs.

When using the DeployBlobsModule, you will be able to download blobs content to a local folder within the container running the module. You can take advantage of the device local storage to provide a persistent storage that can be shared with other modules so you can use the downloaded blobs in other modules as per your convenience. 

TODO: This solution can be extended to provide other ways to share the downloaded blobs with other modules.  
> TODO: Provide a property to invoke a "call back method" thru Direct Methods (this callback method can be a custom one or an standard one -restar- for example)

## Debloying blobs by using a Direct Method Call
This option uses a [Direct Method](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-direct-methods) call to the DownloadBlob method provided by the DeployBlobsModule module. The input of the Direct Method call is a list of blobs hosted within a Azure Storage Account container. For security reasons, for each blob, a temporal SAS URL is provided to be able to download the content.  

Using this option, you can reach a certain device by using the DeviceID and directly execute the Direct Method invocation. 

![Download Blobs by calling the Direct Method](https://user-images.githubusercontent.com/45007019/128996747-96128c1e-fc6b-4ac6-b2e4-dad116e812f6.png)

## Deploying blobs by using an IoT Hub Schedule Job
To be able to scale out, and deploy the same set of blob files to multiple IoT Edge Devices, you can [schedule a job](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-jobs) on IoT Hub, the job will invoke the direct method for each device to download the blobs content. Using this option you can deploy blobs files in bulk, and target all the devices running the DeployBlobsModule IoT Edge module or to a certain subset of those by using tags filtering or a list of Device ids.

![Download Blobs by scheduling an IoT Hub Job](https://user-images.githubusercontent.com/45007019/128996747-96128c1e-fc6b-4ac6-b2e4-dad116e812f6.png)

> By now, the scheduled job is invoked inmeditatly but it is quite easy to provide a way to schedule the job to be executed at a certaine time / date by changing or adding some lines of code.

## IoT Edge Module 

Under /source/IotEdgeDeployBlobs.Module you can find an IoT Edge module.
Since it reference another class library under the solution, remember to build the docker from the source root folder with a command like:

    docker build -f "./IotEdgeDeployBlobs.Module/Dockerfile.amd64.debug" -t <your container register>.azurecr.io/blobproxymodule:<your tag> .


Remember to bind the *app/blobs* folder to an host folder or volume.


    {
    "HostConfig": {
        "Binds": [
        "/home/sysadmin/:/app/blobs"
        ]
        }
    }

In case you want to map to an host folder remember to [configure the host folder with the right permission](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11#link-module-storage-to-device-storage) otherwise the module will not be able to write the blob.

## CLI Utility

Under /source/IoTEdgeDeployBlobCli there is a command line utility which requires the following configurations / parameters:

    {
    "IOTHUB_CONNECTIONSTRING": "<your iot hub connection string>",
    "IOT_EDGE_DEVICE_ID": "<your iot edge device>",
    "BLOB_PROXY_MODULE_NAME": "BlobProxyModule",
    "STORAGE_ACCOUNT_NAME": "<your storage account>",
    "STORAGE_KEY": "<storage key>",
    "STORAGE_BLOB_CONTAINER_URL": "https://<your storage account>.blob.core.windows.net/<your blob container>",
    "BLOB_NAME": "<the name of the blob that will land into the storage account>",
    "BLOB_LOCAL_PATH": "c:\\tmp\\sample.json",
    "BLOB_REMOTE_PATH": "/app/blobs/<target>"
    }

   The *BLOB_REMOTE_PATH* in this sample include /app/blobs as initial path. This will land in the Container file system path that should be mounted into the host file system as described above.

   This means that the Module when receive the Direct Method call will save the file locally in the container path which is mounted into the host file system and available for other containers.

   Other containres can mount the same host file system path and access to the Blob file as they need.

   The way the other container reload the content is out of the scope right now.
   It can be just a restart of the module that can be done via DM call to edgeAgent eventually.

## Disclaimer
The code in this repository is provided AS-IS, and the aim is for learning purposes and sample code reference. It is not production-ready, you can use it as an inspiration. It is fully working, but keep in mind that it has been created only for demonstration purposes and to show main architectural elements. It lacks the production quality as it has minimal testing, exception handling, monitoring, etc., as well as we didn't pass thru load or preformance testing.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

<!-- When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments. -->
