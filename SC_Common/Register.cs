using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SC_Common
{
    public static class Register
    {
        private static BlockingCollection<string> LogQuery;
        private static Task CurrentTask;
        private static string FilePath = "Log.txt";
        public static bool IsActive { get; set; } = true;

        public enum LogType : byte
        {
            NaN,
            Normal,
            Warning,
            Error
        }

        static Register()
        {
            LogQuery = new BlockingCollection<string>();

            CurrentTask = Task.Factory.StartNew(() =>
            {
                using (StreamWriter writer = new StreamWriter(FilePath, true, Encoding.UTF8))
                {
                    writer.AutoFlush = true;
                    foreach (string str in LogQuery.GetConsumingEnumerable())
                        writer.WriteLine(str);
                }
            },
            TaskCreationOptions.LongRunning);
        }


        public static void WriteLog(string message, LogType type = LogType.Normal)
        {
            if (IsActive)
            {
                string str = DateTime.Now.ToString('|' + "MM/dd/yyyy HH:mm:ss" + '|');                
                switch (type)
                {
                    case (LogType.Normal): break;
                    case (LogType.Error): str += "::ERROR::"; break;
                    case (LogType.Warning): str += "!WARNING!"; break;
                }
                str += " >> " + message;
                LogQuery.Add(str);
            }
        }

        public static void Flush()
        {
            LogQuery.CompleteAdding();
            CurrentTask.Wait();
        }
    }
}
