using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlobs.Sdk
{
    public class DownloadBlobsRequest
    {
        /// <summary>
        /// ctor
        /// </summary>
        public DownloadBlobsRequest()
        {
            this.Blobs = new List<BlobInfo>();
        }

        public List<BlobInfo> Blobs { get; set; }

        /// <summary>
        /// static ctor
        /// </summary>
        /// <param name="dataAsJson"></param>
        /// <returns></returns>
        public static DownloadBlobsRequest FromJson(string dataAsJson)
        {
            DownloadBlobsRequest instance = JsonConvert.DeserializeObject<DownloadBlobsRequest>(dataAsJson);

            return instance;
        }

        public string ToJson()
        {
            var jsonRepp = JsonConvert.SerializeObject(this, Formatting.Indented);

            return jsonRepp;
        }
    }

    public class BlobInfo
    {
        /// <summary>
        /// The Blob SAS Url
        /// </summary>
        public string SasUrl { get; set; }

        /// <summary>
        /// The local path where to store the file
        /// </summary>
        public string DownloadPath { get; set; }

        /// <summary>
        /// The Name of the blob...  just for reference
        /// </summary>
        public object Name { get; set; }
    }
}
