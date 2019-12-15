namespace SC_Server.db.model
{
    internal class User
    {
        public int ID { get; private set; }
        public string UserName { get; private set; }

        public User(int id, string userName)
        {
            ID = id;
            UserName = userName;
        }       
    }
}
