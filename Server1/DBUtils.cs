using MySql.Data.MySqlClient;

namespace ConnectionDB
{
    class DBUtils
    {
        public static MySqlConnection GetDBConnection()
        {
            string host = "10.192.129.233";
            int port = 3306;
            string database = "server_chats";
            string username = "Andre Linder";
            string password = "gusar1628652470";

            return DBMySQLUtils.GetDBConnection(host, port, database, username, password);
        }

    }
}
