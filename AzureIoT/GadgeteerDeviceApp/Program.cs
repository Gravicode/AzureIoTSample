using Gadgeteer.Modules.GHIElectronics;
using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Microsoft.SPOT.Net.NetworkInformation;
using System.Text;
using Amqp;
using Amqp.Framing;
//using Microsoft.Azure.Devices.Client;
namespace System.Diagnostics
{
    public enum DebuggerBrowsableState
    {
        Never,
        Collapsed,
        RootHidden
    }
}
namespace GadgeteerDeviceApp
{
    public partial class Program
    {
        #region unused
        /*
          // String containing Hostname, Device Id & Device Key in one of the following formats:
        //  "HostName=<iothub_host_name>;DeviceId=<device_id>;SharedAccessKey=<device_key>"
        //  "HostName=<iothub_host_name>;CredentialType=SharedAccessSignature;DeviceId=<device_id>;SharedAccessSignature=SharedAccessSignature sr=<iot_host>/devices/<device_id>&sig=<token>&se=<expiry_time>";
        private const string DeviceConnectionString = "HostName=FreeDeviceHub.azure-devices.net;DeviceId=GadgeteerDevice;SharedAccessKey=NJExGHjY5U6c9P4fw0jZn26UHJI04c9Ck2cgLKfGovw=";
        static DeviceClient deviceClient;


        static void SendEvent(DataSensor item)
        {
            string dataBuffer = Json.NETMF.JsonSerializer.SerializeObject(item);
            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
            Debug.Print(DateTime.Now.ToLocalTime() + "> Sending message: " + DateTime.Now.ToString() + ", Data: [" + dataBuffer + "]");

            deviceClient.SendEvent(eventMessage);

        }

        static void ReceiveCommands()
        {
            Debug.Print("Device waiting for commands from IoTHub...");
            Message receivedMessage;
            string messageData;

            while (true)
            {
                receivedMessage = deviceClient.Receive();

                if (receivedMessage != null)
                {
                    StringBuilder sb = new StringBuilder();

                    foreach (byte b in receivedMessage.GetBytes())
                    {
                        sb.Append((char)b);
                    }

                    messageData = sb.ToString();

                    // dispose string builder
                    sb = null;

                    Debug.Print(DateTime.Now.ToLocalTime() + "> Received message: " + messageData);

                    deviceClient.Complete(receivedMessage);
                }

                //  Note: In this sample, the polling interval is set to 
                //  10 seconds to enable you to see messages as they are sent.
                //  To enable an IoT solution to scale, you should extend this //  interval. For example, to scale to 1 million devices, set 
                //  the polling interval to 25 minutes.
                //  For further information, see
                //  https://azure.microsoft.com/documentation/articles/iot-hub-devguide/#messaging
                Thread.Sleep(10000);
            }
        }

            try
            {
                deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString, TransportType.Http1);

              
            }

            catch(Exception ex) {
                Debug.Print(ex.Message + "_" + ex.StackTrace);
            };

         */
        #endregion
        private const string HOST = "FreeDeviceHub.azure-devices.net";
        private const int PORT = 5671;
        private const string DEVICE_ID = "GadgeteerDevice";
        private const string DEVICE_KEY = "NJExGHjY5U6c9P4fw0jZn26UHJI04c9Ck2cgLKfGovw=";

        private static Address address;
        private static Connection connection;
        private static Session session;

        private static Thread receiverThread;


        static private void SendEvent(DataSensor item)
        {
            var json = Json.NETMF.JsonSerializer.SerializeObject(item);
            string entity = Fx.Format("/devices/{0}/messages/events", DEVICE_ID);

            SenderLink senderLink = new SenderLink(session, "sender-link", entity);

            var messageValue = Encoding.UTF8.GetBytes(json);
            Message message = new Message()
            {
                BodySection = new Data() { Binary = messageValue }
            };

            senderLink.Send(message);
            senderLink.Close();
        }

