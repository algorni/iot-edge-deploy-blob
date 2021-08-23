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
        private static readonly HttpClient _httpClient = new();

        /// <summary>
        /// The name of the Direct Method to initiate to listen for a Device Stream request from the device side.
        /// </summary>
        public const string DownloadBlobMethodName = "DownloadBlobs";


        public static async Task<MethodResponse> DownloadBlobs(MethodRequest methodRequest, object objectContext)
        {
            Console.WriteLine(new String('-', 40));

            var moduleClient = objectContext as ModuleClient;
            Console.WriteLine(moduleClient.ProductInfo);

            var directMethodRequestJson = methodRequest.DataAsJson;

            Console.WriteLine($"New DownloadBlobs request: {directMethodRequestJson}");

            DownloadBlobsRequest downloadBlobRequest = DownloadBlobsRequest.FromJson(directMethodRequestJson);

            DownloadBlobsResponse downloadBlobResponse = new();

            foreach (var blob in downloadBlobRequest.Blobs)
            {
                BlobResponseInfo blobResponseInfo = new() { BlobName = blob.Name };
                
                try
                {
                    await DownloadBlobsFromStroageAccountAsync(blob);

                    blobResponseInfo.BlobDownloaded = true;
                    var reason = $"Successfully downloaded {blob.Name} to {blob.DownloadPath}";
                    blobResponseInfo.Reason = reason;
                    Console.WriteLine(reason);
                }
                catch (Exception ex)
                {
                    blobResponseInfo.BlobDownloaded = false;
                    blobResponseInfo.Reason = $"Exception downloading to {blob.DownloadPath}. Message: {ex.Message}";
                    blobResponseInfo.Exception = ex;

                    Console.WriteLine($"Error downloading {blob.Name} to {blob.DownloadPath}.\n{ex}");
                }

                downloadBlobResponse.Blobs.Add(blobResponseInfo);
            }

            Console.WriteLine(new String('=', 40));

            return new MethodResponse(downloadBlobResponse.GetJsonByte(), 200);
        }


        private static async Task DownloadBlobsFromStroageAccountAsync(BlobInfo blobInfo)
        {
            var httpResponse = await _httpClient.GetAsync(blobInfo.SasUrl);

            if (httpResponse.IsSuccessStatusCode)
            {
                using var contentStream = await httpResponse.Content.ReadAsStreamAsync();
                FileStream fs = File.Create(blobInfo.DownloadPath, 1024, FileOptions.Asynchronous);
                await contentStream.CopyToAsync(fs);
                fs.Close();
            }
            else
            {
                throw new Exception($"Error downloading {blobInfo.Name} from {blobInfo.SasUrl}.  Http Status: {httpResponse.StatusCode}. Reason: {httpResponse.ReasonPhrase}.");
            }
        }
    }
}