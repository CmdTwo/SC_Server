using System;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

namespace SC_Common
{
    public class PackageManager
    {
        private BinaryFormatter BinFormatter = new BinaryFormatter();
        private Register LogRegister = new Register(false);
        public Action<Exception, Socket> HasGotExceptionEvent;

        private struct SendState
        {
            public readonly int BUFFER_SIZE;
            public const int FIRST_PACKAGE_SIZE = 4;
            public Socket Handler { get; private set; }
            public byte[] Buffer { get; private set; }
            public int SendBytes { get; set; }
            public int PackagePartCount { get; set; }
            public int PackageHash { get; private set; }
            public bool IsSendMode { get; set; }
            public readonly Action<Socket, int, bool> HasSendCallback;

            public SendState(Socket handler, byte[] bytes, int packageHash, Action<Socket, int, bool> callback)
            {
                BUFFER_SIZE = 4096;
                Handler = handler;
                Buffer = bytes;
                SendBytes = 0;
                PackagePartCount = 0;
                PackageHash = packageHash;
                IsSendMode = false;
                HasSendCallback = callback;
            }
        }
        private struct ReceiveState
        {
            public readonly int BUFFER_SIZE;
            public Socket Handler { get; private set; }
            public byte[] Buffer { get; set; }
            public int ReadBytes { get; set; }
            public int PackageSize { get; set; }
            public int PackagePartCount { get; set; }
            public bool IsReceiveMode { get; set; }
            public readonly Action<Socket, PackageArgs> HasReceivedCallback;

            public ReceiveState(Socket handler, Action<Socket, PackageArgs> callback)
            {
                BUFFER_SIZE = 4096;
                Handler = handler;
                Buffer = new byte[BUFFER_SIZE];
                ReadBytes = 0;
                PackageSize = 0;
                PackagePartCount = 0;
                IsReceiveMode = false;
                HasReceivedCallback = callback;
            }

            public void Reset()
            {
                Buffer = new byte[BUFFER_SIZE];
                ReadBytes = 0;
                PackageSize = 0;
                PackagePartCount = 0;
                IsReceiveMode = false;
            }
        }


        public void SendPackage(Socket handler, PackageArgs package, 
            Action<Socket, int, bool> hasSendCallback)
        {
            byte[] bytes;
            using (MemoryStream ms = new MemoryStream())
            {
                BinFormatter.Serialize(ms, package);
                bytes = ms.ToArray();
            }

            byte[] size = BitConverter.GetBytes(bytes.Length);            
            SendState state = new SendState(handler, bytes, package.GetHashCode(), hasSendCallback);
            LogRegister.WriteLog("Try send Package. Hash: " + state.PackageHash);
            handler.BeginSend(size, 0, size.Length, 0, SendCallback, state);
        }

        private void SendCallback(IAsyncResult result)
        {
            SendState state = (SendState)result.AsyncState;
            int send = state.Handler.EndSend(result);

            if (state.IsSendMode)
            {
                state.SendBytes += send;
                LogRegister.WriteLog("Send bytes " + state.SendBytes + " of "
                    + state.Buffer.Length + " (#" + state.PackageHash + ")...");
            }
            else
            {
                state.IsSendMode = true;
                state.PackagePartCount = state.Buffer.Length / state.BUFFER_SIZE;
                LogRegister.WriteLog("Start sending Package (#" + state.PackageHash + ")");
            }

            if (state.SendBytes != state.Buffer.Length)
            {
                int size = (--state.PackagePartCount <= 0)
                    ? state.Buffer.Length - state.SendBytes : state.BUFFER_SIZE;


                LogRegister.WriteLog("Send next part (" + size + " bytes) of Package (#" + state.PackageHash + ")...");
                state.Handler.BeginSend(state.Buffer, state.SendBytes, size, 0, 
                    new AsyncCallback(SendCallback), state);
                //int size = state.Buffer.Length - state.SendCount;
                //if (size / SendState.BUFFER_SIZE >= 1)
                //    size = SendState.BUFFER_SIZE;
                //debugBox.Dispatcher.Invoke(() => debugBox.Text += "  " + send + " • " + state.SendCount + "|" + size + "\n");

            }
            else
            {
                LogRegister.WriteLog("Sending Package (#" + state.PackageHash + ") complete!");
                state.HasSendCallback?.Invoke(state.Handler, state.PackageHash, true);
            }
        }

        public void ReceivePackage(Socket handler, Action<Socket, PackageArgs> callback)
        {
            ReceiveState state = new ReceiveState(handler, callback);
            LogRegister.WriteLog("Start receiving Packages...");
            handler.BeginReceive(state.Buffer, 0, SendState.FIRST_PACKAGE_SIZE, 0, 
                new AsyncCallback(ReceiveCallBack), state);
        }

        private void ReceiveCallBack(IAsyncResult result)
        {
            ReceiveState state = (ReceiveState)result.AsyncState;
            if(state.IsReceiveMode)
            {
                int read = -1;
                try
                {
                    read = state.Handler.EndReceive(result);
                }
                catch(Exception ex)
                {
                    LogRegister.WriteLog(ex.Message, Register.LogType.Error);
                    HasGotExceptionEvent?.Invoke(ex, state.Handler);
                    return;
                }

                state.ReadBytes += read;
                LogRegister.WriteLog("Read bytes " + read + " of " + state.PackageSize + "...");

                if (state.ReadBytes != state.PackageSize)
                {
                    int size = (--state.PackagePartCount <= 0)
                        ? state.PackageSize - state.ReadBytes : state.BUFFER_SIZE;

                    LogRegister.WriteLog("Ready for read next part (" + size + " bytes)");

                    state.Handler.BeginReceive(state.Buffer, state.ReadBytes, size,
                        0, new AsyncCallback(ReceiveCallBack), state);
                }
                else
                {
                    PackageArgs package;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(state.Buffer, 0, state.PackageSize);
                        ms.Position = 0;
                        package = (PackageArgs)BinFormatter.Deserialize(ms);
                    }
                    LogRegister.WriteLog("Read complete! Package: " + package.PackageType.ToString()
                        + " | " + (package.PackageType == Enum.PackageType.Command 
                        ? package.Command.ToString() : package.Event.ToString()));
                    state.HasReceivedCallback(state.Handler, package);
                }
            }
            else
            {
                state.PackageSize = BitConverter.ToInt32(state.Buffer, 0);
                state.PackagePartCount = state.Buffer.Length / state.BUFFER_SIZE;
                state.Buffer = new byte[state.PackageSize];
                state.IsReceiveMode = true;

                int size = state.Buffer.Length;
                if (size / state.BUFFER_SIZE >= 1)
                    size = state.BUFFER_SIZE;

                try
                {
                    LogRegister.WriteLog("Begin waiting part of Package (" + state.PackageSize + " bytes)");
                    state.Handler.BeginReceive(state.Buffer, 0, size, 0,
                        new AsyncCallback(ReceiveCallBack), state);
                }
                catch (Exception ex)
                {
                    LogRegister.WriteLog(ex.Message, Register.LogType.Error);
                    HasGotExceptionEvent?.Invoke(ex, state.Handler);
                    return;
                }
            }
        }
    }
}
