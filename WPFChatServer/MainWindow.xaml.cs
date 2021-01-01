/*
 * Chat Server
 * --------------------------------------------------
 * Author: Jon Walker - jlwalker.dev@gmail.com
 * License: None - use as you wish
 * 
 * 
 * Website Acknowledgements
 * --------------------------------------------------
 * Chat Server URLs
 * http://csharp.net-informations.com/communications/csharp-chat-server-programming.htm
 * https://www.c-sharpcorner.com/article/chat-server-with-client-implemented-with-C-Sharp/
 * https://www.codeproject.com/Articles/16023/Multithreaded-Chat-Server
 * https://stackoverflow.com/questions/43431196/c-sharp-tcp-ip-simple-chat-with-multiple-clients
 * http://www.eng.northampton.ac.uk/~espen/CSY2026/CSY2026CSharp5.htm
 * https://github.com/AdrienPoupa/chat
 * 
 * StackOverflow Questions
 * https://stackoverflow.com/questions/4874371/how-to-check-if-any-word-in-my-liststring-contains-in-text
 * https://stackoverflow.com/questions/3303420/regex-to-remove-all-special-characters-from-string
 * https://stackoverflow.com/questions/11293827/find-a-value-from-a-hashtable
 * https://stackoverflow.com/questions/9854917/how-can-i-find-a-specific-element-in-a-listt
 * 
 * 
 * "If someone is giving it away, take it!"
 *      --- S. Jobs
 *      
 * "Good developers borrow ideas from others.  Great developers steal an entire system and call it theirs."  
 *      --- B. Gates
 *      
 * "Crush your enemies, see them bankrupted before you, and hear the lamentation of their stockholders"
 *     --- J. Bezos
 *
 * 
 * Description
 * --------------------------------------------------
 * WPF form that waits for clients to connect and when
 * a client connects, starts up a thread for each.
 * 
 * A seperate thread looks to see if anyone has disconnected
 * and will clean up after if it finds one.
 * 
 * 
 * History
 * --------------------------------------------------
 *  11/11/2020 - Ver 1.0
 *      First take.  Took one day to convert from the console application and make it much more powerful and capable.
 * 
 * 11/12/2020
 *      Added timeout and cleaned up some code.
 *      Made form noresize because I don't want to deal with it right now.
 *      
 *      Added debuglevel control to display
 *      Added config file support
 *      
 *  11/13/2020
 *      Added AWAY, IGNORE & WHO commands
 *      
 *      I'm pretty sure I'm not going to bother with registration or password.  I have no real world use for that and would
 *      want input from a stakeholder on how it should be handled.
 *
 * 11/17/2020 - Ver 1.1
 *      Confirmed you can put a class into a hash table but did not find a good enough reason to do that with the current 
 *      logic.  The userlist needs to be independant of the thread list so that we can easily look up to/from
 *      references.  I'll study the code later to determine if I can make changes that will improve the code
 *      by putting the Users class into the clientHash table.
 *      
 *      Created room logic and everything for control of the room is contained in a class stored in
 *      the roomHash value.  Room name is the key and rooms are all caps, under 15 charcters and
 *      alphanumberic with underscore and dash allowed as part of the room name.
 *      
 *      This is really becoming a nice local chat server that nobody will every use outside of acedamic reasons, but
 *      it's here to help anyone who is interested.
 *      
 *      I've been creating the User.instanceID from what's passed through the HELLO from the remote client along with their IP.
 *      I just don't see that as correct.  I think the chat server should create the instanceID from an incrementing value 
 *      (or any other unique value... take your pick) and then use code to get the remote IP from the connection.  The remote 
 *      information passed from the client will be logged for debug purposes, but not saved anywhere else.
 *      
 *      Cleaned up private messaging and finished HELLO processing.  
 *      We're about ready to release it into the wild.
 *      
 *  11/18/2020
 *      Expanded room control
 *      Added seating for public and private seating
 *      Updated help system to handle subtopics
 *      Added kick command
 *      Fixed some bugs in displaying messages
 *      Added help files
 *      
 *  11/26/2020 - Ver 1.2
 *      Created cmdInfo for passing information through the system - should I just user REF?  Need to look that up.
 *      Added ability to create an Admin without a registered admin (/ELEVATE)
 *      Added several Admin commands
 *      Added ability to accept a client that doesn't send a hello, so just about any compatible client will work
 *      Broke commands out into seperate classes to simplify ChatServer.cs
 *      Added a notification system for tracking changes in the user class so they can be passed back to the user list
 *      Cleaned up a lot of small issues
 *      
 *  11/30/2020
 *      Added room word blacklist
 *  
 *  12/01/2020
 *      Added save/load room profile - each room is in a separate table using the room name
 *      and can be loaded by the room admin or a moderator.  You cannot create a room that
 *      already has a profile saved.
 *      
 *      Moved all admin/moderator references over to RegisteredID instead of username
 *      Added Names2IDs and IDs2Names methods in registration system - string passed in format /x/x/x../
 *      
 *  12/02/2020
 *      Added Forum controls and cleaned up who can speak in a forum room
 *      
 *  12/03/2020 - Ver 1.3
 *      Added SMTP send for sending verify code and RESEND command and
 *      cleaned up verification code
 *      Cleaned up all .Field[] references to .GetField() and SetField() so that we're using 
 *      field names instead of numbers.
 *      Got rid of old data file and retested creation of new data file
 *      Tested REG, RESEND, VERIFY, ELEVATE, ENTER, RMLOAD, RMSAVE again and looking pretty good, a few changes.
 *      Need to make a worksheet with commands and testing parameters
 *      CHANGE (MOD, PASSWORD, TOPIC, SEATS) tested
 *      Added SPEAKER to set up speaker list
 *      
 *      
 *  12/04/2020  
 *      Added xml config support for registration and smtp
 *      
 *  12/05/2020
 *      Misc cleanup and commenting
 *      Ready for Alpha release - time to finish client end
 *      
 *  12/12/2020
 *      Fleshed a lot of help files and mods to be made
 *      
 *  12/16/2020 - Ver 1.4
 *      Finished up the commands and now lots of testing to do.  Major change in that Registration class in 
 *      CommandsForRegistration is also passed to the CommandsForRooms class.  
 *      
 *      Added several passthrough functions in Registration to access features of the SmallDataHandler class.
 *      
 *      Need to wrap this thing up.  It's consuming me and there are a lot more  projects to work on.  
 *      Remember:  The quest for perfection consumes resources and destroys projects.
 *      
 *      Most recent completed ToDos
 *         Create the Registration.xml if it does not exist and put in a short elevation password to turn it off
 *         Only verified users can leave the lobby
 *         Setting your away message prevents you from sending a message
 *         USERRIGHTS cannot add or delete the ADMIN flag
 *         Get the log deletion logic from ComPack
 *         Usernames and Nicknames must start with a letter 
 *         Create /EPASS command so sysadmin can change PW in Registration.xml
 *         Ignore does not work in the lobby
 *         Finish REGLIST command with fullname, nickname, email used with range specifiers
 *         Modify REGLIST so you can specify a field to search  /REGLIST FIELD / startrange - endrange and assumes full name if not supplied
 *         STATUS command - Cannot set an unverified user to A,I,P or V  - I=inactive verified and N=inactive unverified
 *         Finish BAN
 *         Change ignore to work on registered IDs and carries on from session to session
 *         
 *  12/25/20
 *      Fixed the nagging problem with reporting the last on date/time
 *      Added version reporting
 *      Added exit code of 1 if chat server is still running when exiting the application
 *  
 *  01/01/2020
 *      Time to put this to bed and publish
 *      
 * -------------------------------------------------------------------------------------------------------------------------------
 * 
 *      Could be a fun base for educational purposes.  Nothing here should be beyond the level of a second semester student.
 *      The enclosed help files will explain the intent of all commands.
 *      
 * -------------------------------------------------------------------------------------------------------------------------------
 * 
 */
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml;

