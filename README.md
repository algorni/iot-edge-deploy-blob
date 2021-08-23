# IoT Edge Deploy Blobs
A scalable way to deploy arbitrary blob(s) to an IoT Edge installation (e.g. configuration files). There are two ways to deploy a certain blob over IoT Edge Devices running the DeployBlobsModule module: using [direct methods](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-direct-methods) or using a [scheduled job](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-jobs).

Two tipical scenarios where you need to provide certain configuration files are:
* When using the [Opc Publisher](https://docs.microsoft.com/en-us/azure/industrial-iot/tutorial-publisher-deploy-opc-publisher-standalone) module in standalone mode and using a [Configuration File](https://docs.microsoft.com/en-us/azure/industrial-iot/tutorial-publisher-configure-opc-publisher#configuration-via-configuration-file) to define the published nodes. 
* When using [Azure Stream Analytics as an IoT Edge module](https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-deploy-stream-analytics?view=iotedge-2020-11) and need [Reference Data](https://docs.microsoft.com/en-us/azure/stream-analytics/stream-analytics-use-reference-data#iot-edge-jobs), as only local reference data is supported for Stream Analytics edge jobs.

By using the DeployBlobsModule, you will be able to download blobs content to a local folder within the container running the module. You can take advantage of [device's local storage](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11) to provide a persistent storage to be shared with other modules so you can use the downloaded blobs in other modules as per your convenience. 

## Deploying blobs by using a Direct Method Call
This option uses a [Direct Method](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-direct-methods) call to the DownloadBlob method provided by the DeployBlobsModule module. The input of the Direct Method call is a list of blobs hosted within an Azure Storage Account container. For security reasons, for each blob, a temporal SAS URL is provided to be able to download the content.  

Using this option, you can reach a certain device by using the DeviceID and directly execute the Direct Method invocation. 

![Download Blobs by calling the Direct Method](https://user-images.githubusercontent.com/2638875/130454984-c61a49f3-7fa0-43a4-8978-b2bfb6bc3de3.jpg)

## Deploying blobs by using an IoT Hub Schedule Job
To be able to scale out, and deploy the same set of blob files to multiple IoT Edge Devices, you can [schedule a job](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-jobs) on IoT Hub, the job will invoke the direct method for each device to download the blobs content. Using this option you can deploy blobs files in bulk, and target all the devices running the DeployBlobsModule IoT Edge module or targeting just a certain subset of those using tags filtering or a list of device ids.

![Download Blobs by scheduling an IoT Hub Job](https://user-images.githubusercontent.com/2638875/130455948-4350a6d6-b0b9-4bca-ad09-9dbebe84d33b.jpg)

> By now, the scheduled job is invoked inmeditatly but it is quite easy to provide a way to schedule the job to be executed at a certaine time and date by changing or adding some lines of code.

## IoT Edge Module

### How the module works
The module creates a direct method handler to execute the DownloadBlobs method which accepts a `List<BlobInfo>` as the list of blobs to be downloaded. The `BlobInfo` class is defined as follows:

```c#
    public class BlobInfo
    {
        /// <summary>
        /// The Blob SAS Url
        /// </summary>
        public string SasUrl { get; set; }
        /// <summary>
        /// The local path (inside the DeployBlobModule) where to store the file
        /// </summary>
        public string DownloadPath { get; set; }
        /// <summary>
        /// The name of the blob
        /// </summary>
        public string Name { get; set; }
    }
``` 

So, you provide the Blob SAS Url, the DownloadPath (relative to the container running the module) and the blob name. You can use whatever the path you want, but you need to ensure the path exists and running process have enought permissions to write to it. If resulting target path already exist, it will be replaced.

To be able to share the downloaded blobs with other modules, you will need to use the [device's local storage](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11)

For example, if you use `/app/blobs` as the `DownloadPath` you can map a host folder or volume using the `HostConfig/Binds` [Container Create Options](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-use-create-options?view=iotedge-2020-11).

```json
//Host Config for DeployBlobsModule module
{
    "HostConfig": {
        "Binds": [
            "/etc/iotedge/deployBlobs:/app/blobs"
        ]
    }
}
```

With this configuration, the host path `/etc/iotedge/deployBlobs` will be mounted as `/app/blobs` for the DeployBlobsModule.

A back-end application (console, web app, function app) can upload a `publishednodes.json` file to the `blobconfig` container in the storage account. We will need to create a `BlobInfo` object as follows (to be included in the request):
```c#
    BlobInfo blobInfo = new()
    {
        Name = myFile.json,
        SasUrl = "https://<storageAccount>.blob.core.windows.net/blobconfig/publishednodes.json?sv=2020-04-08&st=2021-08-23T14%3A07%3A24Z&se=2021-08-23T14%3A17%3A00Z&sr=b&sp=r&sig=0B0Cv8k1rc9%2FcknoYUXKeF%2Fstd39tgN7LLDyPBArTNA%3D",
        DownloadPath = "/app/blobs/opcPublisher"
    };
```

Then, that back-end application invokes the direct method directly, by using the `DeployBlobsAsync()` method or by creating an scheduled job using the `ScheduleDeployBlobsJobAsync()` (both provided by the IoTEdgeDeployBlobs.Sdk library).

Once the `DownloadBlobs` direct method is executed (directly or using a scheduled job), the content of `publishednodes.json` located in the storage account will be downloaded to the module folder `/app/blobs/opcPublisher`, so at the host, the file `/etc/iotedge/deployBlobs/opcPublisher/publishednodes.json` will be created.

To allow other modules to use the downloaded blobs, you can map the download host path to other modules. For example, imagine that we have an Opc Publisher module created with the following Container Create Options:

```json
//Host Config for OpcPublisher module
{
  "HostConfig": {
    "Binds": [
      "/etc/iotedge/deployBlobs/opcPublisher:/blobsConfig"
    ]
  },
  "Hostname": "opcpublisher",
  "Cmd": [
    "--pf=/blobsConfig/publishednodes.json",
    "--mm=PubSub",
    "--fm=true",
    "--fd=false",
    "--bs=100",
    "--di=20",
    "--sc=1",
    "--aa"
  ]
}
```

With this, the Opc Publisher module will react to the updates of the `publishednodes.json` file done usign the DeployBlobsModule.

Be sure you [configure the host folder with the right permission](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11#link-module-storage-to-device-storage), otherwise the module will not be able to write the blob.

## Building the module
Under /source/IotEdgeDeployBlobs.Module you can find an IoT Edge module.
Since it reference another class library under the solution, remember to build the docker from the source root folder with a command like:

    docker build -f "./IotEdgeDeployBlobs.Module/Dockerfile.amd64.debug" -t <your container register>.azurecr.io/blobproxymodule:<your tag> .

## IoTEdgeDeployBlobs.Sdk library
The IoTEdgeDeployBlobs.Sdk provide several methods to ease the blob storage file handling (upload, download, SAS url generation),as well as the Direct Method invocation, schedule job creation and responses retrival of the invocations.

The Sdk is used in both sides, the DeployBlobsModule and the external client to request the invocations or job scheduling. You can implement any kind of external client (console apps, web app, azure function) just using the Sdk. 

## Sample Console

Under /source/IoTEdgeDeployBlob.SampleConsole there is a console utility to show case the usage of the Sdk to create the requests and manage the responses. 

The Sample console requires the following configuration:

```json
{
  "IOTHUB_CONNECTIONSTRING": "<your iot hub connection string>", //Ensure you have a Shared Access Policy that allows Service Connect and Registry Read & Write
  "IOT_EDGE_DEVICE_ID": "<your iot edge device>",
  "DEPLOY_BLOBS_MODULE_NAME": "DeployBlobsModule",
  "STORAGE_ACCOUNT_NAME": "<your storage account>",
  "STORAGE_KEY": "<storage key>",
  "STORAGE_BLOB_CONTAINER_URL": "https://<your storage account>.blob.core.windows.net/<your blob container>"
}
```

* `IOTHUB_CONNECTIONSTRING`: This is the connection string to the target IoT Hub. To be able to create an IoT Hub scheduled job, a Shared Access Policy with "Registry Read" and "Registry Write" permissions is required. To be able to direct invoke a Direct Method, a Shared Access Policy with "Service Connect" permission is required.
* `IOT_EDGE_DEVICE_ID`: Target device id for the sample.
* `DEPLOY_BLOBS_MODULE_NAME`: Name assigned to the DeployBlobsModule in the IoT Edge device definition (defined thru the "Set Modules" or by a IoT Edge deployment). It is important to have a consistent naming to ensure we reach the desired edge devices when invoking thru the scheduled jobs (as the schedule job is executed against the edge devices by using [module twin queries](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-query-language#module-twin-queries))

The rest of the configuration settings are needed to handle the blob uploads and downloads.

You can add a file appSettings.local.json to the project with your custom values. This file is excluded from the git repository but, if exists, it is loaded while launching.

<IMAGE> 


The way the other container reload the content is out of the scope right now.
It can be just a restart of the module that can be done via DM call to edgeAgent eventually.

> Idea: You can extend the `DownloadBlobsRequest` to include a property to define an [edgeAgent built-in direct method](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-edgeagent-direct-method?view=iotedge-2020-11) to be invoked as a kind of "call back method" after a proper execution of the DownloadBlobs method (for example, you can call the built-in `RestartModule` once the download has successfully finished).

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
