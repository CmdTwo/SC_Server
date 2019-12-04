using System;
using SC_Server.src;

namespace SC_Server
{
    sealed class Source
    {
        private static void Main()
        {
            Server server = new Server();
            server.Start("192.168.100.2", 25252);

            Console.ReadKey();
            server.Shutdown();
        }
    }
}
