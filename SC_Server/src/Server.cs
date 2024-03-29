﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;

using SC_Common;
using SC_Common.Enum;
using SC_Common.Messages;
using SC_Server.db.model;

namespace SC_Server.src
{
    internal sealed class Server
    {
        private struct ChatCommand
        {
            private enum TextCommand : byte
            {
                NaN = 0,
                auth,
                pm,
                ban,
                kick
            }

            public Command ParsedCommand;
            public string[] Args;

            public ChatCommand(Command command, string[] args)
            {
                ParsedCommand = command;
                Args = args;
            }

            public static ChatCommand ParseToCommand(string message, Command defaultCmd = Command.NaN)
            {
                string[] parts = message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                TextCommand textCmd;

                Command reusltOfCmd = defaultCmd;
                string[] resultOfArgs = null;

                if (parts[0][0] == ChatCommandHelper.SymbolOfCommand)
                {
                    reusltOfCmd = Command.IvalidCommand;
                    Enum.TryParse(parts[0].Substring(1), out textCmd);

                    switch (textCmd)
                    {
                        case (TextCommand.auth):                            
                            if (parts.Length == 3)
                            {                             
                                reusltOfCmd = Command.Authorization;
                                resultOfArgs = parts.Skip(1).ToArray();
                            }                        
                            break;
                        case (TextCommand.pm):
                            if (parts.Length == 3)
                            {
                                reusltOfCmd = Command.Send_PM;
                                resultOfArgs = parts.Skip(1).ToArray();
                            }
                            break;
                        case (TextCommand.ban):
                            if (parts.Length >= 3)
                            {
                                reusltOfCmd = Command.Ban;
                                resultOfArgs = parts.Skip(1).ToArray();
                                resultOfArgs[1] = string.Join(" ", resultOfArgs.Skip(1));
                                resultOfArgs = resultOfArgs.Take(2).ToArray();
                            }
                            break;
                        case (TextCommand.kick):
                            if (parts.Length >= 3)
                            {
                                reusltOfCmd = Command.Kick;
                                resultOfArgs = parts.Skip(1).ToArray();
                                resultOfArgs[1] = string.Join(" ", resultOfArgs.Skip(1));
                                resultOfArgs = resultOfArgs.Take(2).ToArray();
                            }
                            break;
                    }
                }

                return new ChatCommand(reusltOfCmd, resultOfArgs);
            }
        }
        private class UserInfo
        {
            public int ID { get; private set; }
            public string UserName { get; private set; }
            public User UserModel { get; private set; }
            public Socket UserSocket { get; private set; }

            public UserInfo(int id, string userName, Socket socket)
            {
                ID = id;
                UserName = userName;
                UserModel = null;
                UserSocket = socket;
            }

            public void SetUserModel(User model)
            {
                UserModel = model;
            }
        }

        private readonly int MAX_USERS = 10;
        private readonly int MAX_MESSAGES = 10;

        private int ServerPort;
        private IPAddress ServerIP;
        private EndPoint ServerEndPoint;
        private Socket Listener;
        private PackageManager PackageManag;
        private Task MessageTask;

        private BlockingCollection<ChatMessage> AddingMessageQuery;
        private List<ChatMessage> ChatMessages;
        private Dictionary<Socket, UserInfo> UserInfoBySocket;
        private Dictionary<string, UserInfo> UserInfoByUserName;
        private int MessageIndex;

        public Server()
        {
            UserInfoBySocket = new Dictionary<Socket, UserInfo>();
            UserInfoByUserName = new Dictionary<string, UserInfo>();

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
            string error;
            if (ex != null)
                Console.WriteLine(error = "Socket exception: " + ex.Message);
            else
                Console.WriteLine(error = "Unkonow exception!");       
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

        public void ReceiveCallback(Socket user, PackageArgs package)
        {
            Console.WriteLine("New package: " + package.Command.ToString());

            Command command = package.Command;
            ChatCommand chatCommand = new ChatCommand();

            if (package.Command == Command.Send_Message)
            {
                chatCommand = ChatCommand.ParseToCommand(
                    ((UserMessage)package.Arguments[Argument.MessageObj]).Content, command);
                command = chatCommand.ParsedCommand;
            }                

            switch(command)
            {
                case (Command.User_Setup): UserSetupHandler(user, package); break;
                case (Command.Send_Message): UserSendMeesage(user, package); break;
                case (Command.Get_Last_Messages): GetLastMessages(user); break;
                case (Command.Authorization): UserAuthorization(user, chatCommand); break;
                case (Command.Send_PM): UserSendPM(user, chatCommand); break;
                case (Command.Ban): ActionOnUser(user, chatCommand); break;
                case (Command.Kick): ActionOnUser(user, chatCommand); break;
                case (Command.IvalidCommand): InvalidCommand(user); break;
                case (Command.CheckBlockIP): CheckBlockIP(user, package); break;
                case (Command.Exit): CloseUserSocket(user); return;
                default: Console.WriteLine("Ivalid Package!"); break;
            }

            PackageManag.ReceivePackage(user, ReceiveCallback);
        }        
     
        private void CloseUserSocket(Socket userSocket)
        {
            if (UserInfoBySocket.ContainsKey(userSocket))
            {
                string userName = UserInfoBySocket[userSocket].UserName;

                PackageArgs sendPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.User_Left,
                    Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.UserName, userName}
                    }
                };

