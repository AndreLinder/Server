using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using MySql.Data.MySqlClient;
using ConnectionDB;
using System.Data.Common;
using System.Collections.Generic;

namespace Server
{
    class Program
    {
        //Объект подключения к БД
        static MySqlConnection connection = DBUtils.GetDBConnection();

        static int port = 8005; // порт для приема входящих запросов
        static void Main(string[] args)
        {
            // получаем адреса для запуска сокета
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse("10.192.129.233"), port);

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
                    numberCommand = builder.ToString().Split('#')[0];
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
                    if(builder.ToString().Split('#')[1] != "") foreach(string s in builder.ToString().Split('#')[1].Split('~'))
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
                    foreach(string value in message.Split('~'))
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
    }
}