        static private void ReceiveCommands()
        {
            string entity = Fx.Format("/devices/{0}/messages/deviceBound", DEVICE_ID);

            ReceiverLink receiveLink = new ReceiverLink(session, "receive-link", entity);

            Message received = receiveLink.Receive();
            if (received != null)
                receiveLink.Accept(received);

            receiveLink.Close();
        }

        static private bool PutCbsToken(Connection connection, string host, string shareAccessSignature, string audience)
        {
            bool result = true;
            Session session = new Session(connection);

            string cbsReplyToAddress = "cbs-reply-to";
            var cbsSender = new SenderLink(session, "cbs-sender", "$cbs");
            var cbsReceiver = new ReceiverLink(session, cbsReplyToAddress, "$cbs");

            // construct the put-token message
            var request = new Message(shareAccessSignature);
            request.Properties = new Properties();
            request.Properties.MessageId = Guid.NewGuid().ToString();
            request.Properties.ReplyTo = cbsReplyToAddress;
            request.ApplicationProperties = new ApplicationProperties();
            request.ApplicationProperties["operation"] = "put-token";
            request.ApplicationProperties["type"] = "azure-devices.net:sastoken";
            request.ApplicationProperties["name"] = audience;
            cbsSender.Send(request);

            // receive the response
            var response = cbsReceiver.Receive();
            if (response == null || response.Properties == null || response.ApplicationProperties == null)
            {
                result = false;
            }
            else
            {
                int statusCode = (int)response.ApplicationProperties["status-code"];
                string statusCodeDescription = (string)response.ApplicationProperties["status-description"];
                if (statusCode != (int)202 && statusCode != (int)200) // !Accepted && !OK
                {
                    result = false;
                }
            }

            // the sender/receiver may be kept open for refreshing tokens
            cbsSender.Close();
            cbsReceiver.Close();
            session.Close();

            return result;
        }

        private static readonly long UtcReference = (new DateTime(1970, 1, 1, 0, 0, 0, 0)).Ticks;

