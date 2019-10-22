using System;
using System.IO;

namespace SC_Common
{
    public class Register
    {
        private string FilePath = "Log.txt";
        public bool IsActive { get; set; }
        
        public Register(bool isActive = true)
        {
            IsActive = isActive;
        }

        public enum LogType : byte
        {
            NaN,
            Normal,
            Warning,
            Error
        }

        public void WriteLog(string message, LogType type = LogType.Normal)
        {
            if (IsActive)
            {
                File.AppendAllText(FilePath, DateTime.Now.ToString('|' + "MM/dd/yyyy HH:mm:ss" + '|'));
                switch (type)
                {
                    case (LogType.Normal): break;
                    case (LogType.Error): File.AppendAllText(FilePath, "::ERROR::"); break;
                    case (LogType.Warning): File.AppendAllText(FilePath, "!WARNING!"); break;
                }
                File.AppendAllText(FilePath, " >> " + message + "\n");
            }
        }
    }
}
