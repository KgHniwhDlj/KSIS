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
        static List<Socket> clientSockets = new List<Socket>();
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
                serverSocket.Bind(serverEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при привязке сокета. Проверьте IP и порт. {ex.Message}");
                return;
            }

            serverSocket.Listen(10);
            Console.WriteLine($"Сервер запущен и ожидает подключений на {ipString}:{port}");

            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.Start();

            Thread sendThread = new Thread(SendMessages);
            sendThread.Start();

            acceptThread.Join();
            sendThread.Join();
            ShutdownServer();
        }

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
                    IPEndPoint clientEP = clientSocket.RemoteEndPoint as IPEndPoint;
                    // Здесь выводится только IP клиента (без порта)
                    string notice = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Система ({clientEP.Address}): подключился";
                    Console.WriteLine(notice);
                    BroadcastMessage(notice, null);

                    Thread clientThread = new Thread(() => ReceiveData(clientSocket));
                    clientThread.Start();
                }
                catch (SocketException)
                {
                    break;
                }
            }
        }

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
                        throw new SocketException();
                    }
                    string text = Encoding.UTF8.GetString(buffer, 0, received);
                    Console.WriteLine(text);
                    // При передаче сообщения исключаем отправителя, чтобы он не получил эхо.
                    BroadcastMessage(text, clientSocket);
                }
                catch (SocketException)
                {
                    IPEndPoint clientEP = clientSocket.RemoteEndPoint as IPEndPoint;
                    string disconnectMsg = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Система ({clientEP.Address}): отключился";
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

        static void SendMessages()
        {
            while (isRunning)
            {
                string message = Console.ReadLine();
                if (string.IsNullOrEmpty(message))
                    continue;
                // Для сообщений сервера убираем IP:порт – выводим только текст сообщения
                string fullMessage = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Сервер: {message}";
                Console.WriteLine(fullMessage);
                BroadcastMessage(fullMessage, null);
            }
        }

        static void BroadcastMessage(string message, Socket sender)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            lock (lockObj)
            {
                // Создаем копию списка, чтобы избежать проблем при итерации
                foreach (var socket in new List<Socket>(clientSockets))
                {
                    if (sender != null && socket == sender)
                        continue;
                    try
                    {
                        socket.Send(data);
                    }
                    catch (SocketException)
                    {
                        clientSockets.Remove(socket);
                        socket.Close();
                    }
                }
            }
        }

        static void ShutdownServer()
        {
            isRunning = false;
            string shutdownMsg = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Сервер: завершает работу";
            BroadcastMessage(shutdownMsg, null);
            foreach (var socket in clientSockets)
            {
                socket.Close();
            }
            serverSocket.Close();
        }
    }
}
