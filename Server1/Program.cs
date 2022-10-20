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

namespace Server
{
    class Program
    {
        //Объект подключения к БД
        static MySqlConnection connection = DBUtils.GetDBConnection();
        static MySqlConnection connection_async = DBUtils.GetDBConnection();
        static string NewMessage = null;

        static int port = 8005; // порт для приема входящих запросов
        static void Main(string[] args)
        {
            // получаем адреса для запуска сокета
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Any, port);

            
            while (true)
            {
                // создаем сокет
                Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {

                    // связываем сокет с локальной точкой, по которой будем принимать данные
                    listenSocket.Bind(ipPoint);

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
                        byte[] data = new byte[256]; // буфер для получаемых данных

                        do
                        {
                            bytes = handler.Receive(data);
                            builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                        }
                        while (handler.Available > 0);


                        //Первое значение - номер команды. За символом '#' будут идти параметры для команды,
                        //которые будут разделены символом '~' (параметров может не быть)
                        numberCommand = builder.ToString().Split('#')[0].Replace(" ", "");
                        parameters = builder.ToString().Split('#')[1].Replace(" ", "");

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
                                Console.WriteLine(message);
                                break;
                            case "06":
                                message = CheckUnreadMessage_Async(parameters).Result;
                                break;
                            case "07":
                                MarkRead(parameters);
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
                string SQLcommand = "SELECT server_chats.users.ID, server_chats.users.User_Nickname, server_chats.users.User_Password FROM server_chats.users WHERE(User_Nickname = @NICKNAME AND User_Password = @PASSWORD)";

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
                            message = reader.GetString(0) + "~" + reader.GetString(1) + "~" + reader.GetString(2) + "~";
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
                string SQLCommand = "INSERT INTO server_chats.users (User_Name, User_Password, User_Nickname) VALUES (@NAME,@PASSWORD,@LOGIN);";

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
                string sql_cmd = "SELECT * FROM server_chats.chats WHERE (ID_User_1=@ID OR ID_User_2=@ID);";

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
                            message += reader.GetString(0) + "~" + reader.GetString(2) + "~";

                            if (reader.GetString(3) == parameters.Split('~')[0]) message += reader.GetString(4) + "%";
                            else message += reader.GetString(3) + "%";
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
                id.Value = int.Parse(parameters);
                command.Parameters.Add(id);

                using (DbDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        message = "";
                        while (reader.Read())
                        {
                            message += reader.GetString(1) + "~" + reader.GetString(2) + "%";
                        }
                    }
                }
                message = message.Substring(0, message.Length - 1);

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

                //Здесь прописывается логика отображения сообщений в окне дилога
                //У "моих" сообщений и сообщений собеседника будет различное цветовое оформление 
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
                string sql_cmd = "UPDATE server_chats.messages SET Visible_Message = 1 WHERE (ID_Reciever=@MYID AND ID_Sender = @FRIENDID AND Visible_Message = 0) LIMIT 1000";

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

        //Поиск непрочитанных сообщений
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
                                        message += reader.GetString(0) + "~" + reader.GetString(1) + "~" + reader.GetString(2);
                                        //System.Threading.Thread.Sleep(1500);
                                }                                    
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
    }
}
