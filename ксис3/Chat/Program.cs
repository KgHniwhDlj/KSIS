using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatClient
{
    class Program
    {
        static Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        static bool isRunning = true;

        static void Main(string[] args)
        {
            // Ввод имени пользователя
            Console.WriteLine("Введите ваше имя:");
            string userName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = "Аноним";
            }

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

            // Биндим сокет клиента на указанный локальный адрес
            try
            {
                // Порт 0 – системный выбор свободного порта
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(localIP), 0);
                clientSocket.Bind(localEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при привязке клиента: {ex.Message}");
                return;
            }

            // Подключаемся к серверу
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

            Console.WriteLine($"[{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")}] Подключено к серверу {serverIP}:{port}");

            // Отправляем уведомление о подключении (это сообщение отправляется на сервер,
            // и согласно логике сервера не будет эхо-выведено самому отправителю)
            string connMessage = $"[{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")}] {userName} ({clientSocket.LocalEndPoint}): подключился";
            clientSocket.Send(Encoding.UTF8.GetBytes(connMessage));

            // Поток для получения сообщений от сервера
            Thread receiveThread = new Thread(ReceiveData);
            receiveThread.Start();

            // Основной цикл для отправки сообщений
            while (isRunning)
            {
                string message = Console.ReadLine();
                if (string.IsNullOrEmpty(message))
                    continue;

                // Формат сообщения согласно требованиям
                string fullMessage = $"[{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")}] {userName} ({clientSocket.LocalEndPoint}): {message}";
                try
                {
                    clientSocket.Send(Encoding.UTF8.GetBytes(fullMessage));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при отправке сообщения: {ex.Message}");
                    break;
                }
            }
            clientSocket.Close();
        }

        // Метод получения сообщений от сервера
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
                    // Выводим полученное сообщение в консоль
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
