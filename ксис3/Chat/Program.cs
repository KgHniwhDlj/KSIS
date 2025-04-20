using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Chat
{
    class Program
    {
        static Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        static bool isRunning = true;

        static void Main(string[] args)
        {
            Console.WriteLine("Введите ваше имя:");
            string userName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(userName))
                userName = "Аноним";

            Console.WriteLine("Введите ваш IP для локального подключения:");
            string localIP = Console.ReadLine();
            Console.WriteLine("Введите IP сервера для подключения:");
            string serverIP = Console.ReadLine();
            Console.WriteLine("Введите порт сервера:");
            if (!int.TryParse(Console.ReadLine(), out int port))
            {
                Console.WriteLine("Некорректный порт!");
                return;
            }

            try
            {
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(localIP), 0);
                clientSocket.Bind(localEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при привязке клиента: {ex.Message}");
                return;
            }

            try
            {
                IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), port);
                clientSocket.Connect(serverEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Невозможно подключиться к серверу: {ex.Message}");
                return;
            }

            Console.WriteLine($"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Подключено к серверу {serverIP}:{port}");

            // Для отображения используем только IP (без порта)
            string localIPAddr = ((IPEndPoint)clientSocket.LocalEndPoint).Address.ToString();
            string connMessage = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {userName} ({localIPAddr}): подключился";
            clientSocket.Send(Encoding.UTF8.GetBytes(connMessage));

            Thread receiveThread = new Thread(ReceiveData);
            receiveThread.Start();

            while (isRunning)
            {
                string message = Console.ReadLine();
                if (string.IsNullOrEmpty(message))
                    continue;
                string fullMessage = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {userName} ({localIPAddr}): {message}";
                // Отправляем сообщение на сервер;
                // сервер затем пересылает его всем, кроме отправителя, чтобы убрать дублирование.
                clientSocket.Send(Encoding.UTF8.GetBytes(fullMessage));
            }
            clientSocket.Close();
        }

        static void ReceiveData()
        {
            while (isRunning)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int received = clientSocket.Receive(buffer);
                    if (received == 0)
                        break;
                    string text = Encoding.UTF8.GetString(buffer, 0, received);
                    Console.WriteLine(text);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при получении данных: {ex.Message}");
                    break;
                }
            }
        }
    }
}
