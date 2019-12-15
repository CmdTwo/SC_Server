using System;
using System.Collections.Generic;
using SC_Server.src;

using SC_Common;
using SC_Common.Enum;
using SC_Common.Messages;

namespace SC_Server
{
    

    sealed class Source
    {
        private static void Main()
        {
            Server server = new Server();
            server.Start("192.168.100.4", 25252);

            //PackageArgs sendPackage = new PackageArgs()
            //{
            //    PackageType = PackageType.Command,
            //    Command = Command.Send_Message,
            //    Arguments = new Dictionary<Argument, object>()
            //    {
            //        { Argument.MessageObj, new UserMessage(0, "ff", "!auth fff fff") },
            //    }
            //};

            //server.ReceiveCallback(null, sendPackage);

            //PackageArgs sendPackage = new PackageArgs()
            //{
            //    PackageType = PackageType.Command,
            //    Command = Command.Send_Message,
            //    Arguments = new Dictionary<Argument, object>()
            //    {
            //        { Argument.MessageObj, new UserMessage(0, "ff", "auth fff fff") },
            //    }
            //};

            //server.ReceiveCallback(null, sendPackage);

            Console.Read();
            server.Shutdown();
        }
    }
}
