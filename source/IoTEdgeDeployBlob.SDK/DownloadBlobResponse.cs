using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlob.SDK
{
    public class DownloadBlobResponse
    {
        /// <summary>
        /// ctor
        /// </summary>
        public DownloadBlobResponse()
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

        public static DownloadBlobResponse FromJson(string dataAsJson)
        {
            DownloadBlobResponse instance = JsonConvert.DeserializeObject<DownloadBlobResponse>(dataAsJson);

            return instance;
        }
    }
    
    
    public class BlobResponseInfo
    {       
        
        public bool BlobDownloaded { get; set; }

        public string Reason { get; set; }

        /// <summary>
        /// The Name of the blob...  just for reference
        /// </summary>
        public object BlobName { get; set; }
    }
}
