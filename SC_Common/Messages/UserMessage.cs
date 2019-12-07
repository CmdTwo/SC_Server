using System;

namespace SC_Common.Messages
{
    [Serializable]
    public struct UserMessage : ChatMessage
    {
        public int UserID { get; private set; }
        public string UserName { get; private set; }
        public string Content { get; private set; }

        public UserMessage(int id, string name, string message)
        {
            UserID = id;
            UserName = name;
            Content = message;
        }
    }
}
