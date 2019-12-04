using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

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

        private struct UserInfo
        {
            public int ID { get; private set; }
            public string Nickname { get; private set; }

            public UserInfo(int id, string nickname)
            {
                ID = id;
                Nickname = nickname;
            }
        }
        private Dictionary<Socket, UserInfo> UserInfoDict;

        public Server()
        {
            PackageManag = new PackageManager();
            PackageManag.HasGotExceptionEvent += ExceptionHandler;
            UserInfoDict = new Dictionary<Socket, UserInfo>();
        }

        public void Start(string ip, int port)
        {
            ServerIP = IPAddress.Parse(ip);
            ServerPort = port;

            ServerEndPoint = new IPEndPoint(ServerIP, ServerPort);
            Listener = new Socket(ServerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            Listener.Bind(ServerEndPoint);
            Listener.Listen(MAX_USERS);

            Console.WriteLine("Server start: " + ip + ":" + port);

            Listener.BeginAccept(new AsyncCallback(AcceptCallBack), null);
        }

        public void Shutdown()
        {
            Register.Flush();
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
            Register.WriteLog("Accept new connection");
            Console.WriteLine("\n\nConnected new user!");
        }

        private void ReceiveCallback(Socket user, PackageArgs package)
        {
            Console.WriteLine("New package: " + package.Command.ToString());

            switch(package.Command)
            {
                case (Command.User_Setup): UserSetupHandler(user, package); break;
                case (Command.Send_Message): UserSendMeesage(user, package); break;
                case (Command.Exit): CloseUserSocket(user); return;
                default: Console.WriteLine("Ivalid Package!"); break;
            }

            PackageManag.ReceivePackage(user, ReceiveCallback);
        }

        private void UserSendMeesage(Socket sender, PackageArgs package)
        {
            foreach (Socket user in UserInfoDict.Keys)
            {
                if (user == sender) continue;
                PackageArgs sendPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.New_Message,
                    Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.Message, package.Arguments[Argument.Message] },
                        { Argument.Nickname, UserInfoDict[sender].Nickname }
                    }
                };
                PackageManag.SendPackage(user, sendPackage, null);
            }
        }

        private void UserSetupHandler(Socket sender, PackageArgs package)
        {
            UserInfo userInfo = new UserInfo(UserInfoDict.Count,
                package.Arguments[Argument.Nickname] as string);

            UserInfoDict.Add(sender, userInfo);

            PackageArgs responsePackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.User_Setup,
                Arguments = new Dictionary<Argument, object>()
                {
                    { Argument.Nickname, package.Arguments[Argument.Nickname] },
                    { Argument.UserID, userInfo.ID }
                }
            };

            PackageManag.SendPackage(sender, responsePackage, null);


            //TEMP
            foreach (Socket user in UserInfoDict.Keys)
            {
                if (user == sender) continue;
                PackageArgs sendPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.New_User,
                    Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.Nickname, UserInfoDict[sender].Nickname }
                    }
                };
                PackageManag.SendPackage(user, sendPackage, null);
            }
        }
     
        private void CloseUserSocket(Socket userSocket)
        {
            //TEMP
            foreach (Socket user in UserInfoDict.Keys)
            {
                if (user == userSocket) continue;
                PackageArgs sendPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.User_Left,
                    Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.Nickname, UserInfoDict[userSocket].Nickname }
                    }
                };
                PackageManag.SendPackage(user, sendPackage, null);
            }
            //TEMP

            UserInfoDict.Remove(userSocket);
            userSocket.Shutdown(SocketShutdown.Both);
            userSocket.Close();
            Console.WriteLine("User disconnected!");
        }
    }
}