        static string GetSharedAccessSignature(string keyName, string sharedAccessKey, string resource, TimeSpan tokenTimeToLive)
        {
            // http://msdn.microsoft.com/en-us/library/azure/dn170477.aspx
            // the canonical Uri scheme is http because the token is not amqp specific
            // signature is computed from joined encoded request Uri string and expiry string

#if NETMF
            // needed in .Net Micro Framework to use standard RFC4648 Base64 encoding alphabet
            System.Convert.UseRFC4648Encoding = true;
#endif
            string expiry = ((long)(DateTime.UtcNow - new DateTime(UtcReference, DateTimeKind.Utc) + tokenTimeToLive).TotalSeconds()).ToString();
            string encodedUri = HttpUtility.UrlEncode(resource);

            byte[] hmac = SHA.computeHMAC_SHA256(Convert.FromBase64String(sharedAccessKey), Encoding.UTF8.GetBytes(encodedUri + "\n" + expiry));
            string sig = Convert.ToBase64String(hmac);

            if (keyName != null)
            {
                return Fx.Format(
                "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}",
                encodedUri,
                HttpUtility.UrlEncode(sig),
                HttpUtility.UrlEncode(expiry),
                HttpUtility.UrlEncode(keyName));
            }
            else
            {
                return Fx.Format(
                    "SharedAccessSignature sr={0}&sig={1}&se={2}",
                    encodedUri,
                    HttpUtility.UrlEncode(sig),
                    HttpUtility.UrlEncode(expiry));
            }
        }
        const string KeyWifi = "123qweasd";
        const string SSID = "gravicode";

       
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            /*******************************************************************************************
            Modules added in the Program.gadgeteer designer view are used by typing 
            their name followed by a period, e.g.  button.  or  camera.
            
            Many modules generate useful events. Type +=<tab><tab> to add a handler to an event, e.g.:
                button.ButtonPressed +=<tab><tab>
            
            If you want to do something periodically, use a GT.Timer and handle its Tick event, e.g.:
                GT.Timer timer = new GT.Timer(1000); // every second (1000ms)
                timer.Tick +=<tab><tab>
                timer.Start();
            *******************************************************************************************/


            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Debug.Print("Program Started");
            characterDisplay.Clear();
            characterDisplay.Print("Starting App");
            SetupWifi();
            Thread th1 = new Thread(new ThreadStart(StartMonitor));
            th1.Start();
        }
        void SetupWifi()
        {
            //setup wifi
            wifiRS21.DebugPrintEnabled = true;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;           // setup events
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            wifiRS21.NetworkDown += new GT.Modules.Module.NetworkModule.NetworkEventHandler(wifi_NetworkDown);
            wifiRS21.NetworkUp += new GT.Modules.Module.NetworkModule.NetworkEventHandler(wifi_NetworkUp);
            // use the router's DHCP server to set my network info
            if (!wifiRS21.NetworkInterface.Opened)
                wifiRS21.NetworkInterface.Open();
            if (!wifiRS21.NetworkInterface.IsDhcpEnabled)
            {
                wifiRS21.UseDHCP();
                wifiRS21.NetworkInterface.EnableDhcp();
                wifiRS21.NetworkInterface.EnableDynamicDns();
            }
            // look for avaiable networks
            var scanResults = wifiRS21.NetworkInterface.Scan();
            characterDisplay.Clear();
            characterDisplay.Print("looking for wifi...");
            // go through each network and print out settings in the debug window
            foreach (GHI.Networking.WiFiRS9110.NetworkParameters result in scanResults)
            {
                Debug.Print("****" + result.Ssid + "****");
                Debug.Print("ChannelNumber = " + result.Channel);
                Debug.Print("networkType = " + result.NetworkType);
                Debug.Print("PhysicalAddress = " + GetMACAddress(result.PhysicalAddress));
                Debug.Print("RSSI = " + result.Rssi);
                Debug.Print("SecMode = " + result.SecurityMode);
            }

            // locate a specific network
            GHI.Networking.WiFiRS9110.NetworkParameters[] info = wifiRS21.NetworkInterface.Scan(SSID);
            if (info != null)
            {
                wifiRS21.NetworkInterface.Join(info[0].Ssid, KeyWifi);
                wifiRS21.UseThisNetworkInterface();
                bool res = wifiRS21.IsNetworkConnected;
                characterDisplay.Clear();
                characterDisplay.Print("joined to "+ wifiRS21.NetworkInterface.ActiveNetwork.Ssid);

                Debug.Print("Network joined");
                Debug.Print("active:" + wifiRS21.NetworkInterface.ActiveNetwork.Ssid);

                //waiting till connect...
                // After connecting, go out and get a web page. This can also be used to access web services
                //Gadgeteer.Networking.HttpRequest wc = WebClient.GetFromWeb("http://www.simpleweb.org/");
                //wc.ResponseReceived += new HttpRequest.ResponseHandler(wc_ResponseReceived);
            }

            //init amqp
            Amqp.Trace.TraceLevel = Amqp.TraceLevel.Frame | Amqp.TraceLevel.Verbose;
#if NETMF
            Amqp.Trace.TraceListener = (f, a) => Debug.Print(DateTime.Now.ToString("[hh:ss.fff]") + " " + Fx.Format(f, a));
#else
            Amqp.Trace.TraceListener = (f, a) => Debug.Print(DateTime.Now.ToString("[hh:ss.fff]") + " " + Fx.Format(f, a));
#endif
            address = new Address(HOST, PORT, null, null);
            connection = new Connection(address);

            string audience = Fx.Format("{0}/devices/{1}", HOST, DEVICE_ID);
            string resourceUri = Fx.Format("{0}/devices/{1}", HOST, DEVICE_ID);

            string sasToken = "SharedAccessSignature sr=FreeDeviceHub.azure-devices.net&sig=bH6Z%2Be2dPMHrJW8h0k9H2sn0nnqYZaq%2FiOlt0MgjraY%3D&se=1522944015&skn=iothubowner";
            //GetSharedAccessSignature(null, DEVICE_KEY, resourceUri, new TimeSpan(1, 0, 0));
            bool cbs = PutCbsToken(connection, HOST, sasToken, audience);

            if (cbs)
            {
                session = new Session(connection);

               
            }
            /*
            SendEvent();
            receiverThread = new Thread(ReceiveCommands);
            receiverThread.Start();
            // just as example ...
            // the application ends only after received a command or timeout on receiving
            receiverThread.Join();

            session.Close();
            connection.Close();
            */

        }
        void StartMonitor()
        {
            while (true)
            {
                if (wifiRS21.IsNetworkConnected && session!=null )
                {
                    var item = new DataSensor() { Dev = "Gadgeteer", Celsius = tempHumidSI70.TakeMeasurement().Temperature, Humidity = tempHumidSI70.TakeMeasurement().RelativeHumidity, Geo = "Indonesia", Light = lightSense.GetIlluminance() };
                    SendEvent(item);
                    characterDisplay.Clear();
                    characterDisplay.Print("SEND: " + DateTime.Now.ToString());
                    
                }
                Thread.Sleep(5000);
            }
            session.Close();
            connection.Close();
        }

