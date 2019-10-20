using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System;

using SC_Common;
using SC_Common.Enum;

namespace SC_Server.src
{
    internal sealed class Server
    {
        private readonly int MAX_USERS = 10;
        private int ServerPort;
        private IPAddress ServerIP;
        private EndPoint ServerEndPoint;
        private Socket Listener;
        private PackageManager PackageManag;

        public Server()
        {
            PackageManag = new PackageManager();
            PackageManag.HasGotExceptionEvent += ExceptionHandler;
        }

        public void Start(string ip, int port)
        {
            ServerIP = IPAddress.Parse(ip);
            ServerPort = port;

            ServerEndPoint = new IPEndPoint(ServerIP, ServerPort);
            Listener = new Socket(ServerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            Listener.Bind(ServerEndPoint);
            Listener.Listen(MAX_USERS);

            Listener.BeginAccept(new AsyncCallback(AcceptCallBack), null);
        }

        public void Shutdown()
        {
            PackageManag.HasGotExceptionEvent -= ExceptionHandler;
            Listener.Close();
        }

        private void ExceptionHandler(Exception ex, Socket user)
        {
            Console.WriteLine("Socket exception: " + ex.Message);
            CloseUserSocket(user);
        }

        private void AcceptCallBack(IAsyncResult result)
        {
            Socket newUser = Listener.EndAccept(result);
            PackageManag.ReceivePackage(newUser, ReceiveCallback);

            Listener.BeginAccept(new AsyncCallback(AcceptCallBack), null);

            ////NEED_TO_UPDATE
            Console.WriteLine("\n\nConnected new user!");
        }

        private void ReceiveCallback(Socket user, PackageArgs package)
        {
            Console.WriteLine("New package: " + package.Command.ToString() + " | ");

            switch(package.Command)
            {
                case (Command.Exit): CloseUserSocket(user); return;
            }

            PackageManag.ReceivePackage(user, ReceiveCallback);
        }
     
        private void CloseUserSocket(Socket userSocket)
        {
            userSocket.Shutdown(SocketShutdown.Both);
            userSocket.Close();
            Console.WriteLine("User disconnected!");
        }
    }
}
