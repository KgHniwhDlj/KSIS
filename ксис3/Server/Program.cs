using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace Server
{
    class Program
    {
        // Список подключённых клиентов
        static List<Socket> clientSockets = new List<Socket>();
        // Основной сокет сервера
        static Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        static bool isRunning = true;
        static readonly object lockObj = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("Введите IP сервера:");
            string ipString = Console.ReadLine();
            Console.WriteLine("Введите порт сервера:");
            if (!int.TryParse(Console.ReadLine(), out int port))
            {
                Console.WriteLine("Некорректный порт!");
                return;
            }

            IPEndPoint serverEndPoint;
            try
            {
                serverEndPoint = new IPEndPoint(IPAddress.Parse(ipString), port);
                // Пробуем привязать сокет
                serverSocket.Bind(serverEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при привязке сокета. Проверьте IP и порт. {ex.Message}");
                return;
            }

            // Начинаем прослушивание входящих соединений.
            serverSocket.Listen(10);
            Console.WriteLine($"Сервер запущен и ожидает подключений на {ipString}:{port}");

            // Запускаем отдельный поток для принятия клиентов
            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.Start();

            // Отдельный поток для локального ввода с клавиатуры (отправка сообщений от сервера)
            Thread sendThread = new Thread(SendMessages);
            sendThread.Start();

            acceptThread.Join();
            sendThread.Join();
            ShutdownServer();
        }

        // Метод приёма новых клиентов
        static void AcceptClients()
        {
            while (isRunning)
            {
                try
                {
                    Socket clientSocket = serverSocket.Accept();
                    lock (lockObj)
                    {
                        clientSockets.Add(clientSocket);
                    }
                    IPEndPoint clientEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
                    string notice = $"{DateTime.Now} Новый узел подключен: {clientEndPoint.Address}:{clientEndPoint.Port}";
                    Console.WriteLine(notice);
                    // Оповещаем всех клиентов о появлении нового узла
                    BroadcastMessage(notice, null);

                    // Запускаем поток для приёма сообщений от данного клиента
                    Thread clientThread = new Thread(() => ReceiveData(clientSocket));
                    clientThread.Start();
                }
                catch (SocketException)
                {
                    // Если сервер закрывается, выходим из цикла
                    break;
                }
            }
        }

        // Метод получения данных от клиента
        static void ReceiveData(Socket clientSocket)
        {
            while (isRunning)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int received = clientSocket.Receive(buffer);
                    if (received == 0)
                    {
                        // Клиент отключился
                        throw new SocketException();
                    }
                    string text = Encoding.UTF8.GetString(buffer, 0, received);
                    //IPEndPoint clientEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
                    //string log = $"{DateTime.Now} Сообщение от {clientEndPoint.Address}:{clientEndPoint.Port}: {text}";
                    //Console.WriteLine(log);
                    //BroadcastMessage(log);
                    // Локально выводим полученное сообщение
                    Console.WriteLine(text);
                    // При пересылке сообщения пропускаем отправителя (чтобы он не получил эхо)
                    BroadcastMessage(text, clientSocket);
                }
                catch (SocketException)
                {
                    // Обработка отключения клиента
                    IPEndPoint clientEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
                    string disconnectMsg = $"{DateTime.Now} Узел отключился: {clientEndPoint.Address}:{clientEndPoint.Port}";
                    Console.WriteLine(disconnectMsg);
                    BroadcastMessage(disconnectMsg, null);

                    lock (lockObj)
                    {
                        clientSockets.Remove(clientSocket);
                    }
                    clientSocket.Close();
                    break;
                }
            }
        }

        // Метод отправки сообщений от сервера (с консоли) всем клиентам
        static void SendMessages()
        {
            while (isRunning)
            {
                string message = Console.ReadLine();
                if (string.IsNullOrEmpty(message))
                    continue;

                string fullMessage = $"{DateTime.Now} [Сервер]: {message}";
                Console.WriteLine(fullMessage);
                BroadcastMessage(fullMessage, null);
            }
        }

        // Метод широковещательной отправки сообщений всем подключённым клиентам
        static void BroadcastMessage(string message, Socket sender)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            lock (lockObj)
            {
                foreach (var socket in clientSockets.ToArray())
                {
                    if (sender != null && socket == sender)
                        continue;
                    try
                    {
                        socket.Send(data);
                    }
                    catch (SocketException)
                    {
                        // Если отправка не удалась (например, клиент отключился), удаляем сокет
                        clientSockets.Remove(socket);
                        socket.Close();
                    }
                }
            }
        }

        // Метод корректного завершения работы сервера
        static void ShutdownServer()
        {
            isRunning = false;
            string shutdownMsg = $"{DateTime.Now} Сервер завершает работу.";
            BroadcastMessage(shutdownMsg, null);
            foreach (var socket in clientSockets)
            {
                socket.Close();
            }
            serverSocket.Close();
        }
    }
}
