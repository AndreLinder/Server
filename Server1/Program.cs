using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using MySql.Data.MySqlClient;
using ConnectionDB;
using System.Data.Common;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;

namespace Server
{
    class Program
    {
        //Объект подключения к БД
        static MySqlConnection connection = DBUtils.GetDBConnection();
        static MySqlConnection connection_async = DBUtils.GetDBConnection();

        static int port = 8005; // порт для приема входящих запросов
        static void Main(string[] args)
        {
            // получаем адреса для запуска сокет
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse("192.168.50.219"), port);
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);   
            
            while (true)
            {
                //// создаем сокет
                listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    listenSocket.Bind(ipPoint);
                    listenSocket.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
                    // начинаем прослушивание
                    listenSocket.Listen(10);

                    Console.WriteLine("Сервер запущен. Ожидание подключений...");

                    while (true)
                    {
                        //Сообщение для ответа клиенту
                        string message = "ERROR";

                        //Номер команды и её параметры
                        string numberCommand = "";
                        string parameters = "";

                        Socket handler = listenSocket.Accept();
                        // получаем сообщение
                        StringBuilder builder = new StringBuilder();
                        int bytes = 0; // количество полученных байтов
                        byte[] data = new byte[2560000]; // буфер для получаемых данных
                        do
                        {
                            bytes = handler.Receive(data);
                            builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                        }
                        while (handler.Available > 0);
                        //Первое значение - номер команды. За символом '#' будут идти параметры для команды,
                        //которые будут разделены символом '~' (параметров может не быть)
                        numberCommand = builder.ToString().Split('#')[0].Replace(" ", "");
                        parameters = builder.ToString().Split('#')[1];
                        

                        //В зависимости от номера команды, вызываем определенную функцию
                        //При необходимости, передаем параметры
                        switch (numberCommand)
                        {
                            case "01":
                                message = SignIn(parameters);
                                break;
                            case "02":
                                message = SignUp(parameters);
                                break;
                            case "03":
                                message = GetChatList(parameters);
                                break;
                            case "04":
                                message = GetFriendList(parameters);
                                break;
                            case "05":
                                message = GetMessageFromChat(parameters);
                                break;
                            case "06":
                                message = CheckUnreadMessage_Async(parameters).Result;
                                break;
                            case "07":
                                MarkRead(parameters);
                                break;
                            case "08":
                                message = Search_User_By_Name(parameters);
                                break;
                            case "10":
                                message = Recieve_Message_And_Save(parameters);
                                break;
                            case "11":
                                message = Add_To_Friend(parameters);
                                break;
                            case "12":
                                message = CreateNewChat(parameters);
                                break;
                            case "13":
                                message = Delete_Message(parameters);
                                break;
                            case "14":
                                message = Delete_Contact(parameters);
                                break;
                            case "15":
                                message = Create_Group_Chat(parameters);
                                break;
                            case "16":
                                message = Update_Group_Chat(parameters);
                                break;
                            case "17":
                                message = Get_Message_From_Group_Chat(parameters);
                                break;
                            case "18":
                                message = Recieve_And_Save_Message_From_Group(parameters);
                                break;
                        }
                        //Цветовое офрмление серверной части, для наглядности обмена данными
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(DateTime.Now.ToShortTimeString() + ": ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("<" + handler.RemoteEndPoint.ToString() + "> :");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write(builder.ToString().Split('#')[0]);
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write('#');
                        if (builder.ToString().Split('#')[1] != "") foreach (string s in builder.ToString().Split('#')[1].Split('~'))
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.Write(s);
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.Write('~');
                            }
                        Console.WriteLine();

                        // отправляем ответ
                        data = Encoding.Unicode.GetBytes(message);
                        handler.Send(data);

                        //Цветовое офрмление серверной части, для наглядности обмена данными
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(DateTime.Now.ToShortTimeString() + ": ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("<Response Server> : ");
                        foreach (string value in message.Split('~'))
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write(value);
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write('~');
                        }
                        Console.WriteLine();

                        // закрываем сокет
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.ReadKey();
                    listenSocket.Close();
                }
            }
        }

        //Метод авторизации пользователя (#01)
        static string SignIn(string parameters)
        {
            string message = "ERROR";

                //Подключаемся к БД
                connection.Open();
                //Строка запроса на выборку данных из БД
                string SQLcommand = "SELECT server_chats.users.ID, server_chats.users.User_Nickname, server_chats.users.User_Password, server_chats.users.User_GUID FROM server_chats.users WHERE(User_Nickname = @NICKNAME AND User_Password = @PASSWORD)";

                //Создаем объект команды для нашего запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = SQLcommand;

                //Добавляем параметры для нашей команды
                MySqlParameter nick = new MySqlParameter("@NICKNAME", MySqlDbType.VarChar);
                nick.Value = parameters.Split('~')[0];
                cmd.Parameters.Add(nick);

                MySqlParameter password = new MySqlParameter("@PASSWORD", MySqlDbType.VarChar);
                password.Value = parameters.Split('~')[1];
                cmd.Parameters.Add(password);

                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            message = reader.GetString(0) + "~" + reader.GetString(1) + "~" + reader.GetString(2) + "~" + reader.GetString(3) + "~";
                        }
                    }
                }
                connection.Close();

            return message;
        }

        //Метод регистрации нового пользователя (#02)
        static string SignUp(string parameters)
        {
            //Сообщение для вывод ответа от сервера (по умолчаниию "ошибка")
            string message = "ERROR";

            try
            {
                //Открываем соединение с БД
                connection.Open();

                //SQL-запрос на регистрацию нового пользователя
                string SQLCommand = "INSERT INTO server_chats.users (User_Name, User_Password, User_Nickname, User_GUID) VALUES (@NAME,@PASSWORD,@LOGIN, @GUID);";

                //Создаем объект запроса для БД
                MySqlCommand command = connection.CreateCommand();
                command.CommandText = SQLCommand;

                //Добавляем параметры к нашему запросу
                MySqlParameter nick = new MySqlParameter("@LOGIN", MySqlDbType.VarChar);
                nick.Value = parameters.Split('~')[1];
                command.Parameters.Add(nick);

                MySqlParameter password = new MySqlParameter("@PASSWORD", MySqlDbType.VarChar);
                password.Value = parameters.Split('~')[2];
                command.Parameters.Add(password);

                MySqlParameter name = new MySqlParameter("@NAME", MySqlDbType.VarChar);
                name.Value = parameters.Split('~')[0];
                command.Parameters.Add(name);
                MySqlParameter guid = new MySqlParameter("@GUID", MySqlDbType.VarChar);
                guid.Value = System.Guid.NewGuid().ToString();
                command.Parameters.Add(guid);

                //Выполняем запрос
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            finally
            {
                message = "CREATE NEW USER: " + parameters.Split('~')[0];
                connection.Close();
            }

            //Возвращаем ответ сервера
            return message;
        }

        //Метод получения списка чатов (#03)
        static string GetChatList(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Подключаемся к БД
                connection.Open();

                //Строка запроса к БД
                string sql_cmd = "SELECT server_chats.chats.Chat_Name, server_chats.chats.ID, server_chats.chats.GUID, server_chats.users.User_Name, server_chats.chats.ID_User_1,server_chats.chats.ID_User_2 FROM server_chats.chats, server_chats.users WHERE (ID_User_1=@ID AND server_chats.users.ID = ID_User_2) OR (ID_User_2=@ID AND server_chats.users.ID = ID_User_1); ";

                //Создаем команду запроса к БД
                MySqlCommand command = connection.CreateCommand();
                command.CommandText = sql_cmd;

                MySqlParameter id_user = new MySqlParameter("@ID", MySqlDbType.VarChar);
                id_user.Value = parameters.Split('~')[0];
                command.Parameters.Add(id_user);

                using (DbDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        message = "";
                        while (reader.Read())
                        {
                            message += reader.GetString(1) + "~" + reader.GetString(0) + "~" + reader.GetString(2)+ "~" + reader.GetString(3) + "~";

                            if (reader.GetString(4) == parameters.Split('~')[0]) message += reader.GetString(5) + "%";
                            else message += reader.GetString(4) + "%";
                        }
                        message = message.Substring(0, message.Length-1);
                    }
                }

                //Закрываем соединение
                connection.Close();
            }
            catch(Exception ex)
            {
                message = ex.ToString();
            }

            return message;
        }

        //Метод получения списка друзей(#04)
        static string GetFriendList(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Открываем соединение с БД
                connection.Open();

                //Строка запроса к БД
                string sql_cmd = "SELECT * FROM server_chats.friend WHERE ID_User = @ID;";

                //Создаем команду для запроса к БД
                MySqlCommand command = connection.CreateCommand();
                command.CommandTimeout = 200;
                command.CommandText = sql_cmd;

                //Добавляем параметры к нашему запросу
                MySqlParameter id = new MySqlParameter("@ID", MySqlDbType.Int32);
                id.Value = int.Parse(parameters.Split('~')[0]);
                command.Parameters.Add(id);

                using (DbDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        message = "";
                        while (reader.Read())
                        {
                            message += reader.GetString(1) + "~" + reader.GetString(2) + "~" + reader.GetString(3) + "%";
                        }
                        message = message.Substring(0, message.Length - 1);
                    }
                }
                

                //Закрываем соединение с БД
                connection.Close();
            }
            catch(Exception ex)
            {
                //Есть вариант, при вызове исключения к началу сообщения добавлять какую-либо строку, 
                //чтобы клиентское приложение не вызывало исключение "индекс находился вне границ массива"
                message = ex.Message;
            }

            return message;
        }

        //Получение списка сообщений из определенного чата(05#)
        static string GetMessageFromChat(string parameters)
        {
            string message = "ERROR";

            try
            {
                int MID = int.Parse(parameters.Split('~')[0]);
                int FID = int.Parse(parameters.Split('~')[1]);

                //Открываем соединение
                connection.Open();

                //Запрос на выгрузку сообщений (максимум 100)
                string sql_cmd = "SELECT * FROM server_chats.messages WHERE (ID_Sender = @MYID AND ID_Reciever = @IDFRIEND) OR (ID_Sender = @IDFRIEND AND ID_Reciever = @MYID)";

                //Команда запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры
                MySqlParameter myID = new MySqlParameter("@MYID", MySqlDbType.Int32);
                myID.Value = MID;
                cmd.Parameters.Add(myID);

                MySqlParameter friendID = new MySqlParameter("@IDFRIEND", MySqlDbType.Int32);
                friendID.Value = FID;
                cmd.Parameters.Add(friendID);

                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        message = " ";
                        while (reader.Read())
                        {
                                message += reader.GetString(0)+"~"+ reader.GetString(1) + "~"+ reader.GetString(2) + "~"+ reader.GetString(3) + "%";
                        }
                        message = message.Substring(0, message.Length - 1);
                    }
                }

                connection.Close();
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return message;
            }
            return message;
        }

        //Отметка о прочтении сообщений(07#)
        static string MarkRead(string parameters)
        {
            int MID = int.Parse(parameters.Split('~')[0]);
            int FID = int.Parse(parameters.Split('~')[1]);
            try
            {
                //Открываем соединение
                connection.Open();

                //Запускаем запрос на отметку сообщений, как прочитанные
                string sql_cmd = "UPDATE server_chats.messages SET Visible_Message = 1, visible_notification = 1 WHERE (ID_Reciever=@MYID AND ID_Sender = @FRIENDID AND Visible_Message = 0) LIMIT 1000";

                //Создаём команду запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры в запрос
                MySqlParameter myID = new MySqlParameter("@MYID", MySqlDbType.Int32);
                myID.Value = MID;
                cmd.Parameters.Add(myID);

                MySqlParameter friendID = new MySqlParameter("@FRIENDID", MySqlDbType.Int32);
                friendID.Value = FID;
                cmd.Parameters.Add(friendID);

                //Осуществляем запрос
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                //Закрыавем соединение 
                connection.Close();
            }

            return "OK";
        }

        //Поиск непрочитанных сообщений текущего диалога(06#)
        static async Task<string> CheckUnreadMessage_Async(string parameters)
        {
            var message = "ERROR";
            int MID = int.Parse(parameters.Split('~')[0]);
            int FID = int.Parse(parameters.Split('~')[1]);

                using (MySqlConnection conn = connection_async)
                {
                    try
                    {
                        //Открываем соединение
                        await conn.OpenAsync();
                        //Строка запроса
                        string sql_cmd = "SELECT * FROM server_chats.messages WHERE (ID_Reciever = @MYID AND ID_Sender = @FRIENDID AND Visible_Message = 0);";

                        //Команда запроса
                        MySqlCommand cmd = connection_async.CreateCommand();
                        cmd.CommandText = sql_cmd;

                        //Добавляем параметры запроса
                        MySqlParameter myID = new MySqlParameter("@MYID", MySqlDbType.Int32);
                        myID.Value = MID;
                        cmd.Parameters.Add(myID);

                        MySqlParameter friendID = new MySqlParameter("@FRIENDID", MySqlDbType.Int32);
                        friendID.Value = FID;
                        cmd.Parameters.Add(friendID);

                        //Проверяем в БД непрочитанные нами сообщения
                        using (DbDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (reader.HasRows)
                            {
                                message = "";
                                while (reader.Read())
                                {
                                        message += reader.GetString(0) + "~" + reader.GetString(1) + "~" + reader.GetString(2) + "%";
                                }
                            message = message.Substring(0, message.Length - 1);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        message = ex.ToString();
                    }
                    finally
                    {
                        //Закрываем соединение
                        await conn.CloseAsync();
                        conn.Dispose();
                    }
                    
                }
                return message;

                
            
        }


        //Поиск пользователей(09#)
        static string Search_User_By_Name(string parameters)
        {
            string message = "ERROR";
            try
            {
                //Открываем соединение
                connection.Open();

                //Строка запроса на поиск пользователей в БД
                string sql_cmd = "SELECT server_chats.users.ID, server_chats.users.User_Name, server_chats.users.User_Nickname FROM server_chats.users WHERE server_chats.users.User_Name LIKE @NAME";

                //Создаём команду для запроса в БД
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры в команду
                MySqlParameter name_parameter = new MySqlParameter("@NAME", MySqlDbType.VarChar);
                name_parameter.Value = parameters.Split('~')[0]+"%";
                cmd.Parameters.Add(name_parameter);

                //...
                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        message = "";
                        while (reader.Read())
                        {
                            message += reader.GetString(0) + "~" + reader.GetString(1) + "~" + reader.GetString(2) + "%";
                        }
                        message = message.Substring(0, message.Length - 1);
                    }
                }

                connection.Close();
            }
            catch (Exception ex)
            {
                message = ex.Message;  
            }


            return message;
        }

        //Прием сообщения от пользователя(10#)
        static string Recieve_Message_And_Save(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Открываем соединение
                connection.Open();

                //Строка запроса для БД (недописана)
                string sql_cmd = "INSERT INTO server_chats.messages (Text_Message, Date_Message, ID_Sender, ID_Reciever, Visible_Message) VALUES (@TEXT, NOW(), @MYID, @FRIENDID, 0);";

                //Команда запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры
                MySqlParameter text_message = new MySqlParameter("@TEXT", MySqlDbType.Text);
                text_message.Value = parameters.Split('~')[2];
                cmd.Parameters.Add(text_message);

                MySqlParameter myID = new MySqlParameter("@MYID", MySqlDbType.Int32);
                myID.Value = parameters.Split('~')[0];
                cmd.Parameters.Add(myID);

                MySqlParameter friendID = new MySqlParameter("@FRIENDID", MySqlDbType.Int32);
                friendID.Value = parameters.Split('~')[1];
                cmd.Parameters.Add(friendID);

                //Выполняем запрос
                cmd.ExecuteNonQuery();

                sql_cmd = "SELECT MAX(server_chats.messages.ID) FROM server_chats.messages WHERE (Text_Message = @TEXT AND ID_Sender = @MYID AND ID_Reciever = @FRIENDID)";
                cmd.CommandText = sql_cmd;

                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    if(reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            message = reader.GetString(0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                //Закрываем соединение
                connection.Close();
            }

            return message;
        }

        //Добавление пользователя в список друзей(11#)
        static string Add_To_Friend(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Открываем соединение 
                connection.Open();

                //Строка запроса на добавление пользователя в список друзей
                string sql_cmd = "INSERT INTO server_chats.friend VALUES (@MYID, @IDFRIEND, @FRIENDNAME, @NICKNAME);";

                //Команда запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры
                MySqlParameter myID = new MySqlParameter("@MYID", MySqlDbType.Int32);
                myID.Value = parameters.Split('~')[0];
                cmd.Parameters.Add(myID);

                MySqlParameter friendID = new MySqlParameter("@IDFRIEND", MySqlDbType.Int32);
                friendID.Value = parameters.Split('~')[1];
                cmd.Parameters.Add(friendID);

                MySqlParameter name = new MySqlParameter("@FRIENDNAME", MySqlDbType.VarChar);
                name.Value = parameters.Split('~')[2];
                cmd.Parameters.Add(name);

                MySqlParameter nick = new MySqlParameter("@NICKNAME", MySqlDbType.VarChar);
                nick.Value = parameters.Split('~')[3];
                cmd.Parameters.Add(nick);

                //Запускаем команду
                cmd.ExecuteNonQuery();
                message = "OK";
                
            }
            catch(MySqlException e)
            {
                Console.WriteLine(e.Message);
                message = e.Number + "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                message = ex.Message;
            }
            finally
            {
                //Закрываем соединение
                connection.Close();
            }

            return message;
        }


        //Создание нового диалога с пользователем (12#)
        static string CreateNewChat(string parameters)
        {
            string message = "OK";

            try
            {
                string G = System.Guid.NewGuid().ToString();
                //Открываем соединение
                connection.Open();

                //Строка запроса
                string sql_cmd = "INSERT INTO server_chats.chats (GUID, Chat_Name, ID_User_1, ID_User_2) VALUES (@GUID, @CHATNAME,@IDUSER1, @IDUSER2)";

                //Команда запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры в наш запрос
                MySqlParameter name_parameter = new MySqlParameter("@CHATNAME", MySqlDbType.VarChar);
                name_parameter.Value = parameters.Split('~')[0];
                cmd.Parameters.Add(name_parameter);

                MySqlParameter id1 = new MySqlParameter("@IDUSER1", MySqlDbType.Int32);
                id1.Value = int.Parse(parameters.Split('~')[1]);
                cmd.Parameters.Add(id1);

                MySqlParameter id2 = new MySqlParameter("@IDUSER2", MySqlDbType.Int32);
                id2.Value = int.Parse(parameters.Split('~')[2]);
                cmd.Parameters.Add(id2);

                message += "~" + G;
                MySqlParameter guid = new MySqlParameter("@GUID", MySqlDbType.VarChar);
                guid.Value = G;
                cmd.Parameters.Add(guid);

                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {
                message = ex.Message;
                Console.WriteLine(ex.Message);
            }
            finally
            {
                //Закрываем соединение
                connection.Close();
            }

            return message;
        }

        //Удаление сообщения из чата(13#)
        static string Delete_Message(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Открываем соединение
                connection.Open();

                //Строка запроса
                string sql_cmd = "DELETE FROM server_chats.messages WHERE ID = @IDMESSAGE;";

                //Команда запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры
                MySqlParameter messageID = new MySqlParameter("@IDMESSAGE", MySqlDbType.Int32);
                messageID.Value = parameters.Split('~')[0];
                cmd.Parameters.Add(messageID);

                //Выполняем запрос
                cmd.ExecuteNonQuery();
                message = "MESSAGE_DELETE";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                //Закрываем соединение
                connection.Close();
            }

            return message;
        }

        //Удаление пользователя из списка контактов(14#)
        static string Delete_Contact(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Открываем соединение
                connection.Open();

                //Строка запроса на удаление пользователя из друзей
                string sql_cmd = "DELETE FROM server_chats.friend WHERE ID_User = @MYID AND ID_Friend = @IDFRIEND";

                //Создаём команду запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры
                MySqlParameter myID = new MySqlParameter("@MYID", MySqlDbType.Int32);
                myID.Value = parameters.Split('~')[0];
                cmd.Parameters.Add(myID);

                MySqlParameter friendID = new MySqlParameter("@IDFRIEND", MySqlDbType.Int32);
                friendID.Value = parameters.Split('~')[1];
                cmd.Parameters.Add(friendID);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                //Закрываем соединение
                connection.Close();
                message = "USER_DELETED";
            }

            return message;
        }

        //Создание группового чата(#15)
        static string Create_Group_Chat(string parameters)
        {
            string message = "ERROR";

            try
            {
                string G = System.Guid.NewGuid().ToString();
                //Открываем соединение
                connection.Open();

                //Строка запроса
                string sql_cmd = "INSERT INTO server_chats.group_chats (Name_Group, List_ID, GUID) VALUES (@CHATNAME,@IDUSEROWNER, @GUID)";

                //Команда запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры в наш запрос
                MySqlParameter name_parameter = new MySqlParameter("@CHATNAME", MySqlDbType.VarChar);
                name_parameter.Value = "Group by " + parameters.Split('~')[0];
                cmd.Parameters.Add(name_parameter);

                MySqlParameter id1 = new MySqlParameter("@IDUSEROWNER", MySqlDbType.Text);
                id1.Value = "&" + parameters.Split('~')[1] + "$!";
                cmd.Parameters.Add(id1);

                message += "~" + G;
                MySqlParameter guid = new MySqlParameter("@GUID", MySqlDbType.VarChar);
                guid.Value = G;
                cmd.Parameters.Add(guid);

                cmd.ExecuteNonQuery();

                connection.Close();
            }
            catch (Exception ex)
            {
                message = ex.ToString();  
            }

            return message;
        }

        //Обновление списка групповых чатов(16#)
        static string Update_Group_Chat(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Подключаемся к БД
                connection.Open();

                //Строка запроса к БД
                string sql_cmd = "SELECT * FROM server_chats.group_chats WHERE server_chats.group_chats.List_ID LIKE @IDKEY; ";

                //Создаем команду запроса к БД
                MySqlCommand command = connection.CreateCommand();
                command.CommandText = sql_cmd;

                MySqlParameter id_user = new MySqlParameter("@IDKEY", MySqlDbType.VarChar);
                id_user.Value = "%&" + parameters.Split('~')[0] + "$!%";
                command.Parameters.Add(id_user);

                using (DbDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        message = "";
                        while (reader.Read())
                        {
                            message += reader.GetString(0) + "~" + reader.GetString(1) + "~" + reader.GetString(2) + "~" + reader.GetString(3) + "%";
                        }
                        message = message.Substring(0, message.Length - 1);
                    }
                }

                //Закрываем соединение
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return message;
        }

        //Выгрузка сообщений из группового чата(17#)
        static string Get_Message_From_Group_Chat(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Открываем соединение
                connection.Open();

                //Запрос на выгрузку сообщений (максимум 100)
                string sql_cmd = "select server_chats.messages_group_chat.ID, server_chats.messages_group_chat.Text_Message, server_chats.messages_group_chat.ID_Sender, server_chats.messages_group_chat.Date_Message, server_chats.users.User_Name from server_chats.messages_group_chat left join server_chats.users on server_chats.messages_group_chat.ID_Sender = server_chats.users.ID where ID_Chat = @IDCHAT;";

                //Команда запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры
                MySqlParameter ID = new MySqlParameter("@IDCHAT", MySqlDbType.Int32);
                ID.Value = parameters.Split('~')[0];
                cmd.Parameters.Add(ID);

                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        message = " ";
                        while (reader.Read())
                        {
                            message += reader.GetString(0) + "~" + reader.GetString(1) + "~" + reader.GetString(2) + "~" + reader.GetString(3) + "~" + reader.GetString(4) + "%";
                        }
                        message = message.Substring(0, message.Length - 1);
                    }
                }

                connection.Close();
            }
            catch (Exception ex)
            {
                message = ex.ToString();
            }

            return message;
        }


        //Получение и сохранение сообщения определенного группового диалога(18#)
        static string Recieve_And_Save_Message_From_Group(string parameters)
        {
            string message = "ERROR";

            try
            {
                //Открываем соединение
                connection.Open();

                //Строка запроса для БД (недописана)
                string sql_cmd = "INSERT INTO server_chats.messages_group_chat (Text_Message, ID_Sender, ID_Chat, Date_Message) VALUES (@TEXT, @MYID, @IDCHAT, NOW());";

                //Команда запроса
                MySqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = sql_cmd;

                //Добавляем параметры
                MySqlParameter text_message = new MySqlParameter("@TEXT", MySqlDbType.Text);
                text_message.Value = parameters.Split('~')[2];
                cmd.Parameters.Add(text_message);

                MySqlParameter myID = new MySqlParameter("@MYID", MySqlDbType.Int32);
                myID.Value = parameters.Split('~')[1];
                cmd.Parameters.Add(myID);

                MySqlParameter IDchat = new MySqlParameter("@IDCHAT", MySqlDbType.Int32);
                IDchat.Value = parameters.Split('~')[0];
                cmd.Parameters.Add(IDchat);

                MySqlParameter IDvisible = new MySqlParameter("@VID", MySqlDbType.Text);
                IDvisible.Value = "&" + parameters.Split('~')[0] + "$!";
                cmd.Parameters.Add(IDvisible);

                //Выполняем запрос
                cmd.ExecuteNonQuery();

                sql_cmd = "SELECT MAX(server_chats.messages_group_chat.ID) FROM server_chats.messages_group_chat WHERE (Text_Message = @TEXT AND ID_Sender = @MYID)";
                cmd.CommandText = sql_cmd;

                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            message = reader.GetString(0);
                        }
                    }
                }
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                message = ex.Message;
            }

            return message;
        }

        //Не рабочий метод
        ////Прием и сохранение изображения(10#)
        //static string Save_Picture(StringBuilder parameters)
        //{
        //    string message = "OK";

        //    Console.WriteLine(parameters.ToString().Length);

        //    byte[] image = Encoding.Unicode.GetBytes(parameters.ToString());

        //    Console.WriteLine(image.Length);

        //    try
        //    {
        //        connection.Open();

        //        MySqlCommand cmd = connection.CreateCommand();
        //        cmd.CommandText = "INSERT INTO server_chats.user_profile VALUES (4, @IMAGE)";

        //        //using (FileStream fs = new FileStream("C:\\Users\\artur\\OneDrive\\Изображения\\2017-11-27 14-45-44_1609234090589.JPG", FileMode.Open))
        //        //{
        //        //    image = new byte[fs.Length];
        //        //    fs.Read(image, 0, image.Length);
        //        //}

        //        MySqlParameter path = new MySqlParameter("@IMAGE", MySqlDbType.MediumBlob);
        //        path.Value = image;
        //        cmd.Parameters.Add(path);

        //        cmd.ExecuteNonQuery();

        //        connection.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //    }

        //    return message;
        //}
    }
}
