using IoTEdgeDeployBlobs.Sdk;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IoTEdgeDeployBlobs.Module
{
    class Program
    {

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            await ioTHubModuleClient.OpenAsync();
            ioTHubModuleClient.ProductInfo = "DeployBlobs v<TO_BE_DEFINED>";

            Console.WriteLine(new String('-', 40));
            Console.WriteLine($"Starting Module {ioTHubModuleClient.ProductInfo}");
            Console.WriteLine(new String('-', 40));
            Console.WriteLine("Ensure you have a proper ContainerCreateOptions to map a Host persistent storage path with the DeployBlob target download path,");
            Console.WriteLine("for example to map the host path '/etc/iotedge/deployBlobs' to '/app/blobs' container path, specify the HostConfig in ContainerCreateOptions:");
            Console.WriteLine("{\r\n    \"HostConfig\": {\r\n        \"Binds\": [\r\n            \"/etc/iotedge/deployBlobs:/app/blobs\"\r\n        ]\r\n    }\r\n}");
            Console.WriteLine("\r\nEnsure too Host path is created with the correct permissions to allow iotedge to access it:");
            Console.WriteLine("\tsudo mkdir /etc/iotedge/deployBlobs");
            Console.WriteLine("\tsudo chown 1000 /etc/iotedge/deployBlobs");
            Console.WriteLine("\tsudo chmod 700 /etc/iotedge/deployBlobs");
            Console.WriteLine("\r\nThen, in the Direct Method request, ensure the BlobRemotePath is set using the '/app/blobs' path, for example '/app/blobs/myModule'");
            Console.WriteLine("\r\nFinally, to allow an existing module 'myModule' to access the downloaded files, ensure that you setup another HostConfig binding mapping the Host path. In this example:");
            Console.WriteLine("{\r\n    \"HostConfig\": {\r\n        \"Binds\": [\r\n            \"/etc/iotedge/deployBlobs/myModule:/myBlobs\"\r\n        ]\r\n    }\r\n}");
            Console.WriteLine("\r\nWith this, 'myModule' will be able to access the downloaded blobs by accessing to the local container path '/myBlobs' ");

            Console.WriteLine(new String('-', 40));
            Console.WriteLine("IoT Hub module client initialized.");
            Console.WriteLine(new String('-', 40));
            Console.WriteLine(new String('#', 40));
            Console.WriteLine();

            await ioTHubModuleClient.SetMethodHandlerAsync(
                        DownloadBlobsDirectMethod.DownloadBlobMethodName, //the name of the Direct Method 
                        DownloadBlobsDirectMethod.DownloadBlobs,  //the Direct Method handler code
                        ioTHubModuleClient);

            Console.WriteLine($"\r\nDirect Method {DownloadBlobsDirectMethod.DownloadBlobMethodName} registered.");

            Console.WriteLine("Ready to receive download requests. Waiting DirectMethod calls to Initiate Blob Download.");
            Console.WriteLine();
        }
    }
}
