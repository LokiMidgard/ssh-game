using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SshGame.Server.Ciphers;
using SshGame.Server.Compressions;
using SshGame.Server.HostKeyAlgorithms;
using SshGame.Server.KexAlgorithms;
using SshGame.Server.MACAlgorithms;
using SshGame.Server.Packets;
using SshGame.Server.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;


namespace SshGame.Server
{
    internal partial class Server
    {
        public const string ProtocolVersionExchange = "SSH-2.0-SshGame.Server";

        private const int DefaultPort = 22;
        private const int ConnectionBacklog = 64;

        private IConfigurationRoot m_Configuration;
        private ILoggerFactory m_LoggerFactory;
        private ILogger m_Logger;

        private TcpListener m_Listener;
        private List<Client> m_Clients = new List<Client>();

        private static Dictionary<string, string> s_HostKeys = new Dictionary<string, string>();

        public static (string name, Func<TType> create, bool IsAdvertised) GetDiscoverable<T, TType>()
                where T : IDiscovrable, TType, new()
        {
            return (T.Name, () => new T(), T.IsAdvertised);
        }
        [AutoInvoke.FindAndInvoke]
        public static (string name, Func<IServiceFactory> create, bool IsAdvertised) GetSupportedServiceFactorys<T>()
          where T : IDiscovrable, IServiceFactory, new()
      => GetDiscoverable<T, IServiceFactory>();

        [AutoInvoke.FindAndInvoke]
        public static (string name, Func<IKexAlgorithm> create, bool IsAdvertised) GetSupportedKexAlgorithms<T>()
            where T : IDiscovrable, IKexAlgorithm, new()
        => GetDiscoverable<T, IKexAlgorithm>();

        [AutoInvoke.FindAndInvoke]
        public static (string name, Func<ICipher> create, bool IsAdvertised) GetSupportedChiperAlgorithms<T>()
          where T : IDiscovrable, ICipher, new()
      => GetDiscoverable<T, ICipher>();
        [AutoInvoke.FindAndInvoke]
        public static (string name, Func<ICompression> create, bool IsAdvertised) GetSupportedCompressionAlgorithms<T>()
          where T : IDiscovrable, ICompression, new()
      => GetDiscoverable<T, ICompression>();
        [AutoInvoke.FindAndInvoke]
        public static (string name, Func<IHostKeyAlgorithm> create, bool IsAdvertised) GetSupportedHKAlgorithms<T>()
          where T : IDiscovrable, IHostKeyAlgorithm, new()
      => GetDiscoverable<T, IHostKeyAlgorithm>();
        [AutoInvoke.FindAndInvoke]
        public static (string name, Func<IMACAlgorithm> create, bool IsAdvertised) GetSupportedMACAlgorithms<T>()
          where T : IDiscovrable, IMACAlgorithm, new()
      => GetDiscoverable<T, IMACAlgorithm>();

        public static IReadOnlyDictionary<string, Func<IServiceFactory>> SupportedServices { get; } = GetSupportedServiceFactorys().Where(x => x.IsAdvertised).ToImmutableDictionary(x => x.name, x => x.create);
        public static IReadOnlyDictionary<string, Func<IKexAlgorithm>> SupportedKexAlgorithms { get; } = GetSupportedKexAlgorithms().Where(x => x.IsAdvertised).ToImmutableDictionary(x => x.name, x => x.create);

        public static IReadOnlyDictionary<string, Func<IHostKeyAlgorithm>> SupportedHostKeyAlgorithms { get; } = GetSupportedHKAlgorithms().Where(x => x.IsAdvertised).ToImmutableDictionary(x => x.name, x =>
        {
            return new Func<IHostKeyAlgorithm>(() =>
            {
                var algo = x.create();
                algo.ImportKey(s_HostKeys);
                return algo;
            });
        });


        public static IReadOnlyDictionary<string, Func<ICipher>> SupportedCiphers { get; } = GetSupportedChiperAlgorithms().Where(x => x.IsAdvertised).ToImmutableDictionary(x => x.name, x => x.create);


        public static IReadOnlyDictionary<string, Func<IMACAlgorithm>> SupportedMACAlgorithms { get; } = GetSupportedMACAlgorithms().Where(x => x.IsAdvertised).ToImmutableDictionary(x => x.name, x => x.create);


        public static IReadOnlyDictionary<string, Func<ICompression>> SupportedCompressions { get; } = GetSupportedCompressionAlgorithms().Where(x => x.IsAdvertised).ToImmutableDictionary(x => x.name, x => x.create);


        public Server()
        {
            m_Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("SshGame.Server.json", optional: false)
                .Build();

            m_LoggerFactory = LoggerFactory.Create(x => x.AddConsole().AddConfiguration(m_Configuration.GetSection("Logging")))
            ;


            m_Logger = m_LoggerFactory.CreateLogger("SshGame.Server");

            IConfigurationSection keys = m_Configuration.GetSection("keys");
            foreach (IConfigurationSection key in keys.GetChildren())
            {
                s_HostKeys[key.Key] = key.Value;
            }
        }

        public void Start()
        {
            // Ensure we are stopped before we start listening
            Stop();

            m_Logger.LogInformation("Starting up...");

            // Create a listener on the required port
            int port = m_Configuration.GetValue<int>("port", DefaultPort);
            m_Listener = new TcpListener(IPAddress.Any, port);
            m_Listener.Start(ConnectionBacklog);

            m_Logger.LogInformation($"Listening on port: {port}");
        }

        public void Poll()
        {
            // Check for new connections
            while (m_Listener.Pending())
            {
                Task<Socket> acceptTask = m_Listener.AcceptSocketAsync();
                acceptTask.Wait();

                Socket socket = acceptTask.Result;
                m_Logger.LogDebug($"New Client: {socket.RemoteEndPoint.ToString()}");

                // Create and add client list
                m_Clients.Add(new Client(socket, m_LoggerFactory.CreateLogger(socket.RemoteEndPoint.ToString())));
            }

            // Poll each client for activity
            m_Clients.ForEach(c => c.Poll());

            // Remove all disconnected clients
            m_Clients.RemoveAll(c => c.IsConnected == false);
        }

        public void Stop()
        {
            if (m_Listener != null)
            {
                m_Logger.LogInformation("Shutting down...");

                // Disconnect clients and clear clients
                m_Clients.ForEach(c => c.Disconnect(DisconnectReason.SSH_DISCONNECT_BY_APPLICATION, "The server is shutting down."));
                m_Clients.Clear();

                m_Listener.Stop();
                m_Listener = null;

                m_Logger.LogInformation("Stopped!");
            }
        }



    }
}