namespace WPFChatServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ChatServer cs;
        bool autoRun = false;

        public int debugLevel = 9;
        public readonly string localPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        public int localPort = 6667;
        public int timeOut = 15;
        public bool useHello = true;
        public int logWindow = 7;
        public string version;

        /*
         * Constructor for the window
         */
        public MainWindow()
        {
            InitializeComponent();
            btnStart_Server.IsChecked = false;
            btnStop_Server.IsChecked = true;

            // get the version of this exe
            Assembly assem = Assembly.GetEntryAssembly();
            AssemblyName assemName = assem.GetName();
            Version ver = assemName.Version;

            // create the date string from the build number
            string dt = new DateTime(2000, 1, 1).AddDays(ver.Build).ToString("yyMMdd");

            // create the time string from the revision number
            TimeSpan t = TimeSpan.FromSeconds(ver.Revision*2);
            string tm = string.Format("{0:D2}{1:D2}", t.Hours, t.Minutes);

            // create the version in x.x.yyMMdd.HHmm format
            version = string.Format("1.4.{2}.{3}", dt, tm);
            App.Current.MainWindow.Title = "Chat Server ver " + version;

            Display("Loading Chat Server version " + version, 1);
            ClearOldLogs();
            LoadConfig();

            cs = new ChatServer(this);
            tbxMain.Text = string.Empty;

            if (autoRun)
            {
                BtnStart_Server_Click(null, null);
            }
        }


        public void LoadConfig()
        {
            string configFile = localPath + "WPFChatServer.xml";
            int ivalue;
            string value;
            string setting;

            if (File.Exists(configFile))
            {
                XmlDocument xmldoc = new XmlDocument();
                XmlNodeList xmlnode;
                FileStream fs = new FileStream(configFile, FileMode.Open, FileAccess.Read);

                xmldoc.Load(fs);
                xmlnode = xmldoc.GetElementsByTagName("Properties");

                for (int i = 0; i <= xmlnode.Count - 1; i++)
                {
                    for (int j = 0; j < xmlnode[i].ChildNodes.Count; j++)
                    {
                        value = xmlnode[i].ChildNodes.Item(j).InnerText.Trim();
                        if (int.TryParse(value, out ivalue) == false) ivalue = 0;

                        setting = xmlnode[i].ChildNodes.Item(j).Name.ToUpper().Trim();
                        Display(string.Format("Setting {0} to {1}", setting, value));

                        switch (setting)
                        {
                            case "AUTORUN":
                                // assume false unless first character of value contains a specific character
                                autoRun = "YyTt1".Contains((value + "Y").Substring(0, 1));
                                break;

                            case "PORT":
                            case "PORTOUT":
                                localPort = Enumerable.Range(1000, 65535).Contains(ivalue) ? ivalue : localPort;
                                break;

                            case "DEBUG":
                            case "DEBUGLEVEL":
                                debugLevel = Enumerable.Range(0, 9).Contains(ivalue) ? ivalue : debugLevel;
                                break;

                            case "LOGDAYS":
                                logWindow = Enumerable.Range(0, 30).Contains(ivalue) ? ivalue : logWindow;
                                break;

                            case "TIMEOUT":
                                timeOut = Enumerable.Range(5, 120).Contains(ivalue) ? ivalue : timeOut;
                                break;

                            case "USEHELLO":
                                // assume true unless first character of value contains a specific letter
                                useHello = "NnFf0".Contains((value + "Y").Substring(0, 1)) == false;
                                break;

                            default:
                                Display(string.Format("Unknown property {0}", setting), 1);
                                break;
                        }
                    }
                }
            }
        }


        /*
         * Close event for the window defined in the xaml
         */
        public void Window_Closing(object sender, CancelEventArgs e)
        {
            Display("Shutting down WPFChatServer...\r\n", 1);
            Environment.ExitCode = 0;

            if (cs.serverRunning)
            {
                Environment.ExitCode = 1;
                cs.StopServer();
            }
            cs = null;
        }


        /*
         * Send text out to the main display at level 9
         */
        public void Display(string textout)
        {
            // assume we don't want it saved to the file unless debuglevel>8
            Display(textout, 9);
        }



        /*
         * In the unmodified system, this is the only method that has thread
         * blocking logic.  A system that is used by more than one user may
         * need additional thread handling logic.
         */
        public void Display(string textout, int level)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                // everything goes out to the display window
                tbxMain.AppendText(textout + "\r\n");
                tbxMain.ScrollToEnd();

                // Only write to file if level <= debugLevel
                if (level <= debugLevel)
                {
                    WriteDebug("* " + textout);
                }
            });

        }



        /*
         * Output debug to a log file
         */
        public void WriteDebug(string info)
        {
            // 0 = no output at all
            if (debugLevel != 0)
            {
                DateTime date = DateTime.Now;
                string now = date.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logFileName = localPath + date.ToString("yyyyMMdd") + "-WPFChatServer.log";
                string infoOut = now + " - ";

                info = info.Replace("\n", String.Empty);

                // if there are leading EOLs then transfer them to before
                // the date and time stamp before saving
                while (info.Length > 0 && info[0] == '\r')
                {
                    infoOut = "\n\r" + infoOut;
                    info = info.Substring(1);
                }

                // why not replace("\r","\n\r")?
                infoOut += info;

                using (StreamWriter file = new StreamWriter(logFileName, true))
                {
                    file.WriteLine(infoOut);
                }
            }
        }



        /*
         * Delete log files over a certain number of days in age
         */
        public void ClearOldLogs()
        {
            DateTime date = DateTime.Now.AddDays(-logWindow);  // x day rolling deletion
            // grab all matching files
            string[] files = Directory.GetFiles(localPath, "*.log");
            string file;

            // is it too old?
            for (int i = 0; i < files.Length; i++)
            {
                // Get the filename
                file = Path.GetFileName(files[i]);

                if (file.Length > 10 && file.Substring(8, 1) == "-")
                {
                    // put into ints to prevent accidently killing wrong files like Test04-03-2020.LOG
                    int.TryParse(file.Substring(0, 8), out int oldDate);
                    int.TryParse(date.ToString("yyyyMMdd"), out int cutDate);

                    // geez!  I cannot beleive I got all worried about a 4 digit year
                    // C# probably won't even exist in another 30 years, let alone 80
                    // and if github and I are both still around in 20 years, I'll be 
                    // amazed (80 is not an easily attained age in my family).
                    if (oldDate > 19000101 && oldDate < cutDate)
                    {
                        try
                        {
                            // delete the file
                            System.IO.File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Display("Can't delete file " + file + "\r\n" + "Exception - " + ex.Message);
                        }
                    }
                }
            }
        }


        /*
         * Start Menu click
         */
        public void BtnStart_Server_Click(object sender, RoutedEventArgs e)
        {
            cs.StartServer();
            btnStart_Server.IsChecked = true;
            btnStop_Server.IsChecked = false;
        }

        /*
         * Stop button click
         */
        public void BtnStop_Server_Click(object sender, RoutedEventArgs e)
        {
            cs.StopServer();
            btnStart_Server.IsChecked = false;
            btnStop_Server.IsChecked = true;
        }

    } // End class MainWindow
}
