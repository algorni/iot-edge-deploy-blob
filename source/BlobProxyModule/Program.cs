using IoTEdgeDeployBlobs.SDK;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlobProxyModule
{
    class Program
    {
        static int counter;

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
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            
            await ioTHubModuleClient.OpenAsync();

            Console.WriteLine(new String('-', 40));
            Console.WriteLine("Starting BlobProxyModule");
            Console.WriteLine(new String('-', 40));
            Console.WriteLine("Ensure you have a proper ContainerCreateOptions to map a Host storage path with the BlobProxyModule target download path,");
            Console.WriteLine("for example to map the host path '/etc/iotedge/blobsProxy' to '/blobsDownloads' container path, specify the HostConfig in ContainerCreateOptions as follows:");
            Console.WriteLine("{\r\n    \"HostConfig\": {\r\n        \"Binds\": [\r\n            \"/etc/iotedge/blobsProxy:/blobsDownloads\"\r\n        ]\r\n    }\r\n}");
            Console.WriteLine("\r\nEnsure too Host path is created with the correct permissions to allow iotedge to access it:");
            Console.WriteLine("\tsudo mkdir /etc/iotedge/blobsProxy");
            Console.WriteLine("\tsudo chown 1000 /etc/iotedge/blobsProxy");
            Console.WriteLine("\tsudo chmod 700 /etc/iotedge/blobsProxy");
            Console.WriteLine("\r\nThen, in the Direct Method request, ensure the BlobRemotePath is set using the '/blobsDownloads' path, for example '/blobsDownloads/myModule'");
            Console.WriteLine("\r\nFinally, to allow an existing module 'myModule' to access the downloaded files, ensure that you setup another HostConfig binding mapping the Host path. In this example:");
            Console.WriteLine("{\r\n    \"HostConfig\": {\r\n        \"Binds\": [\r\n            \"/etc/iotedge/blobsProxy/myModule:/myBlobs\"\r\n        ]\r\n    }\r\n}");
            Console.WriteLine("\r\nWith this, 'myModule' will be able to access the downloaded blobs by accessing to the local container path 'myBlobs' ");

            Console.WriteLine(new String('-', 40));
            Console.WriteLine("IoT Hub module client initialized.");
            Console.WriteLine(new String('-', 40));
            Console.WriteLine(new String('#', 40));
            Console.WriteLine();

            await ioTHubModuleClient.SetMethodHandlerAsync(
                        DownloadBlobsDirectMethod.DownloadBlobMethodName, //the name of the Direct Method 
                        new MethodCallback(DownloadBlobsDirectMethod.Execute),  //the Direct Method handler code
                        ioTHubModuleClient);

            Console.WriteLine($"\r\nDirect Method {DownloadBlobsDirectMethod.DownloadBlobMethodName} registered.");

            Console.WriteLine("Ready to receive download requests. Waiting DirectMethod calls to Initiate Blob Download.");
        }
    }
}