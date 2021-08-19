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
using IoTEdgeDeployBlobs.SDK;
using Microsoft.Extensions.Logging.Console;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IoTEdgeDeployBlobs.SampleCli
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
        private static string blobDownloadPath = null;


        static async Task Main(string[] args)
        {
            configuration = new ConfigurationBuilder()
             .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
             .AddJsonFile("appSettings.local.json", optional: true, reloadOnChange: true)
             .AddEnvironmentVariables()
             .AddCommandLine(args)
             .Build();


            using var loggerFactory = LoggerFactory.Create(builder =>
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

            blobDownloadPath = configuration.GetValue<string>("BLOB_REMOTE_PATH");

            if (string.IsNullOrEmpty(blobDownloadPath))
            {
                logger.LogError("This tool requires a <BLOB_REMOTE_PATH> which is the target landing path.");
                return;
            }


            //upload the blob
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

            List<BlobInfo> blobs = new List<BlobInfo>() { blobInfo };


            DeployBlobs deployBlobs = new DeployBlobs(ioTHubConnectionString, blobProxyModuleName, logger);

            await DeploySingle(logger, blobs, deployBlobs);

            await ScheduledDeployJob(blobs, deployBlobs);

        }

        private static async Task ScheduledDeployJob(List<BlobInfo> blobs, DeployBlobs deployBlobs)
        {
            Console.WriteLine();
            Console.WriteLine("> Deploy to Multiple Devices thru Scheduled Job Test:");

            var jobResponse = await deployBlobs.ScheduleDeploymentJobAsync(blobs, $"deviceId IN [ '{iotEdgeDeviceId}' ]");

            do
            {
                Console.WriteLine($"Job Status: {jobResponse.Status.ToString()}...");
                await Task.Delay(1000);

                jobResponse = await deployBlobs.GetDeploymentJobAsync(jobResponse.JobId);
            }
            while ((jobResponse.Status != JobStatus.Completed) && (jobResponse.Status != JobStatus.Failed));

            Console.WriteLine("Final Status:");
            Console.WriteLine("JobStats: " + JsonConvert.SerializeObject(jobResponse.DeviceJobStatistics, Formatting.Indented));
            Console.WriteLine("Job Status : " + jobResponse.Status.ToString());
            Console.WriteLine();

            //GATHER RESPONSES:
            var deviceJobs = await deployBlobs.GetDeploymentJobResponsesAsync(jobResponse.JobId);
            foreach(var deviceJob in deviceJobs)
            {
                Console.WriteLine($"Job {deviceJob.JobId} for device {deviceJob.DeviceId} status: {deviceJob.Status}. Response: {deviceJob.Outcome?.DeviceMethodResponse?.GetPayloadAsJson()}\n");
            }
        }

        private static async Task DeploySingle(ILogger logger, List<BlobInfo> blobs, DeployBlobs deployBlobs)
        {
            Console.WriteLine();
            Console.WriteLine("> Deploy to Single Devices thru Direct Method:");

            var response = await deployBlobs.SingleDeviceDeploymentAsync(iotEdgeDeviceId, blobs);
            foreach (var blobResponse in response.Blobs)
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
