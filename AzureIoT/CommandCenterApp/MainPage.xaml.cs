using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Azure.Devices;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;
using Windows.UI.Xaml.Documents;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CommandCenterApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        static int MessageCounter = 0;
        static ServiceClient serviceClient;
        static string connectionString = "HostName=FreeDeviceHub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=pGREIqFsT9rGgDkGJP3K5Vkrg5zmTnNZAxNeqWpT4UM=";
        public MainPage()
        {
            this.InitializeComponent();
            serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
            ReceiveFeedbackAsync();
            //Task feedbackThread = new Task(new Action(ReceiveFeedbackAsync));
            //feedbackThread.Start();
        }
        private async void ReceiveFeedbackAsync()
        {
            var feedbackReceiver = serviceClient.GetFeedbackReceiver();
           
            while (true)
            {
                var feedbackBatch = await feedbackReceiver.ReceiveAsync();
                if (feedbackBatch == null) continue;
                WriteLog(string.Format("Received feedback: {0}", string.Join(", ", feedbackBatch.Records.Select(f => f.StatusCode))));
                await feedbackReceiver.CompleteAsync(feedbackBatch);
            }
        }
        private async  Task SendCloudToDeviceMessageAsync(DeviceCommand item)
        {
            if (String.IsNullOrEmpty(TxtDevice.Text)) return;
            var json = JsonConvert.SerializeObject(item);
            var commandMessage = new Message(Encoding.ASCII.GetBytes(json));
            commandMessage.Ack = DeliveryAcknowledgement.Full;

            await serviceClient.SendAsync(TxtDevice.Text, commandMessage);
        }
        void WriteLog(string Message)
        {
            if (MessageCounter > 10)
            {
                MessageCounter = 0;
                TxtLog.Blocks.Clear();
            }
            var Par = new Paragraph();
            var run = new Run();
            run.Text = Message;
            Par.Inlines.Add(run);
            TxtLog.Blocks.Add(Par);
            MessageCounter++;
        }


        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            var item = new DeviceCommand() { Command = TxtCommand.Text , Data=TxtData.Text };
            await SendCloudToDeviceMessageAsync(item);

        }
    }
    public class DeviceCommand
    {
        public string Command { get; set; }
        public string Data { get; set; }

    }
}
