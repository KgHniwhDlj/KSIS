using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerApp
{
    public class ChatServer
    {
        private readonly TcpListener _tcpListener;
        private readonly List<ClientHandler> _clients = new List<ClientHandler>();
        private bool _isRunning = true;
        private UdpClient _udpClient;
        private readonly int _udpPort;
        private bool _discoveryRunning = true;

        public ChatServer(string ip, int port, int udpPort=-1)
        {
            // Проверка доступности порта
            if (IsPortInUse(ip, port))
            {
                throw new Exception($"Порт {port} уже занят!");
            }

            // Если udpPort не указан (-1), используем тот же порт что и для TCP
            _udpPort = udpPort == -1 ? port : udpPort;
            IPAddress ipAddress = IPAddress.Parse(ip);
            _tcpListener = new TcpListener(ipAddress, port);
        }

        
        private void StartDiscovery()
        {
            _udpClient = new UdpClient(_udpPort) { EnableBroadcast = true };
            new Thread(DiscoveryListener).Start();
            new Thread(BroadcastPresence).Start();
        }

        private void DiscoveryListener()
        {
            try
            {
                while (_discoveryRunning)
                {
                    IPEndPoint remoteEP = null;
                    byte[] data = _udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);

                    if (message.StartsWith("NODE|"))
                    {
                        string[] parts = message.Split('|');
                        string nodeName = parts[1];
                        string nodeIp = parts[2];
                        int nodePort = int.Parse(parts[3]);

                       
                    }
                }
            }
            catch { /* Завершение работы */ }
        }

        private void BroadcastPresence()
        {
            while (_discoveryRunning)
            {
                try
                {
                    string localIp = Dns.GetHostEntry(Dns.GetHostName())
                        .AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                        .ToString();

                    byte[] data = Encoding.UTF8.GetBytes($"NODE|{Environment.MachineName}|{localIp}|{_tcpListener.LocalEndpoint.ToString().Split(':')[1]}");
                    _udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, _udpPort));
                }
                catch { /* Ошибка отправки */ }

                Thread.Sleep(5000); 
            }
        }

        private bool IsPortInUse(string ip, int port)
        {
            try
            {
                using (var tester = new TcpListener(IPAddress.Parse(ip), port))
                {
                    tester.Start();
                    tester.Stop();
                }
                return false;
            }
            catch (SocketException)
            {
                return true;
            }
        }

        public void Start()
        {
            try
            {
                _tcpListener.Start();
                StartDiscovery(); // Запускаем обнаружение
                Console.WriteLine($"Сервер запущен на {_tcpListener.LocalEndpoint}...");

                // Отдельный поток для обработки подключений
                new Thread(() =>
            {
                while (_isRunning)
                {
                    if (_tcpListener.Pending())
                    {
                        var client = _tcpListener.AcceptTcpClient();
                        var handler = new ClientHandler(client, this);
                        lock (_clients) _clients.Add(handler);
                        new Thread(handler.Handle).Start();
                    }
                    Thread.Sleep(100);
                }
            }).Start();

           
           
        }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Stop();
            }
        }


        public void Broadcast(string message, ClientHandler sender = null)
        {
            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            lock (_clients)
            {
                foreach (var client in _clients.ToArray())
                {
                    if (client != sender) client.Send(formattedMessage);
                }
            }
            Console.WriteLine(formattedMessage);
        }

        public void RemoveClient(ClientHandler client)
        {
            lock (_clients) _clients.Remove(client);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Клиент отключен. Всего: {_clients.Count}");
        }

        public void Stop()
        {
            _isRunning = false;
            _discoveryRunning = false;
            _udpClient?.Close();
            _tcpListener?.Stop();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Сервер остановлен");
        }
    }

    public class ClientHandler
    {
        private readonly TcpClient _client;
        private readonly ChatServer _server;
        private NetworkStream _stream;
        private string _username;
        private string _clientIp;

        public ClientHandler(TcpClient client, ChatServer server)
        {
            _client = client;
            _server = server;
            _stream = client.GetStream();
            _clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
        }

        public void Handle()
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) return;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string[] parts = message.Split('|');

                if (parts.Length >= 2 && parts[0] == "CONNECT")
                {
                    _username = parts[1];
                    _clientIp = parts.Length > 2 ? parts[2] : _clientIp;
                    _server.Broadcast($"{_username} ({_clientIp}) подключился");
                }

                while (true)
                {
                    if (_stream.DataAvailable)
                    {
                        bytesRead = _stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        if (message.StartsWith("MSG|"))
                        {
                            string msgContent = message.Substring(4);
                            _server.Broadcast($"{_username}: {msgContent}", this);
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка: {ex.Message}");
            }
            finally
            {
                if (_username != null)
                {
                    _server.Broadcast($"{_username} отключился");
                }
                _stream?.Close();
                _client?.Close();
                _server.RemoveClient(this);
            }
        }

        public void Send(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            _stream.Write(data, 0, data.Length);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Введите IP сервера: ");
            string ip = Console.ReadLine();

            Console.Write("Введите порт: ");
            int port = int.Parse(Console.ReadLine());

            try
            {
                var server = new ChatServer(ip, port);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true; // Предотвращаем стандартное поведение
                    server.Stop();
                    Environment.Exit(0); // Принудительно завершаем приложение
                };
                server.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось запустить сервер: {ex.Message}");
            }
        }
    }
}