using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlob.Storage
{
    public class AzureStorageHelper
    {
        private ILogger<AzureStorageHelper> _logger;

        public AzureStorageHelper(ILogger<AzureStorageHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Upload a Blob to the Storage Account
        /// </summary>
        /// <param name="blobContainerUri"></param>
        /// <param name="blobPath"></param>
        /// <returns></returns>
        public async Task UploadBlobToStorage(Uri blobContainerUri, string blobPath, string blobName)
        {
            DefaultAzureCredential defaultAzureCredential = new DefaultAzureCredential();

            BlobContainerClient container = new BlobContainerClient(blobContainerUri, defaultAzureCredential);

            await UploadBlobToStorage(container, blobPath, blobName);
        }

        /// <summary>
        /// Upload a Blob to the Storage Account
        /// </summary>
        /// <param name="connectinString"></param>
        /// <param name="blobContainerName"></param>
        /// <param name="blobPath"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public async Task UploadBlobToStorage(string connectinString, string blobContainerName, string blobPath, string blobName)
        {
            DefaultAzureCredential defaultAzureCredential = new DefaultAzureCredential();

            BlobContainerClient container = new BlobContainerClient(connectinString, blobContainerName);

            await UploadBlobToStorage(container, blobPath, blobName);
        }



        /// <summary>
        /// Upload a Blob to the Storage Account
        /// </summary>
        /// <param name="containerClient"></param>
        /// <param name="blobPath"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public async Task UploadBlobToStorage(BlobContainerClient containerClient, string blobPath, string blobName)
        {
            logInfo($"Uploading blob {blobName} from {blobPath}");

            // Get a reference to a blob named {blobName}
            BlobClient blob = containerClient.GetBlobClient(blobName);

            var fileStream = File.OpenRead(blobPath);

            // First upload something the blob so we have something to download
            await blob.UploadAsync(fileStream);           
        }


        /// <summary>
        /// Get Blob SAS URI
        /// </summary>
        /// <param name="containerClient"></param>
        /// <param name="container"></param>
        /// <param name="blobName"></param>
        /// <param name="expireIn"></param>
        /// <returns></returns>
        public Uri GetBlobDownladUri(BlobContainerClient containerClient, string container, string blobName, TimeSpan expireIn)
        {
            var blobClient = containerClient.GetBlobClient(blobName);

            if (blobClient.CanGenerateSasUri)
            {
                return blobClient.GenerateSasUri(new BlobSasBuilder(BlobSasPermissions.Read, DateTime.UtcNow + expireIn));
            }
            else
            {
                var message = $"Cannot generate a SAS Uri for blob {blobName}";

                logError(message);

                throw new ApplicationException(message);
            }                
        }




        private void logInfo(string message)
        {
            if (_logger != null)
                _logger.LogInformation(message);
        }

        private void logError(string message)
        {
            if (_logger != null)
                _logger.LogError(message);
        }

    }
}