        #region Networking

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            Debug.Print("Network address changed");
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            Debug.Print("Network availability: " + e.IsAvailable.ToString());
        }


        void wc_ResponseReceived(HttpRequest sender, HttpResponse response)
        {
            var state = response.StatusCode;
            string text = response.Text;
            // now that the information has been returned, disconnect from the network
            //wifiRS21.NetworkInterface.Disconnect();
        }

        // handle the network changed events
        void wifi_NetworkDown(GT.Modules.Module.NetworkModule sender, GT.Modules.Module.NetworkModule.NetworkState state)
        {
            if (state == GT.Modules.Module.NetworkModule.NetworkState.Down)
                Debug.Print("Network Up event; state = Down");
            else
                Debug.Print("Network Up event; state = Up");
        }

        void wifi_NetworkUp(GT.Modules.Module.NetworkModule sender, GT.Modules.Module.NetworkModule.NetworkState state)
        {
            if (state == GT.Modules.Module.NetworkModule.NetworkState.Up)
            {
                Debug.Print("Network Up event; state = Up");
                Debug.Print("IP:" + wifiRS21.NetworkInterface.IPAddress);
                characterDisplay.Clear();
                characterDisplay.Print("IP:"+ wifiRS21.NetworkInterface.IPAddress);
            }
            else
                Debug.Print("Network Up event; state = Down");
        }

        // borrowed from GHI's documentation
        string GetMACAddress(byte[] PhysicalAddress)
        {
            return ByteToHex(PhysicalAddress[0]) + "-"
                                + ByteToHex(PhysicalAddress[1]) + "-"
                                + ByteToHex(PhysicalAddress[2]) + "-"
                                + ByteToHex(PhysicalAddress[3]) + "-"
                                + ByteToHex(PhysicalAddress[4]) + "-"
                                + ByteToHex(PhysicalAddress[5]);
        }

        string ByteToHex(byte number)
        {
            string hex = "0123456789ABCDEF";
            return new string(new char[] { hex[(number & 0xF0) >> 4], hex[number & 0x0F] });
        }
        #endregion

        
    }
    public class DataSensor
    {
        public string Dev { get; set; }
        public string Utc { get; set; }
        public double Celsius { get; set; }
        public double Humidity { get; set; }
        public int hPa { get; set; }
        public double Light { get; set; }
        public string Geo { get; set; }
        public int WiFi { get; set; }
        public int Mem { get; set; }
        public int Id { get; set; }
    }
}
