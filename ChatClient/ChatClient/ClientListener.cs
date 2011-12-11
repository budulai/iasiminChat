﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace Chat
{
    class ClientListener
    {
        private static string ip = "127.0.0.1";

        public int port = 10000; //TODO Change to Random Port

        private ChatClient _chatClient = null;

        public ClientListener(ChatClient chatClient)
        {
            if (chatClient == null)
                throw new NullReferenceException("Invalid chatClient parameter");
            _chatClient = chatClient;
        }

        public void Run()
        {
            bool isAvailable = false;

            IPGlobalProperties ipGlobalProperties =
                IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] endPoints =
                ipGlobalProperties.GetActiveTcpListeners();

            do
            {
                port++;
                isAvailable = true;
                foreach (IPEndPoint endPoint in endPoints)
                {
                    if (endPoint.Port != port) continue;
                    isAvailable = false;
                    break;
                }

            }
            while (!isAvailable);


            try
            {
                Console.WriteLine("Port : {0}",port);
                TcpListener server = new TcpListener(IPAddress.Parse(ip), port);

                server.Start(1000);

                Console.WriteLine("Server started running at {0}:{1}\n", ip, port);

                new Thread(() => ListenToConnections(server)).Start();


//                Console.WriteLine("Client Listener Started");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error on Client Listener: " + ex);
            }
        }

        private void ListenToConnections(TcpListener server)
        {
            while (true)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();

                    (new Thread(() => TreatConnection(client))).Start();

                    //TODO modify Exception catching
                }
                catch (SocketException se) { Console.WriteLine("SocketException: {0}", se); }
                catch (InvalidOperationException ioe) { Console.WriteLine("InvalidOperationException: {0}", ioe); }
                catch (OutOfMemoryException oome) { Console.WriteLine("OutOfMemoryException: {0}", oome); }
                catch (ThreadStateException tse) { Console.WriteLine("ThreadStateException: {0}", tse); }
                catch (ArgumentNullException ane) { Console.WriteLine("ArgmunetNullException: {0}", ane); }
            }

        }

        private void TreatConnection(TcpClient client)
        {

            Stream stream = client.GetStream();
            int operation = stream.ReadByte();

            byte[] b = null;

            if (operation < 100) //Then is NotifyOperation
            {
                string username = ReadString(stream);

                switch ((NotifyOperation)operation)
                {
                    case NotifyOperation.LogIn:
                        string host = ReadString(stream);
                        int friendPort = ReadInt(stream);
                        _chatClient.UpdateFriendAddress(username, host, friendPort);
                        Console.WriteLine("Login Notif");
                        //TODO change number to enum
                        _chatClient.Notify(2, null);
                        break;
                    case NotifyOperation.ChangeStatus:
                        string statusMessage = ReadString(stream);
                        _chatClient.UpdateFrindStatus(username, "online", statusMessage);
                        Console.WriteLine("Change Status Notif");
                        _chatClient.Notify(2, null);
                        break;
                    case NotifyOperation.LogOut:
                        _chatClient.UpdateFrindStatus(username, "offline");
                        Console.WriteLine("Offline Notif");
                        _chatClient.Notify(2, null);
                        break;
                    case NotifyOperation.FriendRequest:
                            stream.WriteByte(1); //Success
                        //Get Confirmation
                        break;
                    default:
//                        string message = ReadString(stream);
                        Console.WriteLine("Other operation: " + operation);
                        Console.WriteLine("Data: " + username);
                        break;
                }
            }
            else
            {
                switch ((ClientOperation)operation)
                {
                    case ClientOperation.ReceiveMessage:
                        {
                            int length = stream.ReadByte();
                            string username = ReadString(stream);
                            string message = ReadString(stream);
                            Console.WriteLine("Received Message from another client (me: {0})", port);

                            if (_chatClient.ReceiveMessage != null)
                            {
                                _chatClient.ReceiveMessage(username, message);
                            }
                        }
                        break;
                    case ClientOperation.ReceiveFile:
                        {
                            //TODO maybe read username
                            string filename = ReadString(stream);
                            long size = ReadLong(stream);
                            if (_chatClient.ConfirmatFileReceivement != null && _chatClient.ConfirmatFileReceivement(filename, size))
                            {

                                string path = _chatClient.GetPath(filename);
                                //TODO check if path ok
                                //TODO load file from stream + check if is not directory

                                try
                                {
                                    Console.WriteLine("Receiving file");
                                    FileStream fileStream = new FileStream(path + filename, FileMode.Create, FileAccess.Write);
                                    //TODO change all for little indian

                                    b = new byte[size];

                                    long length = size;

                                    int k = 0, offset = 0;
                                    while (length > 0)
                                    {
                                        k = stream.Read(b, offset, (int)length);
                                        length -= k;
                                        offset += k;
                                        Thread.Sleep(10);
                                        Console.WriteLine("Received: " + k);
                                        //TODO + abort
                                    }
                                    fileStream.Write(b, 0, b.Length);
                                    fileStream.Close();
                                    fileStream.Dispose(); 
                                    Console.WriteLine("Successfully received");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }

                        }
                        break;
                }
            }
            

            client.Close();
        }

        private int ReadInt(Stream stream)
        {
            byte[] ba = new byte[4];
            stream.Read(ba, 0, 4);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(ba);

            return BitConverter.ToInt32(ba, 0);
        }

        private long ReadLong(Stream stream)
        {
            byte[] ba = new byte[8];
            stream.Read(ba, 0, 8);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(ba);

            return BitConverter.ToInt64(ba, 0);
        }

        private string ReadString(Stream stream)
        {
            int length = stream.ReadByte();
            byte[] b = new byte[length];
            stream.Read(b, 0, length);

            return Encoding.UTF8.GetString(b);
        }

    }
}

