using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TwitchScanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpClient Client;
        NetworkStream NwStream;
        StreamReader Reader;
        StreamWriter Writer;
        Thread Listen;
        SQLiteConnection m_dbConnection;

        public MainWindow()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
        }

        public void OnProcessExit(object sender, EventArgs e)
        {
            if (Client != null)
                Client = null;
            if (NwStream != null)
                NwStream = null;
            if (Reader != null)
                Reader = null;
            if (Writer != null)
                Writer = null;
            if (m_dbConnection != null)
                m_dbConnection = null;

            if (Listen != null)
                Listen.Abort();

            Environment.Exit(0);
        }

        public void chatListen()
        {
            string[] ex;
            string[] user;
            string data     = "";
            string question = "";
            string answer   = "";

            Dictionary<string, string> dictionary =
                new Dictionary<string, string>();

            bool captureAnswers = false;
            bool answerProvided = false;

            while ((data = Reader.ReadLine()) != null)
            {
                char[] charSeparator = new char[] { ' ' };
                char[] userSeparator = new char[] { '!' };
                user = data.Split(userSeparator);
                ex = data.Split(charSeparator, 4);

                if (ex[0] == "PING")
                {
                    Writer.WriteLine("PONG", ex[1]);
                    Writer.Flush();
                    updateDebug("PONG! Yes, twitch, i'm still here.");
                }

                if (ex[1] == "366")
                    updateDebug("Authentication done!");

                string commands = ex[1];

                if (ex[1] == "PRIVMSG")
                {
                    switch (ex[3].Substring(1))
                    {
                            /*
                        case "!p1raten":
                            this.Dispatcher.Invoke((Action)(() =>
                            {
                                Writer.WriteLine("PRIVMSG #" + text_channel.Text + " :P1raten is awesome!");
                                Writer.Flush();
                            }));
                            break;
                        case "!nwnina":
                            this.Dispatcher.Invoke((Action)(() =>
                            {
                                Writer.WriteLine("PRIVMSG #" + text_channel.Text + " :Hi Nwnina!");
                                Writer.Flush();
                            }));
                            break;
                        case "!fahzblazeit":
                            this.Dispatcher.Invoke((Action)(() =>
                            {
                                Writer.WriteLine("PRIVMSG #" + text_channel.Text + " :Fahzblazeit wants to fap really bad to BoxBox!");
                                Writer.Flush();
                            }));
                            break;
                             */
                        /*default:
                            updateDebug(ex[3].Substring(1));
                            break;*/
                    }

                    if (captureAnswers == true)
                        if (!dictionary.ContainsKey(user[0].Substring(1)))
                            dictionary.Add(user[0].Substring(1), ex[3].Substring(1));

                    // If BoxBoxBot said something
                    if (user[0].Substring(1) == "boxboxbot")
                    {
                        // If there's a new question
                        if (ex[3].Substring(1).Contains("Next question is"))
                        {
                            question = ex[3].Substring(19);

                            updateDebug("New Question: " + question);

                            m_dbConnection.Open();

                            //string sql = "SELECT * FROM questions WHERE question='" + question + "'";
                            string sql = "SELECT * FROM questions WHERE question=@question";

                            //updateDebug(sql);

                            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);

                            command.Parameters.Add(new SQLiteParameter("@question", question));

                            int rows = command.ExecuteNonQuery();
                            SQLiteDataReader reader = command.ExecuteReader();

                            int i = 0;

                            while (reader.Read())
                            {
                                answer = (string)reader["answer"];
                                i++;
                            }

                            m_dbConnection.Close();

                            if (i != 0)
                            {
                                Random rnd = new Random();
                                int sleepTime = rnd.Next(2, 10);
                                sleepTime = sleepTime * 100;
                                Thread.Sleep(sleepTime);
                                updateDebug("I know the answer! It's " + answer);
                                Writer.WriteLine("PRIVMSG #flosd :" + answer);
                                Writer.Flush();
                            }
                            else
                                captureAnswers = true;
                        }
                        else
                        {
                            //updateDebug("BoxBoxBot said:" + ex[3].Substring(1));
                        }

                        // Question time is over
                        if (ex[3].Substring(1).Contains("seconds to next question.") || ex[3].Substring(1).Contains("Next question in"))
                        {
                            if (answerProvided == true)
                                continue;

                            captureAnswers = false;

                            updateDebug("An answer was provided!");

                            // No one answered correctly
                            if (ex[3].Substring(1).Contains("No one answered correctly."))
                            {
                                updateDebug("People are incompetent");
                                
                                Regex regex = new Regex(@"was : (.*?)\.");
                                Match match = regex.Match(ex[3].Substring(1));

                                if (match.Success)
                                {
                                    string realAnswer = match.Value.Substring(6, match.Value.Length - 7);

                                    updateDebug("Answer was: " + realAnswer); // Answer

                                    //question = question.Replace("'", "''");
                                    //realAnswer = realAnswer.Replace("'", "''");

                                    m_dbConnection.Open();

                                    //string sql = "INSERT INTO questions (question, answer) values('" + question + "', '" + realAnswer + "')";
                                    string sql = "INSERT INTO questions (question, answer) values(@question, @answer)";
                                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);

                                    command.Parameters.Add(new SQLiteParameter("@question", question));
                                    command.Parameters.Add(new SQLiteParameter("@answer", realAnswer));

                                    command.ExecuteNonQuery();

                                    m_dbConnection.Close();

                                }
                                else
                                    updateDebug("Can't find answer. :(");

                                dictionary.Clear();
                            }
                            else
                            {
                                updateDebug("People actually know their shit");

                                Regex regex = new Regex(@"to (.*?)\(");
                                Match match = regex.Match(ex[3].Substring(1));

                                if (match.Success)
                                {
                                    string answererer = match.Groups[0].Value.Substring(3, match.Groups[0].Value.Length - 4);

                                    if (answererer == "p1raten")
                                        continue;

                                    updateDebug(answererer + " answered correctly.");

                                    string value;

                                    dictionary.TryGetValue(answererer, out value);

                                    //question = question.Replace("'", "''");
                                    //value = value.Replace("'", "''");

                                    updateDebug("Answer was: " + value); // Answer

                                    m_dbConnection.Open();

                                    //string sql = "INSERT INTO questions (question, answer) values('" + question + "', '" + value + "')";
                                    string sql = "INSERT INTO questions (question, answer) values(@question, @answer)";
                                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);

                                    command.Parameters.Add(new SQLiteParameter("@question", question));
                                    command.Parameters.Add(new SQLiteParameter("@answer", value));

                                    command.ExecuteNonQuery();

                                    m_dbConnection.Close();
                                }
                                else
                                    updateDebug("Could not find who answered correctly. :(");

                                dictionary.Clear();
                            }
                        }
                    }
                }
            }
        }

        public void updateDebug(string message)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<string>(UpdateUI), message);
        }

        public void UpdateUI(string message)
        {
            new TextRange(debug_info.Document.ContentStart, debug_info.Document.ContentEnd).Text += message;
            debug_info.ScrollToEnd();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            updateDebug("Connecting to Server...");
            Client = new TcpClient("irc.twitch.tv", 6667);
            NwStream = Client.GetStream();
            Reader = new StreamReader(NwStream);
            Writer = new StreamWriter(NwStream);

            updateDebug("Connection Succeeded!");

            updateDebug("Let's connect to the DB as well!");

            m_dbConnection = new SQLiteConnection("Data Source=BoxBoxBot.sqlite;Version=3");

            if (m_dbConnection != null)
                updateDebug("Connection successful!");
            else
                updateDebug("Something went horribly wrong! D:");

            updateDebug("Let's authenticate...");

            Writer.WriteLine("USER " + text_nickname.Text + "tmi twitch :" + text_nickname.Text);
            Writer.Flush();

            Writer.WriteLine("PASS " + password.Text);//text_password.Password);
            Writer.Flush();

            Writer.WriteLine("NICK " + text_nickname.Text);
            Writer.Flush();

            Writer.WriteLine("JOIN #" + text_channel.Text + "\n");
            Writer.Flush();

            updateDebug("Joined " + text_channel.Text);

            Listen = new Thread(new ThreadStart(chatListen));
            Listen.Start();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            SQLiteConnection.CreateFile("BoxBoxBot.sqlite");
            updateDebug("Database Created!");

            m_dbConnection = new SQLiteConnection("Data Source=BoxBoxBot.sqlite;Version=3");

            m_dbConnection.Open();

            string sql = "CREATE TABLE questions (question VARCHAR(100), answer VARCHAR(25))";

            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);

            command.ExecuteNonQuery();

            m_dbConnection.Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }
    }
}