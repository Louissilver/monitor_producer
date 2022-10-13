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

namespace MonitoringProducer
{
    [RunInstaller(true)]
    public partial class Service : ServiceBase
    {
        readonly int ScheduleTime = Convert.ToInt32(ConfigurationManager.AppSettings["ThreadTime"]);

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
                var macAddr =
                            (
                                from nic in NetworkInterface.GetAllNetworkInterfaces()
                                where nic.OperationalStatus == OperationalStatus.Up
                                select nic.GetPhysicalAddress().ToString()
                            ).FirstOrDefault();


                var factory = new ConnectionFactory() { HostName = "localhost" };
                using (var connection = factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: "hello",
                                         durable: false,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    string message = "{" + $"\"macAddress\": \"{macAddr}\", \"ipv4\": \"{LocalIPAddress()}\", \"hostname\": \"{LocalHostName()}\"," + $" \"DataHoraMensagem\": \"{DateTime.Now:yyyy'-'MM'-'dd' 'HH':'mm':'ss}\"," + $" \"local\": \"Porto Alegre\"" +  "}";
                    var body = Encoding.UTF8.GetBytes(message);

                    channel.BasicPublish(exchange: "",
                                         routingKey: "hello",
                                         basicProperties: null,
                                         body: body);
                }

                Thread.Sleep(ScheduleTime * 60 * 1000);
            }
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
