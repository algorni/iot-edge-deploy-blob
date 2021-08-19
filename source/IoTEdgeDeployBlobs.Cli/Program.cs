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
        private static string stroageKey = null;
        private static string stroageBlobContainerUrl = null;
        
        private static string blobName = null;
        private static string blobLocalPath = null;
        private static string blobDownloadPath = null;

        private static ILogger logger;


        static async Task Main(string[] args)
        {
            ILoggerFactory loggerFactory = SetupLoggerFactory();
            logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation(new String('-', 40));
            logger.LogInformation("IoT Edge Deploy Blob command line sample");
            logger.LogInformation(new String('-', 40));

            SetConfiguration(args);

            //upload the blob to be distributed among IoT Edge Devices
            BlobInfo blobInfo = await UploadSampleBlob(loggerFactory);
            List<BlobInfo> blobs = new List<BlobInfo>() { blobInfo };

            //prepare deployBlobs
            ILogger loggerDeployBlobs = loggerFactory.CreateLogger<DeployBlobs>();
            DeployBlobs deployBlobs = new DeployBlobs(ioTHubConnectionString, deployBlobsModuleName, loggerDeployBlobs);

            await DeploySingleDevice(blobs, deployBlobs);

            //Deploy blob to all devices running the IotEdgeDeployModule using an Scheduled Job
            await ScheduledDeployJob(blobs, deployBlobs);

            //Deploy blob to certain devices running the IotEdgeDeployModule using an Scheduled Job
            IEnumerable<string> devicesIds = await deployBlobs.GetEdgeDevicesIdsAsync("tags.deviceType = 'Edge' or tags.deviceType = 'Edge-SQL-Test'");
            await ScheduledDeployJob(blobs, deployBlobs, devicesIds);
        }

        private static async Task DeploySingleDevice(List<BlobInfo> blobs, DeployBlobs deployBlobs)
        {
            logger.LogInformation("> Deploy to Single Devices thru Direct Method:");

            var response = await deployBlobs.DeployBlobsAsync(iotEdgeDeviceId, blobs);
            foreach (var blobResponse in response.Blobs)
            {
                //TODO: Extract and share with schedule
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

        private static async Task ScheduledDeployJob(List<BlobInfo> blobs, DeployBlobs deployBlobs, IEnumerable<string> devicesIds = null)
        {
            logger.LogInformation("> Deploy to Multiple Devices thru Scheduled Job Test.");
            JobResponse jobResponse = null;
            if (devicesIds == null){
                //the following scheduled job will deploy the blobs to all devices running the IoTEdgeDeployBlobs module
                logger.LogInformation("Targeting to all Edge Devices running DeployBlobsModule.");
                jobResponse = await deployBlobs.ScheduleDeployBlobsJobAsync(blobs);
            }
            else
            {
                //the following scheduled job will deploy the blobs to all devices running the IoTEdgeDeployBlobs module and which deviceId is in the list provided
                logger.LogInformation($"Targeting to all Edge Devices running DeployBlobsModule with following IDs: {String.Join(", ",devicesIds)}.");
                jobResponse = await deployBlobs.ScheduleDeployBlobsJobAsync(blobs, devicesIds);
            }
            //optionally, you can provide a queryCondition over the device.modules schema to create a custom filter

            logger.LogInformation($"Created scheduled job with jobId '{jobResponse}'");
            do
            {
                logger.LogInformation($"Job Status: {jobResponse.Status.ToString()}...");
                await Task.Delay(1000);

                jobResponse = await deployBlobs.GetDeploymentJobAsync(jobResponse.JobId);
            }
            while ((jobResponse.Status != JobStatus.Completed) && (jobResponse.Status != JobStatus.Failed));

            logger.LogInformation("Job Final Status : " + jobResponse.Status.ToString());
            logger.LogInformation("Job Stats: " + JsonConvert.SerializeObject(jobResponse.DeviceJobStatistics, Formatting.Indented));

            //GATHER RESPONSES:
            var deviceJobs = await deployBlobs.GetDeploymentJobResponsesAsync(jobResponse.JobId);
            foreach (var deviceJob in deviceJobs)
            {

                logger.LogInformation($"Edge Device: {deviceJob.DeviceId}");
                logger.LogInformation($"Response Payload:", deviceJob.Outcome?.DeviceMethodResponse?.GetPayloadAsJson());
                logger.LogInformation($"--");
            }
        }


        private static async Task<BlobInfo> UploadSampleBlob(ILoggerFactory loggerFactory)
        {
            ILogger<AzureStorage> loggerAzureStorageHelper = loggerFactory.CreateLogger<AzureStorage>();
            AzureStorage azureStorageHelper = new AzureStorage(loggerAzureStorageHelper);
            var stroageBlobContainerUri = new Uri(stroageBlobContainerUrl);
            await azureStorageHelper.UploadBlobToStorage(stroageAccountName, stroageKey, stroageBlobContainerUri, blobLocalPath, blobName);
            var blobSasUrl = azureStorageHelper.GetBlobDownladUri(stroageAccountName, stroageKey, stroageBlobContainerUri, blobName, TimeSpan.FromMinutes(10));


            BlobInfo blobInfo = new BlobInfo()
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
                    .AddFilter("IoTEdgeDeployBlobCli.Program", LogLevel.Information)
                    .AddConsole(options =>
                    {
                        options.FormatterName = ConsoleFormatterNames.Simple;
                    })
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
            stroageKey = GetConfigValue("STORAGE_KEY","This tool requires a <STORAGE_KEY> parameter to connect to Stroage Account.");
            stroageBlobContainerUrl = GetConfigValue("STORAGE_BLOB_CONTAINER_URL", "This tool requires a <STORAGE_BLOB_CONTAINER_URL> parameter reppresenting the storage blob container URL. Like: https://<yourstroage>.blob.core.windows.net/<yourcontainer>");
            blobName = GetConfigValue("BLOB_NAME", "This tool requires a <BLOB_NAME> which you want to upload to the Storage Accounte Container.");
            blobLocalPath = GetConfigValue("BLOB_LOCAL_PATH", "This tool requires a <BLOB_LOCAL_PATH> from there to open the file for the upload to the Storage Accounte Container.");
            blobDownloadPath = GetConfigValue("BLOB_REMOTE_PATH","This tool requires a <BLOB_REMOTE_PATH> which is the target landing path.");
        }

        private static string GetConfigValue(string configValue, string missingMessage)
        {
            string v = configuration.GetValue<string>(configValue);
            if (string.IsNullOrEmpty(v))
            {
                logger.LogError($"The '{configValue}' is missing. {missingMessage}");
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
