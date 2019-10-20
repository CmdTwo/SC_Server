using System;

using SC_Server.src;
using SC_Common;

namespace SC_Server
{
    sealed class Source
    {
        private static void Main()
        {
            Server server = new Server();
            server.Start("192.168.100.5", 25252);

            Console.ReadKey();
            server.Shutdown();
        }
    }
}
