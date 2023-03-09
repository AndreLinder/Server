using MySql.Data.MySqlClient;

namespace ConnectionDB
{
    class DBUtils
    {
        public static MySqlConnection GetDBConnection()
        {
            string host = "192.168.50.234";
            int port = 3306;
            string database = "server_chats";
            string username = "andrelinder";
            string password = "gusar1628652470";

            return DBMySQLUtils.GetDBConnection(host, port, database, username, password);
        }

    }
}
