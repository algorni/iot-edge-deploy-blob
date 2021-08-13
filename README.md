# iot-edge-deploy-blob
A Scalable way to deploy arbitrary Blob(s) to an IoT Edge installation (e.g. configuration files)

![Untitled](https://user-images.githubusercontent.com/45007019/128996747-96128c1e-fc6b-4ac6-b2e4-dad116e812f6.png)




## IoT Edge Module 

Under /source/BlobProxyModule you can find an IoT Edge module.
Since it reference another class library under the solution remember to build the docker from the source root folder with a command like:

    docker build -f "./BlobProxyModule/Dockerfile.amd64.debug" -t <your container register>.azurecr.io/blobproxymodule:<your tag> .

The amd64 amd64.debug and the arm32 was amended to work properly. The others are available just for reference but need to be amended to use .NET 5.0 and considering the right build path

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

