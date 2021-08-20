using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlobs.Sdk
{
    public class BlobInfo
    {
        /// <summary>
        /// The Blob SAS Url
        /// </summary>
        public string SasUrl { get; set; }
        /// <summary>
        /// The local path (inside the DeployBlobModule) where to store the file
        /// </summary>
        public string DownloadPath { get; set; }
        /// <summary>
        /// The name of the blob
        /// </summary>
        public string Name { get; set; }
    }
}
