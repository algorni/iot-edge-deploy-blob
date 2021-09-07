using Azure.Core;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlobs.Sdk
{
    public class AzureStorage
    {
        private readonly ILogger<AzureStorage> _logger;

        public AzureStorage(ILogger<AzureStorage> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Upload a Blob to the Storage Account
        /// </summary>
        /// <param name="blobContainerUri"></param>
        /// <param name="blobLocalPath"></param>
        /// <returns></returns>
        public async Task UploadBlobToStorageAsync(TokenCredential tokenCredential, Uri blobContainerUri, string blobLocalPath, string blobName)
        {   
            BlobContainerClient blobContainerClient = new(blobContainerUri, tokenCredential);

            await UploadBlobToStorageAsync(blobContainerClient, blobLocalPath, blobName);
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
        public async Task UploadBlobToStorageAsync(string accountName, string accountKey, Uri blobContainerUri, string blobLocalPath, string blobName)
        {
            // Create a SharedKeyCredential that we can use to authenticate
            StorageSharedKeyCredential credential = new(accountName, accountKey);

            //Blob Container client using Sas credential
            BlobContainerClient blobContainerClient = new(blobContainerUri, credential); 

            await UploadBlobToStorageAsync(blobContainerClient, blobLocalPath, blobName);
        }


        /// <summary>
        /// Upload a Blob to the Storage Account
        /// </summary>
        /// <param name="blobContainerClient"></param>
        /// <param name="blobLocalPath"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public async Task UploadBlobToStorageAsync(BlobContainerClient blobContainerClient, string blobLocalPath, string blobName)
        {
            _logger?.LogInformation($"Uploading blob {blobName} from {blobLocalPath}");

            // Get a reference to a blob named {blobName}
            BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);

            var fileStream = File.OpenRead(blobLocalPath);

            // First upload something the blob so we have something to download
            await blobClient.UploadAsync(fileStream, overwrite: true);           
        }

        /// <summary>
        /// Get Blob SAS URI
        /// </summary>
        /// <param name="blobContainerUri"></param>
        /// <param name="blobName"></param>
        /// <param name="expireIn"></param>
        /// <returns></returns>
        public Uri GetBlobDownloadUri(TokenCredential tokenCredential, Uri blobContainerUri, string blobName, TimeSpan expireIn)
        {  
            BlobContainerClient blobContainerClient = new(blobContainerUri, tokenCredential);

            return GetBlobDownloadUri(blobContainerClient, blobName, expireIn);
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
        public Uri GetBlobDownloadUri(string accountName, string accountKey, Uri blobContainerUri, string blobName, TimeSpan expireIn)
        {
            // Create a SharedKeyCredential that we can use to authenticate
            StorageSharedKeyCredential credential = new(accountName, accountKey);
                      
            //Blob Container client using Sas credential
            BlobContainerClient blobContainerClient = new(blobContainerUri, credential);


            return GetBlobDownloadUri(blobContainerClient, blobName, expireIn);
        }


        /// <summary>
        /// Get Blob SAS URI
        /// </summary>
        /// <param name="blobContainerClient"></param>
        /// <param name="container"></param>
        /// <param name="blobName"></param>
        /// <param name="expireIn"></param>
        /// <returns></returns>
        public Uri GetBlobDownloadUri(BlobContainerClient blobContainerClient, string blobName, TimeSpan expireIn)
        {            
            var blobClient = blobContainerClient.GetBlobClient(blobName);

            if (blobClient.CanGenerateSasUri)
            {
                return blobClient.GenerateSasUri(new BlobSasBuilder(BlobSasPermissions.Read, DateTime.UtcNow + expireIn));
            }
            else
            {
                var message = $"Cannot generate a SAS Uri for blob {blobName}";

                _logger?.LogError(message);

                throw new ApplicationException(message);
            }                
        }
    }
}
