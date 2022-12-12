using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Configuration;
using System.Threading;
using RabbitMQ.Client;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Management;
using System.Collections.Generic;

namespace MonitoringProducer
{
    [RunInstaller(true)]
    public partial class Service : ServiceBase
    {
        readonly int ScheduleTime = Convert.ToInt32(ConfigurationManager.AppSettings["ThreadTime"]);
        readonly string Hostname = ConfigurationManager.AppSettings["Hostname"];
        readonly string Password = ConfigurationManager.AppSettings["Password"];
        readonly string UserName = ConfigurationManager.AppSettings["UserName"];
        readonly int Port = Convert.ToInt32(ConfigurationManager.AppSettings["Port"]);

        private Thread Worker = null;

        public Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            ThreadStart start = new ThreadStart(Working);
            Worker = new Thread(start);
            Worker.Start();
        }

        public void Working()
        {
            while (true)
            {
                var macAddr = GetMACAddress();


                var factory = new ConnectionFactory() { HostName = Hostname.ToString(), Password = Password.ToString(), UserName = UserName.ToString(), Port = Port };
                using (var connection = factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: "monitor",
                                         durable: false,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    string message = "{" + $"\"macAddress\": \"{macAddr}\", \"ipv4\": \"{LocalIPAddress()}\", \"hostname\": \"{LocalHostName()}\"," + $" \"data\": \"{DateTime.Now.ToString("yyyy'-'MM'-'dd")}\"," + $" \"hora\": \"{DateTime.Now.ToString("HH:mm:ss")}\"," + $" \"local\": \"Porto Alegre\"" + "}";
                    var body = Encoding.UTF8.GetBytes(message);

                    channel.BasicPublish(exchange: "",
                                         routingKey: "monitor",
                                         basicProperties: null,
                                         body: body);
                }

                Thread.Sleep(ScheduleTime * 10 * 1000);
            }
        }

        public static string GetMACAddress()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration where IPEnabled=true");
            IEnumerable<ManagementObject> objects = searcher.Get().Cast<ManagementObject>();
            string mac = (from o in objects orderby o["IPConnectionMetric"] select o["MACAddress"].ToString()).FirstOrDefault();
            return mac.Replace(":", "");
        }

        private IPHostEntry Host()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return null;
            }

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            return host;
        }

        private IPAddress LocalIPAddress()
        {
            return Host()
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }

        private string LocalHostName()
        {
            return Host().HostName;
        }

        protected override void OnStop()
        {
            if (Worker != null && Worker.IsAlive)
            {
                Worker.Abort();
            }
        }
    }
}
