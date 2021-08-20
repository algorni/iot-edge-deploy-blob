using Microsoft.Azure.Devices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;

namespace IoTEdgeDeployBlobs.Sdk
{
    public class DeployBlobs
    {
        private readonly string _deployBlobsModuleName;
        private readonly ServiceClient _serviceClient;
        private readonly RegistryManager _registryManager;
        private readonly ILogger _logger;
        private readonly JobClient _jobClient;

        public DeployBlobs(string iotHubConnectionString, string deployBlobsModuleName, ILogger logger = null)
        {
            _deployBlobsModuleName = deployBlobsModuleName;
            _logger = logger;
            _serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString, TransportType.Amqp);
            _registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            _jobClient = JobClient.CreateFromConnectionString(iotHubConnectionString);

            _logger?.LogInformation("Module dependencies Service Client, RegistryManager and JobClient ready.");
        }

        public async Task<DownloadBlobsResponse> DeployBlobsAsync(string targetEdgeDeviceId, IEnumerable<BlobInfo> blobs)
        {
            CloudToDeviceMethod methodRequest = PrepareDownloadBlobsDirectMethod(blobs);

            //perform a Direct Method to the remote device to initiate the device stream!
            CloudToDeviceMethodResult response = await _serviceClient.InvokeDeviceMethodAsync(targetEdgeDeviceId, _deployBlobsModuleName, methodRequest);

            DownloadBlobsResponse responseObj = DownloadBlobsResponse.FromJson(response.GetPayloadAsJson());

            return responseObj;
        }

        /// <summary>
        /// Deploy the list of blobs to the IoT Edge Devices executing the deployBlobs module and the query condition. Returns a JobId that can be used to monitor the JobStatus.
        /// </summary>
        /// <param name="blobs">List of blobs to be downloaded by the deployBlob module at the target devices.</param>
        /// <param name="queryCondition">Query condition to filter target devices based on the 'devices.modules' schema.</param>
        /// <returns></returns>
        public async Task<JobResponse> ScheduleDeployBlobsJobAsync(List<BlobInfo> blobs, string queryCondition = "")
        {
            string jobId = Guid.NewGuid().ToString();

            var downloadMethod = PrepareDownloadBlobsDirectMethod(blobs);

            string deployBlobsModuleCondition = $"FROM devices.modules WHERE devices.modules.moduleId = '{_deployBlobsModuleName}'";
            if (!String.IsNullOrWhiteSpace(queryCondition))
            {
                deployBlobsModuleCondition = $"{ deployBlobsModuleCondition } AND ({queryCondition})";
            }
          
            return await StartDownloadJobAsync(jobId, deployBlobsModuleCondition, downloadMethod);
        }

        /// <summary>
        /// Deploy the list of blobs to the IoT Edge Devices executing the deployBlobs module and matching the deviceIds list. Returns a JobId that can be used to monitor the JobStatus.
        /// </summary>
        /// <param name="blobs">List of blobs to be downloaded by the deployBlob module at the target devices.</param>
        /// <param name="devicesIds">List of target devices.</param>
        /// <returns></returns>
        public async Task<JobResponse> ScheduleDeployBlobsJobAsync(List<BlobInfo> blobs, IEnumerable<string> devicesIds)
        {
            if (devicesIds is null || !devicesIds.Any())
            {
                return null;
            }

            string queryCondition = $"deviceId IN [ '{String.Join("', '", devicesIds)}' ]";
            return await ScheduleDeployBlobsJobAsync(blobs, queryCondition);
        }


        /// <summary>
        /// Gets the current status for the scheduled job matching the jobId
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public async Task<string> GetDeployBlobsJobStatusAsync(string jobId)
        {
            JobResponse result = await _jobClient.GetJobAsync(jobId);
            return result?.Status.ToString();
        }

        /// <summary>
        /// Gets all the deatils for the scheduled job matching the jobId
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public async Task<JobResponse> GetDeploymentJobAsync(string jobId)
        {
            return await _jobClient.GetJobAsync(jobId);
        }

        /// <summary>
        /// Gets all the reponses from all targeted devices by the scheduled job matching the jobId
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="queryCondition">Optionally, a query condition over the devices.jobs schema can be provided</param>
        /// <returns></returns>
        public async Task<IEnumerable<DeviceJob>> GetDeploymentJobResponsesAsync(string jobId, string queryCondition="")
        {
            List<DeviceJob> responses = new();
            string queryStr = $"SELECT * FROM devices.jobs where jobId = '{jobId}'";
            if (!String.IsNullOrEmpty(queryCondition))
            {
                queryStr += $" and ({queryCondition})";
            }

            _logger.LogInformation($"Querying job reponses: {queryStr}");
            var query = _registryManager.CreateQuery(queryStr);
            while (query.HasMoreResults)
            {
                var response = await query.GetNextAsDeviceJobAsync();
                responses.AddRange(response);
            }
            _logger.LogInformation($"Found  {responses.Count} reponses");
            return responses;
        }

        /// <summary>
        /// Gets all the edge devices Ids.
        /// </summary>
        /// <param name="queryCondition">Optionally, a query condition over the devices schema can be provide to filter the retrieved Ids</param>
        /// <returns></returns>
        public async Task<IEnumerable<string>> GetEdgeDevicesIdsAsync(string queryCondition = "")
        {
            List<string> ids = new();
            string queryStr = $"SELECT * FROM devices where capabilities.iotEdge = true";
            if (!String.IsNullOrEmpty(queryCondition))
            {
                queryStr += $" and ( {queryCondition} )";
            }

            _logger.LogInformation($"Gathering Edge DevicesIds: {queryStr}");
            var query = _registryManager.CreateQuery(queryStr);
            while (query.HasMoreResults)
            {
                var response = await query.GetNextAsTwinAsync();
                ids.AddRange(response.Select(dv => dv.DeviceId));
            }
            return ids;
        }

        private static CloudToDeviceMethod PrepareDownloadBlobsDirectMethod(IEnumerable<BlobInfo> blobs)
        {
            DownloadBlobsRequest downloadBlobRequest = new();
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
                (long)TimeSpan.FromMinutes(3).TotalSeconds);

            _logger?.LogInformation($"Scheduled jobId {jobId} targeting '{queryCondition}'. Job Timeout 3 minutes.");
            return result;
        }
    }
}
