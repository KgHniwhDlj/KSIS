using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientApp
{
    public class ChatClient
    {
        private readonly string _username;
        private readonly string _clientIp;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _isRunning;

        public ChatClient(string username, string clientIp)
        {
            _username = username;
            _clientIp = clientIp;
        }

        public void Start()
        {
            Console.Write("Введите IP сервера: ");
            string serverIp = Console.ReadLine();

            Console.Write("Введите порт сервера: ");
            int port = int.Parse(Console.ReadLine());

            _tcpClient = new TcpClient();
            _tcpClient.Connect(serverIp, port);
            _stream = _tcpClient.GetStream();

            // Отправляем данные подключения
            Send($"CONNECT|{_username}|{_clientIp}");

            // Поток для приёма сообщений
            _isRunning = true;
            new Thread(ReceiveMessages).Start();

            Console.WriteLine("Чат запущен. Вводите сообщения (/exit для выхода):");
            while (_isRunning)
            {
                string input = Console.ReadLine();
                if (input == "/exit")
                {
                    _isRunning = false;
                    break;
                }
                Send($"MSG|{input}");
            }
            Disconnect();
        }

        private void Send(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            _stream.Write(data, 0, data.Length);
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            while (_isRunning)
            {
                if (_stream.DataAvailable)
                {
                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine(message);
                }
                Thread.Sleep(100);
            }
        }

        private void Disconnect()
        {
            _isRunning = false;
            _stream?.Close();
            _tcpClient?.Close();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Соединение закрыто");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Введите ваше имя: ");
            string name = Console.ReadLine();

            Console.Write("Введите ваш IP адрес: ");
            string clientIp = Console.ReadLine();

            var client = new ChatClient(name, clientIp);
            try
            {
                client.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }
}