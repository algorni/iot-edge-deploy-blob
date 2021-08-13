using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System.Globalization;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using IoTEdgeDeployBlob.SDK;
using Microsoft.Extensions.Logging.Console;

namespace IoTEdgeDeployBlobCli
{
    class Program
    {
        // The IoT Hub connection string. This is available under the "Shared access policies" in the Azure portal.
        private static IConfiguration configuration;

        private static string ioTHubConnectionString = null; //IOTHUB_CONNECTIONSTRING (service endpoint)
        private static string iotEdgeDeviceId = null;  //DEVICE_ID
        private static string blobProxyModuleName = null;  //MODULE_NAME 

        private static string stroageAccountName = null;
        private static string stroageKey = null;
        private static string stroageBlobContainerUrl = null;
        
        private static string blobName = null;
        private static string blobLocalPath = null;
        private static string blobRemotePath = null;


        static async Task Main(string[] args)
        {
            configuration = new ConfigurationBuilder()
             .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
             .AddEnvironmentVariables()
             .AddCommandLine(args)
             .Build();


            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("IoTEdgeDeployBlobCli.Program", LogLevel.Information)
                    //.AddConsole(options =>
                    //{
                    //    options.FormatterName = ConsoleFormatterNames.Simple;
                    //})
                    .AddSimpleConsole(options =>
                    {
                        options.ColorBehavior = LoggerColorBehavior.Enabled;
                        options.UseUtcTimestamp = true;
                    });
            });

            ILogger logger = loggerFactory.CreateLogger<Program>();



            logger.LogInformation("Hello IoT Edge Deploy Blob command line interface");
            logger.LogInformation("-----------------------");
                      

            //checking configuration requirements
            ioTHubConnectionString = configuration.GetValue<string>("IOTHUB_CONNECTIONSTRING");

            if (string.IsNullOrEmpty(ioTHubConnectionString))
            {
                logger.LogError("This tool requires a <IOTHUB_CONNECTIONSTRING> parameter to connect to IoT Hub to initiate the Device Stream connection.");
                return;
            }

            iotEdgeDeviceId = configuration.GetValue<string>("IOT_EDGE_DEVICE_ID");

            if (string.IsNullOrEmpty(iotEdgeDeviceId))
            {
                logger.LogError("This tool requires a <IOT_EDGE_DEVICE_ID> parameter as target to connect with IoT Hub Device Stream.");
                return;
            }
                        
            blobProxyModuleName = configuration.GetValue<string>("BLOB_PROXY_MODULE_NAME", null);

            if (string.IsNullOrEmpty(blobProxyModuleName))
            {
                logger.LogError("This tool requires a <BLOB_PROXY_MODULE_NAME> parameter as target to connect with IoT Hub Device Stream.");
                return;
            }

            stroageAccountName = configuration.GetValue<string>("STORAGE_ACCOUNT_NAME");

            if (string.IsNullOrEmpty(stroageAccountName))
            {
                logger.LogError("This tool requires a <STORAGE_ACCOUNT_NAME> parameter to connect to Stroage Account.");
                return;
            }


            stroageKey = configuration.GetValue<string>("STORAGE_KEY");

            if (string.IsNullOrEmpty(stroageKey))
            {
                logger.LogError("This tool requires a <STORAGE_KEY> parameter to connect to Stroage Account.");
                return;
            }

            stroageBlobContainerUrl = configuration.GetValue<string>("STORAGE_BLOB_CONTAINER_URL");

            if (string.IsNullOrEmpty(stroageBlobContainerUrl))
            {
                logger.LogError("This tool requires a <STORAGE_BLOB_CONTAINER_URL> parameter reppresenting the storage blob container URL. Like: https://<yourstroage>.blob.core.windows.net/<yourcontainer>");
                return;
            }
            
            blobName = configuration.GetValue<string>("BLOB_NAME");

            if (string.IsNullOrEmpty(blobName))
            {
                logger.LogError("This tool requires a <BLOB_NAME> which you want to upload to the Storage Accounte Container.");
                return;
            }

            blobLocalPath = configuration.GetValue<string>("BLOB_LOCAL_PATH");

            if (string.IsNullOrEmpty(blobLocalPath))
            {
                logger.LogError("This tool requires a <BLOB_LOCAL_PATH> from there to open the file for the upload to the Storage Accounte Container.");
                return;
            }

            blobRemotePath = configuration.GetValue<string>("BLOB_REMOTE_PATH");

            if (string.IsNullOrEmpty(blobRemotePath))
            {
                logger.LogError("This tool requires a <BLOB_REMOTE_PATH> which is the target landing path.");
                return;
            }
 

            //upload the blob
            ILogger<AzureStorageHelper> loggerAzureStorageHelper = loggerFactory.CreateLogger<AzureStorageHelper>();

            AzureStorageHelper azureStorageHelper = new AzureStorageHelper(loggerAzureStorageHelper);

            var stroageBlobContainerUri = new Uri(stroageBlobContainerUrl);

            await azureStorageHelper.UploadBlobToStorage(stroageAccountName, stroageKey, stroageBlobContainerUri, blobLocalPath, blobName);

            var blobSasUrl = azureStorageHelper.GetBlobDownladUri(stroageAccountName, stroageKey, stroageBlobContainerUri, blobName, TimeSpan.FromMinutes(10) );




            //now start the Direct Method call to instruct the 

            Microsoft.Azure.Devices.TransportType transportType = Microsoft.Azure.Devices.TransportType.Amqp;

            //initiate a client to IoT Hub 
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(ioTHubConnectionString, transportType);

            logger.LogInformation("IoT Hub Service Client connected");

            //initiating the Direct Method to start the stream on the other end...
            var methodRequest = new CloudToDeviceMethod(
                    DirectMethodDownloadBlob.DownloadBlob //constant with the Direct Method name
                );


            DownloadBlobRequest downloadBlobRequest = new DownloadBlobRequest();

            BlobInfo blobInfo = new BlobInfo()
                {
                    BlobName = blobName,
                    BlobSASUrl = blobSasUrl.ToString(),
                    BlobRemotePath = blobRemotePath
                };

            downloadBlobRequest.Blobs.Add(blobInfo);

            methodRequest.SetPayloadJson(downloadBlobRequest.ToJson());

            //perform a Direct Method to the remote device to initiate the device stream!
            CloudToDeviceMethodResult response = null;

            response = await serviceClient.InvokeDeviceMethodAsync(iotEdgeDeviceId, blobProxyModuleName, methodRequest);

            DownloadBlobResponse responseObj = DownloadBlobResponse.FromJson(response.GetPayloadAsJson());

            foreach (var blobResponse in responseObj.Blobs)
            {
                if (blobResponse.BlobDownloaded)
                {
                    logger.LogInformation($"Blob {blobResponse.BlobName} Downloaded remotely.");
                }
                else
                {
                    logger.LogError($"Error while calling remote direct method to initiate the Blob download for {blobResponse.BlobName} on Edge.\n{blobResponse.Reason}");
                }
            }

        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}
