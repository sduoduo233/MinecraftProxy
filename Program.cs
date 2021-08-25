using System;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace MinecraftProxy2
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Minecraft Proxy v2");
            Console.WriteLine("Author: duoduo\n");

            Console.Write("ip: ");
            string ip = Console.ReadLine();
            Console.Write("port: ");
            int port = Int32.Parse(Console.ReadLine());

            Console.Write("local port: ");
            int local_port = Int32.Parse(Console.ReadLine());

            //本地服务器
            Socket localServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            localServer.Bind(new IPEndPoint(IPAddress.Parse("0.0.0.0"), local_port));

            //读取motd
            string motd = "{\"version\":{\"name\":\"1.8.7\",\"protocol\":47},\"players\":{\"max\":1919810,\"online\":114514,\"sample\":[]},\"description\":{\"text\":\"Minecraft Proxy\"}}";
            if (!File.Exists("motd.json"))
            {
                using (StreamWriter sw = File.CreateText("motd.json"))
                {
                    sw.WriteLine(motd);
                }
            }
            else
            {
                using (StreamReader sr = File.OpenText("motd.json"))
                {
                    motd = sr.ReadToEnd();
                }
            }

            localServer.Listen(10);
            while (true)
            {
                Socket client = localServer.Accept();
                new Handler(client, ip, port, motd);
            }
        }
    }
}
