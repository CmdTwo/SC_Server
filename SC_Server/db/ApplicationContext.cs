using System.Data.SQLite;
using SC_Server.db.model;

using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SC_Server.db
{
    internal static class DB_Manager
    {
        private static BlockingCollection<string> DBQuery;
        private static Task CurrentTask;

        static DB_Manager()
        {
            DBQuery = new BlockingCollection<string>();

            CurrentTask = Task.Factory.StartNew(() =>
            {
                using (SQLiteConnection con = new SQLiteConnection(Properties.Settings.Default.db_ConnectionString))
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(con))
                    {
                        con.Open();
                        foreach (string str in DBQuery.GetConsumingEnumerable())
                        {
                            cmd.CommandText = str;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            },
            TaskCreationOptions.LongRunning);
        }    
        
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
            return null;
        }

        public static void AddBlockIP(string IP, string reason)
        {
            DBQuery.Add("INSERT INTO t_BlockIP(IP, Reason) VALUES ('" + IP + "', '" + reason + "');");
        }

        public static bool IPInBlackList(string IP)
        {
            using (SQLiteConnection con = new SQLiteConnection(Properties.Settings.Default.db_ConnectionString))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(con))
                {
                    con.Open();
                    cmd.CommandText = "SELECT t_BlockIP.ID FROM t_BlockIP where t_BlockIP.IP = '" + IP + "'";
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    reader.Read();
                    return !reader.HasRows;             
                }
            }
            return false;
        }
    }
}
