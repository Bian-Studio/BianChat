﻿using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server_Console_Tcp
{
    internal class Program
    {
        private static List<TcpClient> clients = new List<TcpClient>();

        static void Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 911);

            listener.Start();
            Console.WriteLine("等待客户端连接...");
            listener.BeginAcceptTcpClient(AcceptCallback, listener);

            string input = null;
            while (input != "exit")
            {
                input = Console.ReadLine();
                if (input == "end all")
                {
                    lock (clients)
                    {
                        foreach (var client in clients)
                        {
                            client.Close();
                        }
                    }
                }
                else if (input.StartsWith("notice "))
                {
                    if (input.Length > 7)
                        Notice(input.Substring(7));
                }
            }
        }
        private static void AcceptCallback(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener)ar.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(ar);
            bool connected = true;
            clients.Add(client);
            Console.WriteLine($"{(IPEndPoint)client.Client.RemoteEndPoint} 已连接，当前连接数 {clients.Count}");

            Task.Run(() =>
            {
                string username = null;
                Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    if (username == null)
                    {
                        Disconnect();
                    }
                });
                while (true)
                {
                    try
                    {
                        byte[] buffer = new byte[8193];
                        int size = client.Client.Receive(buffer);
                        if (size <= 0)
                        {
                            throw new Exception();
                        }
                        Array.Resize(ref buffer, size);
                        switch (buffer[0])
                        {
                            // 登录
                            case 0:
                                username = Encoding.UTF8.GetString(buffer, 1, buffer.Length - 1);
                                string notice = $"{username} 已上线";
                                Notice(notice);
                                break;

                            // 聊天信息
                            case 1:
                                Console.WriteLine($"收到数据：{Encoding.UTF8.GetString(buffer, 1, buffer.Length - 1)}");
                                lock (clients)
                                {
                                    foreach (var client1 in clients)
                                    {
                                        try
                                        {
                                            if (client1 != client)
                                                client1.Client.Send(new byte[1] { 1 }.Concat(buffer).ToArray());
                                        }
                                        catch
                                        {
                                            Disconnect();
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    catch
                    {
                        Disconnect();
                        break;
                    }
                }
            });

            void Disconnect()
            {
                if (connected)
                {
                    try
                    {
                        lock (clients)
                        {
                            connected = false;
                            clients.Remove(client);
                            client.Close();
                        }
                        Console.WriteLine($"客户端已断开连接，当前连接数 {clients.Count}");
                    }
                    catch { }
                }
            }

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    try
                    {
                        client.Client.Send(new byte[1] { 2 }.Concat(BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())).ToArray());
                    }
                    catch
                    {
                        Disconnect();
                        break;
                    }
                }
            });

            listener.BeginAcceptTcpClient(AcceptCallback, listener);
        }

        private static void Notice(string notice)
        {
            Console.WriteLine($"公告：{notice}");
            lock (clients)
            {
                foreach (var client1 in clients)
                {
                    try
                    {
                        client1.Client.Send(new byte[1] { 0 }.Concat(Encoding.UTF8.GetBytes(notice)).ToArray());
                    }
                    catch
                    {
                        client1.Close();
                    }
                }
            }
        }
    }
}