                BroadcastMessage(sendPackage, userSocket);

                AddingMessageQuery.Add(new SystemEvent(userName, false));

                UserInfoBySocket.Remove(userSocket);
                UserInfoByUserName.Remove(userName);
            }

            userSocket.Shutdown(SocketShutdown.Both);
            userSocket.Close();
            Console.WriteLine("User disconnected!");
        }      

        private void BroadcastMessage(PackageArgs sendPackage, Socket exeptUser)
        {
            foreach (Socket user in UserInfoBySocket.Keys)
            {
                if (user == exeptUser) continue;               
                PackageManag.SendPackage(user, sendPackage, null);
            }
        }

        #region Request

        private void CheckBlockIP(Socket sender, PackageArgs package)
        {
            PackageArgs sendPackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.CheckBlockIP,
                Arguments = new Dictionary<Argument, object>()
                {
                    { Argument.IsAdmitted, db.DB_Manager.IPInBlackList
                    (package.Arguments[Argument.IP] as string) }
                }
            };

            PackageManag.SendPackage(sender, sendPackage, null);
        }

        private void UserSendMeesage(Socket sender, PackageArgs package)
        {
            UserMessage messageObj = (UserMessage)package.Arguments[Argument.MessageObj];
            AddingMessageQuery.Add(messageObj);

            PackageArgs sendPackage = new PackageArgs()
            {
                PackageType = PackageType.Event,
                Event = Event.New_Message,
                Arguments = new Dictionary<Argument, object>()
                {
                    { Argument.Message, messageObj.Content },
                    { Argument.UserName, messageObj.UserName },
                    { Argument.IsPM, false }
                }
            };

            BroadcastMessage(sendPackage, sender);
        }

        private void UserAuthorization(Socket sender, ChatCommand command)
        {
            PackageArgs responsePackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.CommandResponse,
                Arguments = new Dictionary<Argument, object>()
                    { { Argument.Message, "Invalid login/password" } }
            };

            User user; 
            if(UserInfoBySocket[sender].UserModel != null)
            {
                responsePackage.Arguments[Argument.Message] = "You already accessed.";
            }
            else if((user = db.DB_Manager.Authorized(command.Args[0], command.Args[1])) != null)
            {
                UserInfoBySocket[sender].SetUserModel(user);
                responsePackage.Arguments[Argument.Message] = "Welcome " + user.UserName + ".";
            }            

            PackageManag.SendPackage(sender, responsePackage, null);
        }

        private void UserSendPM(Socket sender, ChatCommand command)
        {
            string toNickname = command.Args[0];
            Socket defaultSocket = sender;

            PackageArgs defaultPackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.CommandResponse,
                Arguments = new Dictionary<Argument, object>()
                    { { Argument.Message, "No any users in chat with nickanme: " + toNickname } }
            };

            if(UserInfoByUserName.ContainsKey(toNickname))
            {
                UserInfo toUser = UserInfoByUserName[toNickname];

                defaultPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.New_PM,
                    Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.Message, command.Args[1] },
                        { Argument.UserName, UserInfoBySocket[sender].UserName },
                        { Argument.IsPM, true }
                    }
                };
                defaultSocket = toUser.UserSocket;
            }
            PackageManag.SendPackage(defaultSocket, defaultPackage, null);
        }

        private void BanRequest(Socket sender, ChatCommand command)
        {
            PackageArgs sendPackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.CommandResponse,
                Arguments = new Dictionary<Argument, object>()
                    { { Argument.Message, "Invalid username." } }
            };

            if(UserInfoBySocket[sender].UserModel == null)
            {
                sendPackage.Arguments[Argument.Message] = "Access denied.";
                PackageManag.SendPackage(sender, sendPackage, null);
            }
            else if (UserInfoByUserName.ContainsKey(command.Args[0]))
            {
                UserInfo bannedUser = UserInfoByUserName[command.Args[0]];
                IPEndPoint localIP = bannedUser.UserSocket.LocalEndPoint as IPEndPoint;

                db.DB_Manager.AddBlockIP(localIP.Address.ToString(), command.Args[1]);

                sendPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.HasBanned,
                    Arguments = new Dictionary<Argument, object>()
                        { { Argument.Message, command.Args[1] } }                    
                };

                PackageManag.SendPackage(bannedUser.UserSocket, sendPackage, null);

                PackageArgs broadcastPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.User_Left,
                    Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.UserName, bannedUser.UserName }
                    }
                };

                BroadcastMessage(broadcastPackage, bannedUser.UserSocket);
            }
            else
            {
                PackageManag.SendPackage(sender, sendPackage, null);
            }
        }

        private void ActionOnUser(Socket sender, ChatCommand command)
        {
            PackageArgs sendPackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.CommandResponse,
                Arguments = new Dictionary<Argument, object>()
                    { { Argument.Message, "Invalid username." } }
            };

            if (UserInfoBySocket[sender].UserModel == null)
            {
                sendPackage.Arguments[Argument.Message] = "Access denied.";
                PackageManag.SendPackage(sender, sendPackage, null);
            }
            else if (UserInfoByUserName.ContainsKey(command.Args[0]))
            {
                UserInfo user = UserInfoByUserName[command.Args[0]];
                IPEndPoint localIP = user.UserSocket.LocalEndPoint as IPEndPoint;

                if(command.ParsedCommand == Command.Ban)
                    db.DB_Manager.AddBlockIP(localIP.Address.ToString(), command.Args[1]);

                sendPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = (command.ParsedCommand == Command.Ban) ? Event.HasBanned : Event.HasKicked,
                    Arguments = new Dictionary<Argument, object>()
                        { { Argument.Message, command.Args[1] } }
                };

                PackageManag.SendPackage(user.UserSocket, sendPackage, null);

                //PackageArgs broadcastPackage = new PackageArgs()
                //{
                //    PackageType = PackageType.Event,
                //    Event = Event.User_Left,
                //    Arguments = new Dictionary<Argument, object>()
                //    {
                //        { Argument.UserName, user.UserName }
                //    }
                //};

                //BroadcastMessage(broadcastPackage, user.UserSocket);
            }
            else
            {
                PackageManag.SendPackage(sender, sendPackage, null);
            }
        }

        //private void KickRequest(Socket sender, ChatCommand command)
        //{
        //    PackageArgs sendPackage = new PackageArgs()
        //    {
        //        PackageType = PackageType.Command,
        //        Command = Command.CommandResponse,
        //        Arguments = new Dictionary<Argument, object>()
        //            { { Argument.Message, "Invalid username." } }
        //    };

        //    if (UserInfoBySocket[sender].UserModel == null)
        //    {
        //        sendPackage.Arguments[Argument.Message] = "Access denied.";
        //        PackageManag.SendPackage(sender, sendPackage, null);
        //    }
        //    else if (UserInfoByUserName.ContainsKey(command.Args[0]))
        //    {
        //        UserInfo kickedUser = UserInfoByUserName[command.Args[0]];
        //        IPEndPoint localIP = kickedUser.UserSocket.LocalEndPoint as IPEndPoint;

        //        sendPackage = new PackageArgs()
        //        {
        //            PackageType = PackageType.Event,
        //            Event = Event.HasKicked,
        //            Arguments = new Dictionary<Argument, object>()
        //                { { Argument.Message, command.Args[1] } }
        //        };

        //        PackageManag.SendPackage(kickedUser.UserSocket, sendPackage, null);

        //        PackageArgs broadcastPackage = new PackageArgs()
        //        {
        //            PackageType = PackageType.Event,
        //            Event = Event.User_Left,
        //            Arguments = new Dictionary<Argument, object>()
        //            {
        //                { Argument.UserName, kickedUser.UserName }
        //            }
        //        };
        //        BroadcastMessage(broadcastPackage, kickedUser.UserSocket);
        //    }
        //    else
        //    {
        //        PackageManag.SendPackage(sender, sendPackage, null);
        //    }
        //}

        private void InvalidCommand(Socket sender)
        {
            PackageArgs responsePackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.CommandResponse,
                Arguments = new Dictionary<Argument, object>()
                    { { Argument.Message, "Invalid command or parameters" } }
            };

            PackageManag.SendPackage(sender, responsePackage, null);
        }

        private void UserSetupHandler(Socket sender, PackageArgs package)
        {
            PackageArgs responsePackage = new PackageArgs()
            {
                PackageType = PackageType.Command,
                Command = Command.User_Setup,
                Arguments = new Dictionary<Argument, object>()
                {
                    { Argument.IsAdmitted, false }
                }
            };

            string userName = package.Arguments[Argument.UserName] as string;
            if (UserInfoByUserName.ContainsKey(userName))
            {
                responsePackage.Arguments.Add(Argument.Message, "Sorry, but \"" + userName + "\" is already taken...");
            }
            else
            {
                UserInfo userInfo = new UserInfo(UserInfoBySocket.Count, userName, sender);
                UserInfoBySocket.Add(sender, userInfo);
                UserInfoByUserName.Add(userInfo.UserName, userInfo);

                responsePackage.Arguments.Add(Argument.UserID, UserInfoBySocket.Count);
                responsePackage.Arguments.Add(Argument.UserName, userName);
                responsePackage.Arguments[Argument.IsAdmitted] = true;

                PackageArgs sendPackage = new PackageArgs()
                {
                    PackageType = PackageType.Event,
                    Event = Event.New_User,
                    Arguments = new Dictionary<Argument, object>()
                    {
                        { Argument.UserName, userName }
                    }
                };

                BroadcastMessage(sendPackage, sender);
                AddingMessageQuery.Add(new SystemEvent(userName, true));
            }
            PackageManag.SendPackage(sender, responsePackage, null);          
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



        #endregion
    }
}
