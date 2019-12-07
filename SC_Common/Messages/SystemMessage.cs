using System;

namespace SC_Common.Messages
{
    [Serializable]
    public struct SystemEvent : ChatMessage
    {
        public bool Mode { get; private set; }
        public string Content { get; private set; }

        public SystemEvent(string content, bool mode)
        {
            Content = content;
            Mode = mode;
        }
    }
}
