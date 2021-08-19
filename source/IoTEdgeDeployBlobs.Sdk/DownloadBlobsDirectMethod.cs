using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlobs.Sdk
{    
    public class DownloadBlobsDirectMethod
    {
        private static HttpClient httpClient = new HttpClient();

        /// <summary>
        /// The name of the Direct Method to initiate to listen for a Device Stream request from the device side.
        /// </summary>
        public const string DownloadBlobMethodName = "DownloadBlob";


        public static async Task<MethodResponse> Execute(MethodRequest methodRequest, object parameter)
        {
            Console.WriteLine();
            Console.WriteLine(new String('-', 40));

            var moduleClient = parameter as ModuleClient;

            var directMethodRequestJson = methodRequest.DataAsJson;

            Console.WriteLine($"Deserializing DirectMethod request: {directMethodRequestJson}");

            DownloadBlobsRequest downloadBlobRequest = DownloadBlobsRequest.FromJson(directMethodRequestJson);
            
            Console.WriteLine("Deserialization done.");

            DownloadBlobsResponse downloadBlobResponse = new DownloadBlobsResponse();

            foreach (var blob in downloadBlobRequest.Blobs)
            {
                BlobResponseInfo blobResponseInfo = new BlobResponseInfo() { BlobName = blob.Name };
                
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

            Console.WriteLine(new String('=', 40));

            return new MethodResponse(downloadBlobResponse.GetJsonByte(), 200);
        }


        private static async Task downloadBlobFromStroageAccount(BlobInfo blobInfo)
        {
            Console.WriteLine($"Downloading blob {blobInfo.Name}.");

            var httpResponse = await httpClient.GetAsync(blobInfo.SasUrl);

            if (httpResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Download of blob {blobInfo.Name} done. Now saving into {blobInfo.DownloadPath}");

                ///TODO: optimize using stream instead of byte array!!!

                var blobContentByte = await httpResponse.Content.ReadAsByteArrayAsync();

                await File.WriteAllBytesAsync(blobInfo.DownloadPath, blobContentByte);

                Console.WriteLine($"Saving of blob {blobInfo.Name} done in {blobInfo.DownloadPath}!");
            }
            else
            {
                throw new ApplicationException($"An error occurred while downliading the blob {blobInfo.Name}.  HttpStatus: {httpResponse.StatusCode} ");
            }
        }
    }
}