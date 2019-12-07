using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.IO;

using SC_Common;
using SC_Common.Enum;
using SC_Common.Messages;

namespace SC_Server.src
{
    internal sealed class Server
    {
        private struct UserInfo
        {
            public int ID { get; private set; }
            public string UserName { get; private set; }

            public UserInfo(int id, string userName)
            {
                ID = id;
                UserName = userName;
            }
        }

        private readonly int MAX_USERS = 10;
        private readonly int MAX_MESSAGES = 10;

        private int ServerPort;
        private IPAddress ServerIP;
        private EndPoint ServerEndPoint;
        private Socket Listener;
        private PackageManager PackageManag;
        private static Task MessageTask;

        private BlockingCollection<ChatMessage> AddingMessageQuery;
        private List<ChatMessage> ChatMessages;
        private Dictionary<Socket, UserInfo> UserInfoDict;
        private int MessageIndex;

        public Server()
        {
            UserInfoDict = new Dictionary<Socket, UserInfo>();
            AddingMessageQuery = new BlockingCollection<ChatMessage>();
            ChatMessages = new List<ChatMessage>(MAX_MESSAGES);
            MessageIndex = MAX_MESSAGES - 1;

            MessageTask = Task.Factory.StartNew(() =>
            {
                foreach (ChatMessage msg in AddingMessageQuery.GetConsumingEnumerable())
                {
                    if(ChatMessages.Count == MAX_MESSAGES)
                        ChatMessages.RemoveAt(0);
                    ChatMessages.Add(msg);
                }
            },
            TaskCreationOptions.LongRunning);

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
                case (Command.Get_Last_Messages): GetLastMessages(user); break;
                case (Command.Exit): CloseUserSocket(user); return;
                default: Console.WriteLine("Ivalid Package!"); break;
            }

            PackageManag.ReceivePackage(user, ReceiveCallback);
        }

        private void GetLastMessages(Socket sender)
        {
            PackageArgs sendPackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.Get_Last_Messages,
                Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.MessageList, ChatMessages }
                    }
            };
            PackageManag.SendPackage(sender, sendPackage, null);
        }

        private void UserSendMeesage(Socket sender, PackageArgs package)
        {
            UserMessage messageObj = (UserMessage)package.Arguments[Argument.MessageObj];
            AddingMessageQuery.Add(messageObj);

            foreach (Socket user in UserInfoDict.Keys)
            {
                if (user == sender) continue;
                PackageArgs sendPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.New_Message,
                    Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.Message, messageObj.Content },
                        { Argument.UserName, messageObj.UserName }
                    }
                };
                PackageManag.SendPackage(user, sendPackage, null);
            }
        }

        private void UserSetupHandler(Socket sender, PackageArgs package)
        {
            UserInfo userInfo = new UserInfo(UserInfoDict.Count,
                package.Arguments[Argument.UserName] as string);

            UserInfoDict.Add(sender, userInfo);

            PackageArgs responsePackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.User_Setup,
                Arguments = new Dictionary<Argument, object>()
                {
                    { Argument.UserName, package.Arguments[Argument.UserName] },
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
                        { Argument.UserName, UserInfoDict[sender].UserName }
                    }
                };
                PackageManag.SendPackage(user, sendPackage, null);
            }

            AddingMessageQuery.Add(new SystemEvent(userInfo.UserName, true));
        }
     
        private void CloseUserSocket(Socket userSocket)
        {
            string userName = UserInfoDict[userSocket].UserName;

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
                        { Argument.UserName, userName}
                    }
                };
                PackageManag.SendPackage(user, sendPackage, null);
            }
            //TEMP

            AddingMessageQuery.Add(new SystemEvent(userName, false));

            UserInfoDict.Remove(userSocket);
            userSocket.Shutdown(SocketShutdown.Both);
            userSocket.Close();
            Console.WriteLine("User disconnected!");
        }
    }
}
