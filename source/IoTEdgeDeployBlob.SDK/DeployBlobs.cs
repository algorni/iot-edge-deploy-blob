using Microsoft.Azure.Devices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlobs.SDK
{
    public class DeployBlobs
    {
        private readonly string _iotHubConnectionString;
        private readonly string _blobProxyModuleName;
        private readonly ServiceClient _serviceClient;
        private readonly RegistryManager _registryManager;
        private readonly ILogger _logger;
        private readonly JobClient _jobClient;

        public DeployBlobs(string iotHubConnectionString, string blobProxyModuleName, ILogger logger = null)
        {
            //TODO: Remove module private vars
            _iotHubConnectionString = iotHubConnectionString;
            _blobProxyModuleName = blobProxyModuleName;
            _logger = logger;
            Microsoft.Azure.Devices.TransportType transportType = Microsoft.Azure.Devices.TransportType.Amqp;
            _serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString, transportType);
            _registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            _logger?.LogInformation("ServiceClient connected created. BlobProxyJobs connected to IoT Hub");

            _jobClient = JobClient.CreateFromConnectionString(iotHubConnectionString);
        
        }
        public async Task<DownloadBlobsResponse> SingleDeviceDeploymentAsync(string targetEdgeDeviceId, IEnumerable<BlobInfo> blobs)
        {
            CloudToDeviceMethod methodRequest = PrepareDownloadBlobsDirectMethod(blobs);

            //perform a Direct Method to the remote device to initiate the device stream!
            CloudToDeviceMethodResult response = await _serviceClient.InvokeDeviceMethodAsync(targetEdgeDeviceId, _blobProxyModuleName, methodRequest);

            DownloadBlobsResponse responseObj = DownloadBlobsResponse.FromJson(response.GetPayloadAsJson());

            return responseObj;
        }

        /// <summary>
        /// Distribute the list of blobs to the devices matching the query condition. Returns a JobId that can be used to monitor the JobStatus using the MonitorJob Method.
        /// </summary>
        /// <param name="blobs">List of blobs to be downloaded</param>
        /// <param name="queryCondition">Query to filter target devices</param>
        /// <returns></returns>
        public async Task<JobResponse> ScheduleDeploymentJobAsync(List<BlobInfo> blobs, string queryCondition = "")
        {
            string jobId = Guid.NewGuid().ToString();

            var downloadMethod = PrepareDownloadBlobsDirectMethod(blobs);

            string deployBlobsModuleCondition = $"FROM devices.modules WHERE devices.modules.moduleId = '{_blobProxyModuleName}'";
            if (!String.IsNullOrWhiteSpace(queryCondition))
            {
                deployBlobsModuleCondition = $"{ deployBlobsModuleCondition } AND ({queryCondition})";
            }

            _logger?.LogInformation($"Scheduling to deploy blobs to multiple IoT Edge Devices matching the following quey: {deployBlobsModuleCondition}.");
          
            return await StartDownloadJobAsync(jobId, deployBlobsModuleCondition, downloadMethod);
        }


        /// <summary>
        /// Gets the current status string for the distribution Job
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public async Task<string> GetDeploymentJobStatusAsync(string jobId)
        {
            JobResponse result = await _jobClient.GetJobAsync(jobId);
            return result?.Status.ToString();
        }

        public async Task<JobResponse> GetDeploymentJobAsync(string jobId)
        {
            return await _jobClient.GetJobAsync(jobId);
        }

        public async Task<IEnumerable<DeviceJob>> GetDeploymentJobResponsesAsync(string jobId)
        {
            List<DeviceJob> responses = new List<DeviceJob>();
            string queryStr = $"SELECT * FROM devices.jobs where jobId = '{jobId}'";
            //if (devices != null)
            //{
            //    queryStr += $" and { GetDevicesQueryFilter(devices) }";
            //}

            var query = _registryManager.CreateQuery(queryStr);
            while (query.HasMoreResults)
            {
                var response = await query.GetNextAsDeviceJobAsync();
                responses.AddRange(response);
            }
            return responses;
        }

        private CloudToDeviceMethod PrepareDownloadBlobsDirectMethod(IEnumerable<BlobInfo> blobs)
        {
            _logger?.LogInformation("Preparing DownloadBlobs Direct Method (Cloud to Device) call...");

            DownloadBlobsRequest downloadBlobRequest = new DownloadBlobsRequest();
            downloadBlobRequest.Blobs.AddRange(blobs);

            var methodRequest = new CloudToDeviceMethod(
                    DownloadBlobsDirectMethod.DownloadBlobMethodName,
                    TimeSpan.FromSeconds(30),  //It could get a time out if the download takes more than 30 seconds
                    TimeSpan.FromSeconds(5)
            );

            methodRequest.SetPayloadJson(downloadBlobRequest.ToJson());

            return methodRequest;
        }

        private async Task<JobResponse> StartDownloadJobAsync(string jobId, string queryCondition, CloudToDeviceMethod downloadMethod)
        {
            JobResponse result = await _jobClient.ScheduleDeviceMethodAsync(jobId, 
                queryCondition, 
                downloadMethod, 
                DateTime.UtcNow, 
                (long)TimeSpan.FromMinutes(2).TotalSeconds);

            _logger?.LogInformation($"Scheduled job to download blobs on devices matching: {queryCondition}. Job Timeout 3 minutes.");
            return result;
        }
    }
}
