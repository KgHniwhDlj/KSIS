using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

class Tracert
{
    private const int timeout = 1000;
    private const int maxHops = 30;
    private const int bufferSize = 1024;
    private const int packetsPerHop = 3;

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("myTracert <адрес>");
            return;
        }

        string target = args[0];
        IPAddress targetAddress;

        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(target);
            if (addresses.Length == 0)
            {
                Console.WriteLine("Не удалось выполнить операцию");
                return;
            }
            targetAddress = addresses[0];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при выполнении операции: {ex.Message}");
            return;
        }

        Console.WriteLine($"Трассировка маршрута к {target} [{targetAddress}] с максимальным числом хопов {maxHops}:");

        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            socket.ReceiveTimeout = timeout;

            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);

                Console.Write($"{ttl}\t");

                IPAddress remoteAddress = null;
                bool destinationReached = false;

                for (int packetNumber = 0; packetNumber < packetsPerHop; packetNumber++)
                {
                    byte[] sendBuffer = CreateIcmpPacket();
                    byte[] receiveBuffer = new byte[bufferSize];

                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    EndPoint remoteEP = (EndPoint)remoteEndPoint;

                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    socket.SendTo(sendBuffer, new IPEndPoint(targetAddress, 0));

                    int bytesReceived = 0;
                    try
                    {
                        bytesReceived = socket.ReceiveFrom(receiveBuffer, ref remoteEP);
                        stopwatch.Stop();

                        remoteAddress = ((IPEndPoint)remoteEP).Address;
                        long roundTripTime = stopwatch.ElapsedMilliseconds;
                        Console.Write($"{roundTripTime} ms\t");

                        if (remoteAddress.Equals(targetAddress))
                        {
                            destinationReached = true;
                        }
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        Console.Write("*\t");
                    }
                    catch (SocketException ex)
                    {
                        Console.Write($"Ошибка: {ex.SocketErrorCode}\t");
                    }
                }

                if (remoteAddress != null)
                {
                    Console.Write($"{remoteAddress}");
                }

                Console.WriteLine();

                if (destinationReached)
                {
                    Console.WriteLine("Трассировка завершена.");
                    
                    return;
                }
            }
        }

    
    }

    private static byte[] CreateIcmpPacket()
    {
        byte[] packet = new byte[64];
        packet[0] = 8;
        packet[1] = 0;
        packet[2] = 0;
        packet[3] = 0;
        packet[4] = 0;
        packet[5] = 0;
        packet[6] = 0;
        packet[7] = 0;

        ushort checksum = CalculateChecksum(packet);
        packet[2] = (byte)(checksum >> 8);
        packet[3] = (byte)(checksum & 0xFF);

        return packet;
    }

    private static ushort CalculateChecksum(byte[] buffer)
    {
        int length = buffer.Length;
        int i = 0;
        long sum = 0;

        while (length > 1)
        {
            sum += (ushort)((buffer[i++] << 8) | buffer[i++]);
            length -= 2;
        }

        if (length > 0)
        {
            sum += (ushort)(buffer[i] << 8);
        }

        while ((sum >> 16) != 0)
        {
            sum = (sum & 0xFFFF) + (sum >> 16);
        }

        return (ushort)(~sum);
    }
}
