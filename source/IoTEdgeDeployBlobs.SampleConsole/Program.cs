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
    /// <summary>
    /// Regarding IoT Hub Connection String:
    /// * The IoT Hub connection string. This is available under the "Shared access policies" in the Azure portal.
    /// * To be able to invoke a direct method directly on a device, the Service Connect permission is required.
    /// * To be able to schedule a job and query the status with IoT Hub, the Registry Read and Registry Write permissions are required.
    /// * A Custom Policy can be created in IoT Hub with the required permissions (Service Connection, Registry Read, Registry Write)
    ///
    /// References:
    /// https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-jobs
    /// https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-csharp-csharp-schedule-jobs
    /// https://docs.microsoft.com/en-us/azure/iot-hub/quickstart-control-device?pivots=programming-language-csharp
    /// https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-query-language

    /// </summary>
    class Program
    {
        private static IConfiguration configuration;

        private static string _ioTHubConnectionString;  //IOTHUB_CONNECTIONSTRING (service endpoint)
        private static string _iotEdgeDeviceId;         //DEVICE_ID
        private static string _deployBlobsModuleName;   //MODULE_NAME -> It is important to match the Edge Device deployment module name.

        private static string _stroageAccountName;
        private static string _storageKey;
        private static string _stroageBlobContainerUrl;

        private static ILoggerFactory _loggerFactory;

        private static DeployBlobs _deployBlobs;

        static async Task Main(string[] args)
        {
            Console.WriteLine(new String('-', 40));
            Console.WriteLine("IoT Edge Deploy Blob samples");
            Console.WriteLine(new String('-', 40));

            SetupLoggerFactory();
            SetConfiguration(args);

            //Instantiate deployBlobs
            ILogger loggerDeployBlobs = _loggerFactory.CreateLogger<DeployBlobs>(); 
            _deployBlobs = new(_ioTHubConnectionString, _deployBlobsModuleName, loggerDeployBlobs);

            //Setup some sample blobs to to be distributed among IoT Edge Devices
            BlobInfo opcPublisherBlobInfo = await PrepareSampleBlobForDeployAsync("./SampleFiles/pn.json", "/blobsDownloads/opcPublisher");
            BlobInfo otherBlobInfo = await PrepareSampleBlobForDeployAsync("./SampleFiles/otherContent.txt", "/blobsDownloads");
            List <BlobInfo> blobs = new() { opcPublisherBlobInfo, otherBlobInfo };

            //Deploy to a single device thru DirectMethod
            await DeploySingleDevice(blobs);

            //Deploy blob to all devices running the IotEdgeDeployModule using an Scheduled Job
            await ScheduleDeployJob(blobs);

            //Deploy blob to certain devices running the IotEdgeDeployModule using an Scheduled Job
            IEnumerable<string> devicesIds = await _deployBlobs.GetEdgeDevicesIdsAsync("tags.deviceType = 'Edge-SQL-Test'");
            await ScheduleDeployJob(blobs, devicesIds);
        }

        private static async Task DeploySingleDevice(List<BlobInfo> blobs)
        {
            Console.WriteLine("> Deploy to Single Devices thru Direct Method:");

            var response = await _deployBlobs.DeployBlobsAsync(_iotEdgeDeviceId, blobs);
            foreach (var blobResponse in response.Blobs)
            {
                PrintResponseInfo(blobResponse);
            }

            Console.WriteLine();
        }

        private static async Task ScheduleDeployJob(List<BlobInfo> blobs, IEnumerable<string> devicesIds = null)
        {
            Console.WriteLine("> Scheduling job to deploy to Multiple Devices.");
            JobResponse jobResponse;
            if (devicesIds == null)
            {
                //the following scheduled job will deploy the blobs to all devices running the IoTEdgeDeployBlobs module
                Console.WriteLine("Targeting to all Edge Devices running DeployBlobsModule.");
                jobResponse = await _deployBlobs.ScheduleDeployBlobsJobAsync(blobs);
            }
            else
            {
                //the following scheduled job will deploy the blobs to all devices running the IoTEdgeDeployBlobs module and which deviceId is in the list provided
                Console.WriteLine($"Targeting to all Edge Devices running DeployBlobsModule with following IDs: {String.Join(", ", devicesIds)}.");
                jobResponse = await _deployBlobs.ScheduleDeployBlobsJobAsync(blobs, devicesIds);
            }

            Console.WriteLine($"Created scheduled job with jobId '{jobResponse.JobId}'");
            await MonitorJobExecution(jobResponse.JobId);

            await PrintDevicesMethodResponses(jobResponse.JobId);
        }

        private static async Task PrintDevicesMethodResponses(string jobId)
        {
            var deviceJobs = await _deployBlobs.GetDeploymentJobResponsesAsync(jobId);
            foreach (var deviceJob in deviceJobs)
            {
                Console.WriteLine($"Edge Device: {deviceJob.DeviceId}");
                Console.WriteLine($"Status: {deviceJob.Status}");
                if (deviceJob.Outcome != null && deviceJob.Outcome.DeviceMethodResponse != null)
                {
                    DownloadBlobsResponse response = DownloadBlobsResponse.FromJson(deviceJob.Outcome.DeviceMethodResponse.GetPayloadAsJson());
                    if (response != null)
                    {
                        foreach (var blobResponse in response.Blobs)
                        {
                            PrintResponseInfo(blobResponse);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Method Response Status: {deviceJob.Outcome.DeviceMethodResponse.Status}.");
                    }
                }
                if (deviceJob.Status == DeviceJobStatus.Failed)
                {
                    Console.WriteLine($"Device Job Error: {deviceJob.Error?.Description}");
                }
                Console.WriteLine($"--");
            }
        }

        private static async Task MonitorJobExecution(string jobId)
        {
            var jobResponse = await _deployBlobs.GetDeploymentJobAsync(jobId);
            do
            {
                Console.WriteLine($"Job Status: {jobResponse.Status}...");
                await Task.Delay(1000);

                jobResponse = await _deployBlobs.GetDeploymentJobAsync(jobResponse.JobId);
            }
            while ((jobResponse.Status != JobStatus.Completed) && (jobResponse.Status != JobStatus.Failed));

            Console.WriteLine("Job Final Status : " + jobResponse.Status.ToString());
            Console.WriteLine("Job Stats: " + JsonConvert.SerializeObject(jobResponse.DeviceJobStatistics, Formatting.Indented));
        }

        private static async Task<BlobInfo> PrepareSampleBlobForDeployAsync(string localFile, string remoteDownloadPath)
        {
            string blobName = System.IO.Path.GetFileName(localFile);
            string blobDownloadPath = $"{remoteDownloadPath}/{blobName}";

            ILogger<AzureStorage> storageLogger = _loggerFactory.CreateLogger<AzureStorage>();
            AzureStorage azureStorageHelper = new(storageLogger);
            var stroageBlobContainerUri = new Uri(_stroageBlobContainerUrl);
            await azureStorageHelper.UploadBlobToStorageAsync(_stroageAccountName, _storageKey, stroageBlobContainerUri, localFile, blobName);
            var blobSasUrl = azureStorageHelper.GetBlobDownloadUri(_stroageAccountName, _storageKey, stroageBlobContainerUri, blobName, TimeSpan.FromMinutes(10));


            BlobInfo blobInfo = new()
            {
                Name = blobName,
                SasUrl = blobSasUrl.ToString(),
                DownloadPath = blobDownloadPath
            };
            return blobInfo;
        }
        private static void PrintResponseInfo(BlobResponseInfo response)
        {
            Console.WriteLine($"Blob: {response.BlobName}.");
            Console.WriteLine($"Blob Downloaded Remotely: { response.BlobDownloaded }.");
            Console.WriteLine($"Reason: {response.Reason}");
        }

        private static void SetupLoggerFactory()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
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
            _ioTHubConnectionString = GetConfigValue("IOTHUB_CONNECTIONSTRING", "This tool requires a <IOTHUB_CONNECTIONSTRING> parameter to connect to IoT Hub to initiate the Device Stream connection.");
            _iotEdgeDeviceId = GetConfigValue("IOT_EDGE_DEVICE_ID", "This tool requires a <IOT_EDGE_DEVICE_ID> parameter as target to connect with IoT Hub Device Stream.");
            _deployBlobsModuleName = GetConfigValue("DEPLOY_BLOBS_MODULE_NAME", "This tool requires a <DEPLOY_BLOBS_MODULE_NAME> parameter as target to connect with IoT Hub Device Stream.");
            _stroageAccountName = GetConfigValue("STORAGE_ACCOUNT_NAME", "This tool requires a <STORAGE_ACCOUNT_NAME> parameter to connect to Stroage Account.");
            _storageKey = GetConfigValue("STORAGE_KEY", "This tool requires a <STORAGE_KEY> parameter to connect to Stroage Account.");
            _stroageBlobContainerUrl = GetConfigValue("STORAGE_BLOB_CONTAINER_URL", "This tool requires a <STORAGE_BLOB_CONTAINER_URL> parameter reppresenting the storage blob container URL. Like: https://<yourstroage>.blob.core.windows.net/<yourcontainer>");
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
