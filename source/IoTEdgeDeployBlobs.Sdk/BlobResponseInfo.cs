using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlobs.Sdk
{
    public class BlobResponseInfo
    {
        public string BlobName { get; set; }
        public bool BlobDownloaded { get; set; }
        public string Reason { get; set; }
        public Exception Exception { get; set; }
    }
}
