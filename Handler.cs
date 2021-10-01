using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MinecraftProxy2
{
    class Handler
    {
        private Socket client;
        private Socket remote;
        private NetworkStream clientStream;
        private NetworkStream remoteStream;

        private bool isPing = true; //首次连接 或 客户端尝试ping服务器
        private bool handshaked = false;
        private bool validated = false; //是否检查过白名单

        List<byte> handshake_buffer;

        private string ip;
        private int port;

        private string client_ip;

        private string motd;

        public Handler(Socket socket, string ip, int port, string motd)
        {
            this.motd = motd;

            client = socket;
            this.client_ip = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();

            this.ip = ip;
            this.port = port;
            
            if (this.client.Connected) {
                Console.WriteLine($"[+] 客户端 { this.client_ip } 连接成功.");

                clientStream = new NetworkStream(client);
                new Thread(new ThreadStart(UpwardThread)).Start();
            }
            else
            {
                Close();
            }
        }

        private void UpwardThread()
        {
            try
            {
                while (client.Connected)
                {
                    if (isPing)
                    {
                        //客户端尝试ping服务器
                        int packet_length;
                        byte[] buffer;
                        (packet_length, buffer) = Utils.ReadVarInt(clientStream);

                        byte[] packet_buffer = new byte[packet_length];
                        
                        for(int i=0; i<packet_length; i++)
                        {
                            packet_buffer[i] = (byte)clientStream.ReadByte();
                        }

                        int pos = 0;
                        int packet_id;
                        (packet_id, pos) = Utils.ReadVarInt(packet_buffer, pos);

                        switch (packet_id)
                        {
                            case 0: //handshake or request packet
                                if (!handshaked) //handshake packet
                                {
                                    handshaked = true;

                                    int protocol_version; //协议版本
                                    (protocol_version, pos) = Utils.ReadVarInt(packet_buffer, pos);

                                    string ip; //ip
                                    (ip, pos) = Utils.ReadString(packet_buffer, pos);

                                    ushort port; //port
                                    (port, pos) = Utils.ReadUShort(packet_buffer, pos);

                                    int next_state; //状态
                                    (next_state, pos) = Utils.ReadVarInt(packet_buffer, pos);

                                    if (next_state == 1) //ping 请求
                                    {
                                        isPing = true;
                                        SendResponse();
                                        continue;
                                    }
                                    else //登录请求
                                    {
                                        isPing = false;

                                        //修改握手包
                                        handshake_buffer = new List<byte>(); //把握手包存起来，当白名单检查完成后一起发送

                                        handshake_buffer.AddRange(Utils.WriteVarInt(0)); //packet_id
                                        handshake_buffer.AddRange(Utils.WriteVarInt(protocol_version)); //协议版本
                                        handshake_buffer.AddRange(Utils.WriteString(this.ip)); //ip
                                        handshake_buffer.AddRange(Utils.WriteUShort(port)); //port
                                        handshake_buffer.AddRange(Utils.WriteVarInt(2)); //登录
                                        continue;
                                    }
                                }
                                else 
                                { //request packet
                                    continue;
                                }

                            case 1:
                                List<byte> pong = new List<byte>();
                                pong.AddRange(Utils.WriteVarInt(1)); //packet id
                                for(int i=1; i<packet_buffer.Length; i++)
                                {
                                    pong.Add(packet_buffer[i]); //payload
                                }
                                clientStream.Write(Utils.WriteVarInt(pong.Count)); //packet length
                                clientStream.Write(pong.ToArray()); //packet buffer

                                continue;
                        }

                        remoteStream.Write(buffer);
                        remoteStream.Write(packet_buffer);
                    }
                    else if (!validated) //读取Login Start包，进行白名单验证
                    {
                        validated = true;

                        int packet_length;
                        byte[] buffer;
                        (packet_length, buffer) = Utils.ReadVarInt(clientStream);

                        byte[] packet_buffer = new byte[packet_length]; //login start packet

                        for (int i = 0; i < packet_length; i++)
                        {
                            packet_buffer[i] = (byte)clientStream.ReadByte();
                        }

                        int pos = 0;
                        int packet_id;
                        (packet_id, pos) = Utils.ReadVarInt(packet_buffer, pos);

                        string name; //player name
                        (name, pos) = Utils.ReadString(packet_buffer, pos);

                        Console.WriteLine($"[+] 用户登录: {name}");

                        if (!Validate(name))
                        {
                            SendKick("非白名单用户");
                            Close();
                            break;
                        }

                        ConnectRemote();

                        //handshake packet
                        remoteStream.Write(Utils.WriteVarInt(handshake_buffer.Count));
                        remoteStream.Write(handshake_buffer.ToArray());

                        //login start packet
                        remoteStream.Write(buffer);//packet length
                        remoteStream.Write(packet_buffer);
                    }
                    else
                    {
                        //客户端尝试登录服务器
                        byte[] buffer = new byte[4096];
                        int length = clientStream.Read(buffer, 0, 4096);
                        remoteStream.Write(buffer, 0, length);
                    }
                }
            }
            catch (Exception e)
            {
                Close();
                //Console.WriteLine(e.Message);
                //Console.WriteLine(e.StackTrace);
            }
        }

        private void DownwardThread()
        {
            try
            {
                while (client.Connected && remote.Connected)
                {
                    byte[] buffer = new byte[4096];
                    int length = remote.Receive(buffer);
                    client.Send(buffer, length, SocketFlags.None);
                }
            }
            catch (Exception e)
            {
                Close();
                //Console.WriteLine(e.Message);
                //Console.WriteLine(e.StackTrace);
            }
        }

        private void SendResponse()
        {
            Console.WriteLine($"[*] 客户端 { this.client_ip } ping响应发送成功.");
            List<byte> buffer = new List<byte>();

            //packet id
            buffer.AddRange(Utils.WriteVarInt(0));
            //motd
            buffer.AddRange(Utils.WriteString(motd));

            clientStream.Write(Utils.WriteVarInt(buffer.Count)); //buffer length
            clientStream.Write(buffer.ToArray()); //buffer
        }

        private void SendKick(string reason)
        {
            Console.WriteLine($"[*] 客户端 { this.client_ip } 断开连接发送成功.");
            List<byte> buffer = new List<byte>();

            //packet id
            buffer.AddRange(Utils.WriteVarInt(0));
            //reason
            buffer.AddRange(Utils.WriteString($"\"{reason}\""));

            clientStream.Write(Utils.WriteVarInt(buffer.Count)); //buffer length
            clientStream.Write(buffer.ToArray()); //buffer
        }

        private bool Validate(string name)
        {
            //TODO: 根据用户名验证
            return true;
        }

        private void ConnectRemote()
        {
            //连接远程服务器
            IPHostEntry host = Dns.GetHostEntry(ip);
            IPAddress ipAddress = host.AddressList[0];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            remote = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            remote.Connect(remoteEP);

            if (remote.Connected)
            {
                remoteStream = new NetworkStream(remote);
                new Thread(new ThreadStart(DownwardThread)).Start();
            }
        }

        private void Close()
        {
            Console.WriteLine($"[-] 客户端 { this.client_ip } 断开连接.");
            try
            {
                this.client.Close();
                this.remote.Close();
            }
            catch(Exception e)
            {

            }
        }
    }

    
}
