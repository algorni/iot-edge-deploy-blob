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
        public bool BlobDownloaded { get; set; }

        public string Reason { get; set; }

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
}
