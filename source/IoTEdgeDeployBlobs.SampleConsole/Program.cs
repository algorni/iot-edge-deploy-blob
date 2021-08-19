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
using IoTEdgeDeployBlobs.Sdk;
using Microsoft.Extensions.Logging.Console;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IoTEdgeDeployBlobs.Cli
{
    class Program
    {
        // The IoT Hub connection string. This is available under the "Shared access policies" in the Azure portal.
        // To be able to invoke a direct method directly on a device, the Service Connect permission is required.
        // To be able to schedule a job and query the status with IoT Hub, the Registry Read and Registry Write permissions are required.
        // A Custom Policy can be created in IoT Hub with the required permissions

        private static IConfiguration configuration;

        private static string ioTHubConnectionString = null; //IOTHUB_CONNECTIONSTRING (service endpoint)
        private static string iotEdgeDeviceId = null;  //DEVICE_ID
        private static string deployBlobsModuleName = "DeployBlobsModule";  //MODULE_NAME -> defaults to DeployBlobsModule in the console sample. Can be overriden.

        private static string stroageAccountName = null;
        private static string storageKey = null;
        private static string stroageBlobContainerUrl = null;

        //private static string blobName = null;
        //private static string blobLocalPath = null;
        //private static string blobDownloadPath = null;

        private static ILoggerFactory loggerFactory;
        //private static ILogger logger;


        static async Task Main(string[] args)
        {
            loggerFactory = SetupLoggerFactory();
            //logger = loggerFactory.CreateLogger<Program>();
            Console.WriteLine(new String('-', 40));
            Console.WriteLine("IoT Edge Deploy Blob command line sample");
            Console.WriteLine(new String('-', 40));

            SetConfiguration(args);

            //upload the blob to be distributed among IoT Edge Devices
            BlobInfo opcPublisherBlobInfo = await PrepareSampleBlobForDeployAsync("./SampleFiles/pn.json", "/blobsDownloads/opcPublisher");
            BlobInfo otherBlobInfo = await PrepareSampleBlobForDeployAsync("./SampleFiles/otherContent.txt", "/blobsDownloads");
            List <BlobInfo> blobs = new() { opcPublisherBlobInfo, otherBlobInfo };

            //prepare deployBlobs
            ILogger loggerDeployBlobs = loggerFactory.CreateLogger<DeployBlobs>();
            DeployBlobs deployBlobs = new(ioTHubConnectionString, deployBlobsModuleName, loggerDeployBlobs);

            //Deploy to a single device thru DirectMethod
            await DeploySingleDevice(blobs, deployBlobs);

            //Deploy blob to all devices running the IotEdgeDeployModule using an Scheduled Job
            await ScheduledDeployJob(blobs, deployBlobs);

            //Deploy blob to certain devices running the IotEdgeDeployModule using an Scheduled Job
            IEnumerable<string> devicesIds = await deployBlobs.GetEdgeDevicesIdsAsync("tags.deviceType = 'Edge-SQL-Test'");
            await ScheduledDeployJob(blobs, deployBlobs, devicesIds);
        }

        private static async Task DeploySingleDevice(List<BlobInfo> blobs, DeployBlobs deployBlobs)
        {
            Console.WriteLine("> Deploy to Single Devices thru Direct Method:");

            var response = await deployBlobs.DeployBlobsAsync(iotEdgeDeviceId, blobs);
            foreach (var blobResponse in response.Blobs)
            {
                PrintResponseInfo(blobResponse);
            }
        }

        public static void PrintResponseInfo(BlobResponseInfo response)
        {
            Console.WriteLine($"Blob: {response.BlobName}.");
            Console.WriteLine($"Blob Downloaded Remotely: { response.BlobDownloaded }.");
            Console.WriteLine($"Reason: {response.Reason}");
        }

        private static async Task ScheduledDeployJob(List<BlobInfo> blobs, DeployBlobs deployBlobs, IEnumerable<string> devicesIds = null)
        {
            Console.WriteLine("> Deploy to Multiple Devices thru Scheduled Job Test.");
            JobResponse jobResponse;
            if (devicesIds == null)
            {
                //the following scheduled job will deploy the blobs to all devices running the IoTEdgeDeployBlobs module
                Console.WriteLine("Targeting to all Edge Devices running DeployBlobsModule.");
                jobResponse = await deployBlobs.ScheduleDeployBlobsJobAsync(blobs);
            }
            else
            {
                //the following scheduled job will deploy the blobs to all devices running the IoTEdgeDeployBlobs module and which deviceId is in the list provided
                Console.WriteLine($"Targeting to all Edge Devices running DeployBlobsModule with following IDs: {String.Join(", ", devicesIds)}.");
                jobResponse = await deployBlobs.ScheduleDeployBlobsJobAsync(blobs, devicesIds);
            }
            //optionally, you can provide a queryCondition over the device.modules schema to create a custom filter

            Console.WriteLine($"Created scheduled job with jobId '{jobResponse.JobId}'");
            do
            {
                Console.WriteLine($"Job Status: {jobResponse.Status}...");
                await Task.Delay(1000);

                jobResponse = await deployBlobs.GetDeploymentJobAsync(jobResponse.JobId);
            }
            while ((jobResponse.Status != JobStatus.Completed) && (jobResponse.Status != JobStatus.Failed));

            Console.WriteLine("Job Final Status : " + jobResponse.Status.ToString());
            Console.WriteLine("Job Stats: " + JsonConvert.SerializeObject(jobResponse.DeviceJobStatistics, Formatting.Indented));

            //GATHER RESPONSES:
            var deviceJobs = await deployBlobs.GetDeploymentJobResponsesAsync(jobResponse.JobId);
            foreach (var deviceJob in deviceJobs)
            {
                Console.WriteLine($"Edge Device: {deviceJob.DeviceId}");
                Console.WriteLine($"Status: {deviceJob.Status}");
                if (deviceJob.Outcome != null && deviceJob.Outcome.DeviceMethodResponse != null) {
                    DownloadBlobsResponse response = DownloadBlobsResponse.FromJson(deviceJob.Outcome.DeviceMethodResponse.GetPayloadAsJson());
                    foreach (var blobResponse in response.Blobs)
                    {
                        PrintResponseInfo(blobResponse);
                    }
                }
                if(deviceJob.Status == DeviceJobStatus.Failed)
                {
                    Console.WriteLine($"Device Job Error: {deviceJob.Error?.Description}");
                }
                Console.WriteLine($"--");
            }
        }


        private static async Task<BlobInfo> PrepareSampleBlobForDeployAsync(string localFile, string remoteDownloadPath)
        {
            string blobName = System.IO.Path.GetFileName(localFile);
            string blobDownloadPath = $"{remoteDownloadPath}/{blobName}";

            ILogger<AzureStorage> storageLogger = loggerFactory.CreateLogger<AzureStorage>();
            AzureStorage azureStorageHelper = new(storageLogger);
            var stroageBlobContainerUri = new Uri(stroageBlobContainerUrl);
            await azureStorageHelper.UploadBlobToStorageAsync(stroageAccountName, storageKey, stroageBlobContainerUri, localFile, blobName);
            var blobSasUrl = azureStorageHelper.GetBlobDownloadUri(stroageAccountName, storageKey, stroageBlobContainerUri, blobName, TimeSpan.FromMinutes(10));


            BlobInfo blobInfo = new()
            {
                Name = blobName,
                SasUrl = blobSasUrl.ToString(),
                DownloadPath = blobDownloadPath
            };
            return blobInfo;
        }

        private static ILoggerFactory SetupLoggerFactory()
        {
            return LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("IoTEdgeDeployBlobs.Cli.Program", LogLevel.Information)
                    .AddSimpleConsole(options =>
                    {
                        options.ColorBehavior = LoggerColorBehavior.Enabled;
                        options.UseUtcTimestamp = true;
                        options.IncludeScopes = false;
                        options.SingleLine = true;
                    });
            });
        }

        private static void SetConfiguration(string[] args)
        {
            configuration = new ConfigurationBuilder()
             .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
             .AddJsonFile("appSettings.local.json", optional: true, reloadOnChange: true)
             .AddEnvironmentVariables()
             .AddCommandLine(args)
             .Build();

            //checking configuration requirements
            ioTHubConnectionString = GetConfigValue("IOTHUB_CONNECTIONSTRING", "This tool requires a <IOTHUB_CONNECTIONSTRING> parameter to connect to IoT Hub to initiate the Device Stream connection.");
            iotEdgeDeviceId = GetConfigValue("IOT_EDGE_DEVICE_ID", "This tool requires a <IOT_EDGE_DEVICE_ID> parameter as target to connect with IoT Hub Device Stream.");
            deployBlobsModuleName = GetConfigValue("DEPLOY_BLOBS_MODULE_NAME", "This tool requires a <DEPLOY_BLOBS_MODULE_NAME> parameter as target to connect with IoT Hub Device Stream.");
            stroageAccountName = GetConfigValue("STORAGE_ACCOUNT_NAME", "This tool requires a <STORAGE_ACCOUNT_NAME> parameter to connect to Stroage Account.");
            storageKey = GetConfigValue("STORAGE_KEY", "This tool requires a <STORAGE_KEY> parameter to connect to Stroage Account.");
            stroageBlobContainerUrl = GetConfigValue("STORAGE_BLOB_CONTAINER_URL", "This tool requires a <STORAGE_BLOB_CONTAINER_URL> parameter reppresenting the storage blob container URL. Like: https://<yourstroage>.blob.core.windows.net/<yourcontainer>");
            //blobName = GetConfigValue("BLOB_NAME", "This tool requires a <BLOB_NAME> which you want to upload to the Storage Accounte Container.");
            //blobLocalPath = GetConfigValue("BLOB_LOCAL_PATH", "This tool requires a <BLOB_LOCAL_PATH> from there to open the file for the upload to the Storage Accounte Container.");
            //blobDownloadPath = GetConfigValue("BLOB_REMOTE_PATH", "This tool requires a <BLOB_REMOTE_PATH> which is the target landing path.");
        }

        private static string GetConfigValue(string configValue, string missingMessage)
        {
            string v = configuration.GetValue<string>(configValue);
            if (string.IsNullOrEmpty(v))
            {
                Console.WriteLine($"The '{configValue}' is missing. {missingMessage}");
                throw new Exception(missingMessage);
            }
            return v;
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
