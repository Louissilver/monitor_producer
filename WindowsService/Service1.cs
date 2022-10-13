using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Threading;
using System.IO;
using RabbitMQ.Client;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;

namespace WindowsService2
{
    [RunInstaller(true)]
    public partial class Service1 : ServiceBase
    {
        int ScheduleTime = Convert.ToInt32(ConfigurationSettings.AppSettings["ThreadTime"]);

        public Thread Worker = null;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                ThreadStart start = new ThreadStart(Working);
                Worker = new Thread(start);
                Worker.Start();
            }
            catch (Exception)
            {
                throw;
            }
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

                    string message = "{" + $"\"macAddress\": \"{macAddr}\", \"ipv4\": \"{LocalIPAddress().ToString()}\", \"hostname\": \"{LocalHostName()}\"," + $" \"DataHoraMensagem\": \"{DateTime.Now}\"," + $" \"local\": \"Porto Alegre\"" +  "}";
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
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
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
            try
            {
                if (Worker != null && Worker.IsAlive)
                {
                    Worker.Abort();
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }
    }
}
