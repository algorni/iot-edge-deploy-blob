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

            try
            {
                await downloadBlobFromStroageAccount(downloadBlobRequest);
                                
                downloadBlobResponse.BlobDownloaded = true;

                return new MethodResponse(downloadBlobResponse.GetJsonByte(), 200);
            }
            catch (Exception ex)
            {
                downloadBlobResponse.BlobDownloaded = false;
                downloadBlobResponse.Reason = ex.ToString();
                
                Console.WriteLine($"There was an error while downloading the Blob\n{ex.ToString()}");

                return new MethodResponse(downloadBlobResponse.GetJsonByte(), 500);
            }                     
        }

        private static async Task downloadBlobFromStroageAccount(DownloadBlobRequest downloadBlobRequest)
        {
            Console.WriteLine($"Downlading blob {downloadBlobRequest.BlobName}.");

            var httpResponse = await httpClient.GetAsync(downloadBlobRequest.BlobSASUrl);

            if (httpResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Downlad of blob {downloadBlobRequest.BlobName} done. Now saving into {downloadBlobRequest.BlobRemotePath}");

                ///TODO: optimize using stream instead of byte array!!!

                var blobContentByte = await httpResponse.Content.ReadAsByteArrayAsync();

                await File.WriteAllBytesAsync(downloadBlobRequest.BlobRemotePath, blobContentByte);

                Console.WriteLine($"Saving of blob {downloadBlobRequest.BlobName} done in {downloadBlobRequest.BlobRemotePath}!");
            }
            else
            {
                throw new ApplicationException($"An error occurred while downliading the blob {downloadBlobRequest.BlobName}.  HttpStatus: {httpResponse.StatusCode} ");
            }
        }
    }
}