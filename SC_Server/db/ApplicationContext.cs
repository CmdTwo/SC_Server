using System.Data.SQLite;
using SC_Server.db.model;

namespace SC_Server.db
{
    internal static class DB_Manager
    {
        public static User Authorized(string login, string password)
        {
            using (SQLiteConnection con = new SQLiteConnection(Properties.Settings.Default.db_ConnectionString))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(con))
                {
                    con.Open();
                    cmd.CommandText = "SELECT t_User.ID, t_User.Username FROM t_User where t_User.Login = '" + login + "' and t_User.Password = '" + password + "'";
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    reader.Read();
                    if (reader.HasRows)
                        return new User(reader.GetInt32(0), reader.GetString(1));
                    else
                        return null;
                }
            }
        }
    }
}
