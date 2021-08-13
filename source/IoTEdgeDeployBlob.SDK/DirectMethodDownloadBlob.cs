using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlob.SDK
{    
    public class DirectMethodDownloadBlob
    {
        private static HttpClient httpClient = new HttpClient();

        /// <summary>
        /// The name of the Direct Method to initiate to listen for a Device Stream request from the device side.
        /// </summary>
        public const string DownloadBlob = "DownloadBlob";


        public static async Task<MethodResponse> Execute(MethodRequest methodRequest, object parameter)
        {
            Console.WriteLine("Download Blob Direct Method initiate.");

            var moduleClient = parameter as ModuleClient;

            var directMethodRequestJson = methodRequest.DataAsJson;

            Console.WriteLine($"Deserializing DirectMethod request: {directMethodRequestJson}");

            DownloadBlobRequest downloadBlobRequest = DownloadBlobRequest.FromJson(directMethodRequestJson);
            
            Console.WriteLine("Deserialization done.");

            DownloadBlobResponse downloadBlobResponse = new DownloadBlobResponse();

            foreach (var blob in downloadBlobRequest.Blobs)
            {
                BlobResponseInfo blobResponseInfo = new BlobResponseInfo() { BlobName = blob.BlobName };
                
                try
                {
                    await downloadBlobFromStroageAccount(blob);

                    blobResponseInfo.BlobDownloaded = true;
                }
                catch (Exception ex)
                {
                    blobResponseInfo.BlobDownloaded = false;
                    blobResponseInfo.Reason = ex.ToString();

                    Console.WriteLine($"There was an error while downloading the Blob\n{ex.ToString()}");
                }

                downloadBlobResponse.Blobs.Add(blobResponseInfo);
            }
            
            return new MethodResponse(downloadBlobResponse.GetJsonByte(), 200);
        }

        private static async Task downloadBlobFromStroageAccount(BlobInfo blobInfo)
        {
            Console.WriteLine($"Downlading blob {blobInfo.BlobName}.");

            var httpResponse = await httpClient.GetAsync(blobInfo.BlobSASUrl);

            if (httpResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Downlad of blob {blobInfo.BlobName} done. Now saving into {blobInfo.BlobRemotePath}");

                ///TODO: optimize using stream instead of byte array!!!

                var blobContentByte = await httpResponse.Content.ReadAsByteArrayAsync();

                await File.WriteAllBytesAsync(blobInfo.BlobRemotePath, blobContentByte);

                Console.WriteLine($"Saving of blob {blobInfo.BlobName} done in {blobInfo.BlobRemotePath}!");
            }
            else
            {
                throw new ApplicationException($"An error occurred while downliading the blob {blobInfo.BlobName}.  HttpStatus: {httpResponse.StatusCode} ");
            }
        }
    }
}