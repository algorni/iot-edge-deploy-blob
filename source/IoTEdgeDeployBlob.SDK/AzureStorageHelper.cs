using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlob.SDK
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
        /// <param name="blobLocalPath"></param>
        /// <returns></returns>
        public async Task UploadBlobToStorage(Uri blobContainerUri, string blobLocalPath, string blobName)
        {
            DefaultAzureCredential defaultAzureCredential = new DefaultAzureCredential();

            BlobContainerClient blobContainerClient = new BlobContainerClient(blobContainerUri, defaultAzureCredential);

            await UploadBlobToStorage(blobContainerClient, blobLocalPath, blobName);
        }
        

        /// <summary>
        /// Upload a Blob to the Storage Account
        /// </summary>
        /// <param name="accountName"></param>
        /// <param name="accountKey"></param>
        /// <param name="serviceUri"></param>
        /// <param name="blobContainerName"></param>
        /// <param name="blobLocalPath"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public async Task UploadBlobToStorage(string accountName, string accountKey, Uri blobContainerUri, string blobLocalPath, string blobName)
        {
            // Create a SharedKeyCredential that we can use to authenticate
            StorageSharedKeyCredential credential = new StorageSharedKeyCredential(accountName, accountKey);

            //Blob Container client using Sas credential
            BlobContainerClient blobContainerClient = new BlobContainerClient(blobContainerUri, credential); 

            await UploadBlobToStorage(blobContainerClient, blobLocalPath, blobName);
        }





        /// <summary>
        /// Upload a Blob to the Storage Account
        /// </summary>
        /// <param name="blobContainerClient"></param>
        /// <param name="blobLocalPath"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public async Task UploadBlobToStorage(BlobContainerClient blobContainerClient, string blobLocalPath, string blobName)
        {
            logInfo($"Uploading blob {blobName} from {blobLocalPath}");

            // Get a reference to a blob named {blobName}
            BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);

            var fileStream = File.OpenRead(blobLocalPath);

            // First upload something the blob so we have something to download
            await blobClient.UploadAsync(fileStream);           
        }







        /// <summary>
        /// Get Blob SAS URI
        /// </summary>
        /// <param name="blobContainerUri"></param>
        /// <param name="blobName"></param>
        /// <param name="expireIn"></param>
        /// <returns></returns>
        public Uri GetBlobDownladUri(Uri blobContainerUri, string blobName, TimeSpan expireIn)
        {
            DefaultAzureCredential defaultAzureCredential = new DefaultAzureCredential();

            BlobContainerClient blobContainerClient = new BlobContainerClient(blobContainerUri, defaultAzureCredential);

            return GetBlobDownladUri(blobContainerClient, blobName, expireIn);
        }



        /// <summary>
        /// Get Blob SAS URI
        /// </summary>
        /// <param name="accountName"></param>
        /// <param name="accountKey"></param>
        /// <param name="serviceUri"></param>
        /// <param name="container"></param>
        /// <param name="blobName"></param>
        /// <param name="expireIn"></param>
        /// <returns></returns>
        public Uri GetBlobDownladUri(string accountName, string accountKey, Uri blobContainerUri, string blobName, TimeSpan expireIn)
        {
            // Create a SharedKeyCredential that we can use to authenticate
            StorageSharedKeyCredential credential = new StorageSharedKeyCredential(accountName, accountKey);
                      
            //Blob Container client using Sas credential
            BlobContainerClient blobContainerClient = new BlobContainerClient(blobContainerUri, credential);


            return GetBlobDownladUri(blobContainerClient, blobName, expireIn);
        }


        /// <summary>
        /// Get Blob SAS URI
        /// </summary>
        /// <param name="blobContainerClient"></param>
        /// <param name="container"></param>
        /// <param name="blobName"></param>
        /// <param name="expireIn"></param>
        /// <returns></returns>
        public Uri GetBlobDownladUri(BlobContainerClient blobContainerClient, string blobName, TimeSpan expireIn)
        {            
            var blobClient = blobContainerClient.GetBlobClient(blobName);

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
