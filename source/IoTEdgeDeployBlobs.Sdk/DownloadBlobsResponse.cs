using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlobs.Sdk
{
    public class DownloadBlobsResponse
    {
        /// <summary>
        /// ctor
        /// </summary>
        public DownloadBlobsResponse()
        {
            this.Blobs = new List<BlobResponseInfo> ();
        }

        public List<BlobResponseInfo> Blobs { get; set; }

        public byte[] GetJsonByte()
        {
            var jsonRepp = JsonConvert.SerializeObject(this, Formatting.Indented);

            byte[] bytes = Encoding.UTF8.GetBytes(jsonRepp);

            return bytes;
        }

        public static DownloadBlobsResponse FromJson(string dataAsJson)
        {
            DownloadBlobsResponse instance = JsonConvert.DeserializeObject<DownloadBlobsResponse>(dataAsJson);

            return instance;
        }
    }
}
