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
}
