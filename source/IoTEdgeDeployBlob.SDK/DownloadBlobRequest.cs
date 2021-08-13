using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlob.SDK
{
    public class DownloadBlobRequest
    {
        /// <summary>
        /// ctor
        /// </summary>
        public DownloadBlobRequest()
        {
            this.Blobs = new List<BlobInfo>();
        }

        public List<BlobInfo> Blobs { get; set; }

        /// <summary>
        /// static ctor
        /// </summary>
        /// <param name="dataAsJson"></param>
        /// <returns></returns>
        public static DownloadBlobRequest FromJson(string dataAsJson)
        {
            DownloadBlobRequest instance = JsonConvert.DeserializeObject<DownloadBlobRequest>(dataAsJson);

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
        public string BlobSASUrl { get; set; }

        /// <summary>
        /// The local path where to store the file
        /// </summary>
        public string BlobRemotePath { get; set; }

        /// <summary>
        /// The Name of the blob...  just for reference
        /// </summary>
        public object BlobName { get; set; }
    }
}
