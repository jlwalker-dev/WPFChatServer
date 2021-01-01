/*============================================================================================
 * This is the ChatServer that listens for initial connections from clients and creates a new 
 * thread to handle each client.
 * 
 * All outbound communications are routed through this module.
 * 
 * We may need to add more code to the project to make everything thread safe if it goes beyond the realm of one person testing.
 * 
 * 
 * TODO
 *      
 *      Test IGNORE
 *      
 *      
 * Future Work
 *      Ignore list autosaves to UsersIgnoringUsers.sdt
 *      Ignore list shown upon login
 *      Look into secure sockets
 *      
 *============================================================================================*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace WPFChatServer
{
    class ChatServer
    {
        public MainWindow mw;

        public Hashtable clientsHash = new Hashtable();
        public List<ClassUsers> usersList = new List<ClassUsers>();
        public List<ClientThreading> clientThreads = new List<ClientThreading>();

        Thread csThread = null;
        Thread ccThread = null;
        TcpListener serverSocket;

        public readonly string LocalPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        public bool keepRunning = false;
        public bool serverRunning = false;
        public int timeOut = 30;

        private bool RequireHello = true;

        // System settings
        private string localIP;
        private int localPort = 6667;
        public int byteBuffer = 1024;
        //public int instID = 0;

        //Regex regex = new Regex("[^0-9a-zA-Z_-]+");

        CommandsForRegistration RegistrationCommands;
        CommandsForRooms RoomCommands;
        CommandsForUsers UserCommands;


        /*============================================================================================
         * Constructor for the class which sets
         * up some initial pieces of data
         *============================================================================================*/
        public ChatServer(MainWindow m)
        {
            mw = m;
            localIP = GetLocalIP();
            if (mw.localPort > 0) localPort = mw.localPort;
            if (mw.timeOut > 0) timeOut = mw.timeOut;
            RequireHello = mw.useHello;

            RegistrationCommands = new CommandsForRegistration(this);
            RoomCommands = new CommandsForRooms(this, RegistrationCommands);
            UserCommands = new CommandsForUsers(this);
        }


        /*============================================================================================
         * This starts the thread which is run and listens
         * for initial communications from clients
         *============================================================================================*/
        public void StartServer()
        {

            if (keepRunning)
            {
                // it's already running
                mw.Display("Error: Server is already running.");
            }
            else
            {
                mw.Display(string.Format("Starting ChatServer on IP {0} port {1}...", localIP, localPort), 5);
                keepRunning = true;

                csThread = null;
                csThread = new Thread(ChatServerThread);
                csThread.Start();

                ccThread = null;
                ccThread = new Thread(CheckConnections);
                ccThread.Start();
            }
        }


        /*============================================================================================
         * Code to stop the Chat Server thread
         * 
         *============================================================================================*/
        public void StopServer()
        {
            if (keepRunning)
            {
                mw.Display("Stopping ChatServer...", 5);
                keepRunning = false;
                serverSocket.Stop();
                usersList = new List<ClassUsers>();
                clientsHash = new Hashtable();
            }
            else
            {
                mw.Display("Error: Server is not running.");
            }
        }


        /*============================================================================================
         * Look at all connections and clean up any
         * marked as disconnected or the last message
         * was over timeOut minutes ago.
         * 
         *============================================================================================*/
        public void CheckConnections()
        {
            while (keepRunning)
            {
                // use a while loop because we may be deleting an item
                // from the list and won't want to auto increment
                int i = 0;
                while (i < clientThreads.Count)
                {
                    // kill this client if too long or destroyMe flag is set
                    int idx = usersList.FindIndex(x => x.ThreadGUID.Equals(clientThreads[i].ThreadGUID));

                    if (idx >= 0)
                    {
                        TimeSpan howLong = DateTime.Now - usersList[idx].lastMsgDT;

                        if (clientThreads[i].destroyMe || howLong.Minutes > timeOut)
                        {
                            // clean up the lists and hash table
                            try
                            {
                                RoomCommands.RoomCleanup(usersList[idx].CurrentRoom);

                                clientThreads.RemoveAt(i); // remove from list
                                clientsHash.Remove(usersList[idx].ThreadGUID);  // remove from hash table
                                mw.Display("Removed client " + usersList[idx].ThreadGUID + (howLong.Minutes > 60 ? " (timeout)" : " (disconnect)"), 7);
                                mw.Display("          user " + usersList[idx].NickName);
                                usersList.Remove(usersList[idx]); // remove from list
                            }
                            catch (Exception ex)
                            {
                                // got an error from the process
                                mw.Display("CHECKCONNECTIONS: Error - " + ex.Message, 1);
                            }
                        }
                        else
                        {
                            // still active, so increment
                            i++;
                        }
                    }
                    else
                    {
                        // something didn't go right, report and increment
                        mw.Display("Issue matching usersList to ClientThreads =>" + clientThreads[i].nickName, 1);
                        i++;
                    }
                }

                // do this twice a second
                Thread.Sleep(500);
            }
        }


        /*============================================================================================
         * This is the heart of the server and it kicks
         * off client threads as they connect
         * 
         *============================================================================================*/
        public void ChatServerThread()
        {
            serverRunning = true;

            IPAddress localAddr = IPAddress.Parse(localIP);
            serverSocket = new TcpListener(localAddr, localPort);
            TcpClient clientSocket = default;
            long counter = 0;

            serverSocket.Start();
            mw.Display("ChatServer Started ....", 5);

            while (keepRunning)
            {
                counter += 1;

                string dataFromClient = null;
                byte[] bytesFrom = null;

                try
                {
                    // wait for a connection
                    clientSocket = serverSocket.AcceptTcpClient();
                    bytesFrom = new byte[1024];
                    NetworkStream networkStream = clientSocket.GetStream();
                    networkStream.Read(bytesFrom, 0, bytesFrom.Length);
                }
                catch (Exception ex)
                {
                    if (keepRunning)
                        mw.Display("Socket read error: " + ex.Message, 1);
                    else
                        mw.Display("Closing ServerSocket", 5);
                }

                if (bytesFrom != null)
                {
                    // convert to a string and clean it up
                    dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                    dataFromClient = dataFromClient.Replace("\0", string.Empty).Trim();
                }


                string msg = string.Empty;
                string welcomeMsg;
                string guestName = "GUEST" + counter.ToString();

                if (dataFromClient != null)
                {
                    if (RequireHello == false && (dataFromClient.Length < 8 || dataFromClient.Substring(0, 7).Equals("[HELLO:") == false))
                    {
                        // If HELLO is not required
                        msg = dataFromClient; // we're going pass what came in to the client thread

                        // create a guest HELLO string
                        dataFromClient = string.Format("[HELLO:{0}|{0}|_UNK_|_UNK_]", guestName);
                    }

                    if (clientSocket != null)
                    {
                        mw.Display(dataFromClient, 8);
                        if (dataFromClient.Length > 7 && dataFromClient.Substring(0, 7).Equals("[HELLO:"))
                        {
                            // break out the HELLO message
                            dataFromClient = dataFromClient.Replace("[HELLO:", "").Replace("]", "").Trim();
                            string[] data = dataFromClient.Split('|');
                            string guid = counter.ToString("D9");

                            if (data.Length > 3)
                            {
                                // Remote IP address 
                                string ipaddr = ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address.ToString();

                                // Set up a guest account -   GUID  Name       Nick       IP      Computer Name
                                ClassUsers c = new ClassUsers(guid, guestName, guestName, ipaddr, data[3].Trim());

                                // If client sent a HELLO, try to log in using the transmitted user/password
                                if (msg.Length == 0 && RegistrationCommands.userRegistration.Login(data[0], data[1]))
                                {
                                    string g = RegistrationCommands.GetUserInfo("RecID");

                                    if (usersList.FindIndex(x => x.RegisteredGUID.Equals(g)) < 0)
                                    {
                                        c.FullName = RegistrationCommands.GetUserInfo("FullName");
                                        c.NickName = RegistrationCommands.GetUserInfo("NickName");
                                        c.UserName = RegistrationCommands.GetUserInfo("UserName");
                                        c.RegisteredGUID = RegistrationCommands.GetUserInfo("RecID");
                                        c.Email = RegistrationCommands.GetUserInfo("Email");
                                        c.UserRights = RegistrationCommands.GetUserInfo("Rights");
                                        c.Status = RegistrationCommands.GetUserInfo("Status");
                                        c.registeredDT = RegistrationCommands.GetUserInfo("RegisteredDT");
                                        c.lastOnDT = RegistrationCommands.GetUserInfo("LastOnDT");
                                        c.lastMsgDT = DateTime.Now;

                                        welcomeMsg = "Welcome " + (c.IsAdmin ? "administrator " : "") + c.FullName
                                            + ".\r\nYou were last on " + c.lastOnDT.ToString("MMMM dd, yyyy @ h:mm tt");

                                        // if there is a Welcome.txt file, load it at the end of the welcome message
                                        if (File.Exists(LocalPath + "Welcome.txt"))
                                            welcomeMsg += "\r\n\r\n" + File.ReadAllText(LocalPath + "Welcome.txt");
                                    }
                                    else
                                    {
                                        // already logged in
                                        welcomeMsg = "You are already logged into the system.  Your nickname is " + c.NickName;
                                    }
                                }
                                else
                                {
                                    // login failure
                                    c.registeredDT = DateTime.MinValue;
                                    c.lastOnDT = DateTime.MinValue;
                                    c.lastMsgDT = DateTime.Now;
                                    welcomeMsg = "Login failure - you are " + guestName + " and have limited access until logged in";
                                }

                                // Clear out any change tracks so far
                                c.Reset();

                                // Add the user to the list
                                usersList.Add(c);

                                // set up access to the client socket
                                clientsHash.Add(guid, clientSocket);

                                // display some information to the form
                                mw.Display(string.Format("{0} -Instance: {1} -User: {2} -Nick: {3} -Computer: {4} -IP: {5} -remoteID: {6} -localIP: {7} -remoteIP: {8})",
                                    DateTime.Now.ToString("MM/dd HH:mm:ss"), c.ThreadGUID, c.UserName, c.NickName, c.computerName, c.ipAddress, data[0], data[3], ipaddr) + " is in the lobby");

                                // Create and start a new thread for this client connection
                                clientThreads.Add(new ClientThreading());
                                clientThreads[clientThreads.Count - 1].StartClientThread(mw, this, clientSocket, clientThreads.Count - 1, msg);
                                clientThreads[clientThreads.Count - 1].nickName = c.NickName;

                                // Welcome them and let everyone know they are here
                                SendToGUID(guid, welcomeMsg);
                                BroadcastMsgToRoom("", ">>> " + c.NickName + " is in " + c.CurrentRoom);
                            }
                            else
                            {
                                // oops, HELLO msg was too short
                                mw.Display(string.Format(">>> Received badly formed hello message {0}\r\n", dataFromClient), 5);
                            }
                        }
                        else
                        {
                            // tell them we need a hello
                            if (clientSocket.Connected)
                            {
                                try
                                {
                                    msg = "This server requires a [HELLO:...] message to initiate a connection";
                                    NetworkStream broadcastStream = clientSocket.GetStream();
                                    Byte[] broadcastBytes = Encoding.ASCII.GetBytes(msg);
                                    broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                                    broadcastStream.Flush();

                                    // Remote IP address
                                    string ipaddr = ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address.ToString();
                                    mw.Display(string.Format("Notification for HELLO sent to {0}\r\nReceived: {1}\r\nSent: {2}",
                                        ipaddr, dataFromClient, msg));

                                    clientSocket.Dispose();
                                    clientSocket = null;
                                }
                                catch (Exception ex)
                                {
                                    mw.Display("Hello broadCast error: " + ex.Message, 1);
                                }
                            }
                        }
                    }
                }
            }

            serverRunning = false;
            mw.Display("DoServer stopped.", 5);
        }


        /*============================================================================================
         * Return the local IP
         *============================================================================================*/
        private string GetLocalIP()
        {
            string myIP = "127.0.0.1";
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    myIP = ip.ToString();
                    break;
                }
            }

            return myIP;
        }


        /*============================================================================================
         * Process any commands sent by someone
         * 
         *============================================================================================*/
        public string MsgProc(string msg, string thGUID)
        {
            int ui = usersList.FindIndex(x => x.ThreadGUID.Equals(thGUID));
            int ct = clientThreads.FindIndex(x => x.ThreadGUID.Equals(thGUID));

            ClassCommandInfo cmdInfo = new ClassCommandInfo(ui, usersList[ui]);

            if (mw.debugLevel > 4)
                mw.Display(usersList[ui].NickName + ": " + msg);

            // Is there a message to process?
            if (msg.Length > 1)
            {
                usersList[ui].msgCount++;
                usersList[ui].lastMsgDT = DateTime.Now;

                // Does the message start with a forward slash?
                if (msg.Substring(0, 1).Equals("/") && ui >= 0)
                {
                    // Process the command
                    msg += " ";
                    cmdInfo.command = msg.Substring(1, msg.IndexOf(' ') - 1).Trim().ToUpper();
                    cmdInfo.msg = msg.Substring(msg.IndexOf(' ')).Trim();

                    //Console.WriteLine("Processing command " + command);
                    if (cmdInfo.ThisUser.RegisteredGUID.Length > 0)
                    {
                        // Room commands for registered users
                        if (cmdInfo.command.Length > 0)
                            RoomCommands.Process(ref cmdInfo);
                    }

                    // Commands for Everyone
                    if (cmdInfo.command.Length > 0)
                        UserCommands.Process(ref cmdInfo);

                    // unregistered or not yet logged in
                    if (cmdInfo.command.Length > 0)
                        RegistrationCommands.Process(ref cmdInfo);

                    // get any changes made to the user while in the command processors
                    if (cmdInfo.ThisUser.WasChanged)
                    {
                        // This is catch up... if any registration related changes were made by the
                        // commands then they should already be saved in the registration file
                        if (mw.debugLevel > 8)
                            mw.Display(">>> Updating user list");

                        // Properties that are passed between the registration module
                        if (cmdInfo.ThisUser.Dif("RegisteredGUID"))
                        {
                            // when this is updated, other fields that are not normally 
                            // tracked must be updated also because we've just loaded
                            // the values from the registration table
                            usersList[ui].RegisteredGUID = cmdInfo.ThisUser.RegisteredGUID;
                            usersList[ui].lastOnDT = cmdInfo.ThisUser.lastOnDT;
                            usersList[ui].registeredDT = cmdInfo.ThisUser.registeredDT;
                        }

                        if (cmdInfo.ThisUser.Dif("Email")) usersList[ui].Email = cmdInfo.ThisUser.Email;
                        if (cmdInfo.ThisUser.Dif("FullName")) usersList[ui].FullName = cmdInfo.ThisUser.FullName;

                        if (cmdInfo.ThisUser.Dif("NickName"))
                        {
                            usersList[ui].NickName = cmdInfo.ThisUser.NickName;
                            if (ct >= 0)
                                clientThreads[ct].nickName = cmdInfo.ThisUser.NickName;
                            else
                                mw.Display("MsgProc failed to match ClientThreads with ID " + thGUID);
                        }

                        if (cmdInfo.ThisUser.Dif("Status")) usersList[ui].Status = cmdInfo.ThisUser.Status;
                        if (cmdInfo.ThisUser.Dif("UserName")) usersList[ui].UserName = cmdInfo.ThisUser.UserName;
                        if (cmdInfo.ThisUser.Dif("UserRights")) usersList[ui].UserRights = cmdInfo.ThisUser.UserRights;

                        // session properties managed by the Rooms module
                        if (cmdInfo.ThisUser.Dif("AwayMsg")) usersList[ui].AwayMsg = cmdInfo.ThisUser.AwayMsg;
                        if (cmdInfo.ThisUser.Dif("IgnoreList")) usersList[ui].IgnoreList = cmdInfo.ThisUser.IgnoreList;
                        if (cmdInfo.ThisUser.Dif("Seating")) usersList[ui].Seating = cmdInfo.ThisUser.Seating;
                        if (cmdInfo.ThisUser.Dif("CurrentRoom")) usersList[ui].CurrentRoom = cmdInfo.ThisUser.CurrentRoom;
                    }

                    // If the command property still has a length then it did
                    // not get executed ans was either mispelled or invalid
                    if (cmdInfo.command.Length > 0)
                        cmdInfo.msgOut = string.Format("Command '{0}' is not a valid command for you at this time.\r\nUse /HELP for more information.\r\n", cmdInfo.command);
                }
                else
                {
                    if (usersList[ui].AwayMsg != null && usersList[ui].AwayMsg.Length > 0)
                    {
                        // an away msg means you're away
                        cmdInfo.msgOut = "You have an away message set, so no one will hear you.";
                    }
                    else
                    {
                        // normal message
                        cmdInfo.Results = usersList[ui].NickName + ": " + msg;
                    }
                }
            }

            // Was there something to send back to the client?
            if (cmdInfo.msgOut.Length > 0) SendToGUID(cmdInfo.ThisUser.ThreadGUID, cmdInfo.msgOut);

            return cmdInfo.Results;
        }


        /*============================================================================================
         * Toggle the Ignore info.
         * If you send someone on the ignore list, it takes
         * them off.  If you send no nickname, it takes
         * everyone off the list.
         * 
         *============================================================================================*/
        public void Ignore(string guid, string ignoreName)
        {
            ClassUsers iu = usersList.Find(x => x.NickName.Equals(ignoreName, StringComparison.OrdinalIgnoreCase));
            int me = usersList.FindIndex(x => x.ThreadGUID.Equals(guid));

            if (iu == null && ignoreName.Length > 0)
            {
                SendToGUID(usersList[me].ThreadGUID, string.Format("There is no one named {0} here", ignoreName));
            }
            else if (ignoreName.Length == 0 && me >= 0)
            {
                // no nick, so clear out all entries
                usersList[me].IgnoreList = string.Empty;
                SendToGUID(usersList[me].ThreadGUID, "You are no longer ignoring anyone");
            }
            else if (iu != null && me >= 0)
            {
                string g = string.Format("[{0}]", iu.RegisteredGUID);

                if (usersList[me].IgnoreList.Contains(g))
                {
                    SendToGUID(usersList[me].ThreadGUID, "You are no longer ignoring " + iu.NickName);
                    usersList[me].IgnoreList = usersList[me].IgnoreList.Replace(g, ""); // remove the person from the list
                }
                else
                {
                    SendToGUID(usersList[me].ThreadGUID, "You are now ignoring " + iu.NickName);
                    usersList[me].IgnoreList += g; // add someone to the list
                }
            }
        }



        /*============================================================================================
         * Look to see if nick name exists
         *============================================================================================*/
        public int NickExists(string nick)
        {
            return usersList.FindIndex(x => x.NickName.Equals(nick, StringComparison.OrdinalIgnoreCase));
        }



        /*============================================================================================
         * Look to see if name exists
         *============================================================================================*/
        public int NameExists(string name)
        {
            return usersList.FindIndex(x => x.UserName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }



        /*============================================================================================
         * Send a message out to everyone
         *============================================================================================*/
        public void Broadcast(string msg, string fromGUID)
        {
            // Display outgoing broadcase if over DBL 6, save to file if DBL is 9
            ClassUsers u = usersList.Find(x => x.ThreadGUID.Equals(fromGUID));
            string roomName = string.Empty;

            if (u != null)
            {
                string fromRegID = u.RegisteredGUID;

                //if (mw.debugLevel > 6)
                //    mw.Display(msg);

                // get room info
                roomName = u.CurrentRoom;
                ClassRooms rm = (ClassRooms)RoomCommands.roomHash[roomName];

                // do they have admin rights?
                bool OK2Speak = (u.IsAdmin || rm.admin.Equals(fromRegID));

                // if we're not in an active speaker room then
                // no problem, otherwise need the right to speak
                if (rm.privacySetting < 4 || OK2Speak)
                {
                    foreach (DictionaryEntry Item in clientsHash)
                    {
                        TcpClient broadcastSocket;
                        broadcastSocket = (TcpClient)Item.Value;

                        // If we can't find the user entry or if the ignore list does
                        // not contain the fromGUID and both are in the same room
                        // and they are in the lobby
                        u = usersList.Find(x => x.ThreadGUID.Equals(Item.Key));
                        if (u == null || ((u.IgnoreList.Contains(fromRegID) == false || u.CurrentRoom.Equals("LOBBY")) && u.CurrentRoom.Equals(roomName)))
                        {
                            if (broadcastSocket.Connected)
                            {
                                try
                                {
                                    NetworkStream broadcastStream = broadcastSocket.GetStream();
                                    Byte[] broadcastBytes = Encoding.ASCII.GetBytes(msg);
                                    broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                                    broadcastStream.Flush();
                                }
                                catch (Exception ex)
                                {
                                    mw.Display("BroadCast error: " + ex.Message, 1);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // can't speak in an active speaking room
                    SendToGUID(fromGUID, "Quiet please.");
                }
            }
        }  //end broadcast function



        /*============================================================================================
         * Sometimes we just need to broadcase a message to
         * the room or everyone if room is empty.  
         * 
         * No controls, just push the msg out.
         *============================================================================================*/
        public void BroadcastMsgToRoom(string room, string msg)
        {
            ClassUsers u;

            foreach (DictionaryEntry Item in clientsHash)
            {
                TcpClient broadcastSocket;
                broadcastSocket = (TcpClient)Item.Value;

                // If we can find the user entry and if the 
                // receiving user is in the room, or the room
                // is empty (the msg is for the entire server)
                u = usersList.Find(x => x.ThreadGUID.Equals(Item.Key));
                if (u != null && (u.CurrentRoom.Equals(room) || room.Length == 0))
                {
                    if (broadcastSocket.Connected)
                    {
                        try
                        {
                            NetworkStream broadcastStream = broadcastSocket.GetStream();
                            Byte[] broadcastBytes = Encoding.ASCII.GetBytes(msg);
                            broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                            broadcastStream.Flush();
                        }
                        catch (Exception ex)
                        {
                            mw.Display("BroadCast error: " + ex.Message, 1);
                        }
                    }
                }
            }
        }  //end broadcast function



        /*============================================================================================
         * Send a private message to recipient and sender
         *============================================================================================*/
        public void SendPrvMsg(ClassUsers frU, ClassUsers toU, string msg)
        {
            // found it!  Now compare GUID with the key for the match
            foreach (DictionaryEntry Item in clientsHash)
            {
                if (Item.Key.Equals(toU.ThreadGUID))
                {
                    SendToClient(Item, "Private message from " + frU.NickName + ": " + msg);

                    // echo the private msg back to the sender
                    if (frU != null)
                    {
                        SendToGUID(frU.ThreadGUID, "Private message to " + toU.NickName + ": " + msg);

                        // is there an away msg?  then send it back to the sender.
                        if (toU.AwayMsg.Length > 0) SendToGUID(frU.ThreadGUID, "Away msg from " + toU.NickName + ": " + toU.AwayMsg);
                    }
                    break;
                }
            }
        }  //end sendTo function



        /*============================================================================================
         * Send a message out to the thread
         *============================================================================================*/
        public void SendToClient(DictionaryEntry clientListItem, string msg)
        {
            // and send it!
            TcpClient broadcastSocket;
            broadcastSocket = (TcpClient)clientListItem.Value;

            try
            {
                NetworkStream broadcastStream = broadcastSocket.GetStream();
                Byte[] broadcastBytes = Encoding.ASCII.GetBytes(msg);
                broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                broadcastStream.Flush();
            }
            catch (Exception ex)
            {
                mw.Display("BroadCast error: " + ex.Message, 1);
            }
        }



        /*============================================================================================
         * Set out a message to one user
         *============================================================================================*/
        public void SendToGUID(string toGUID, string msg)
        {
            // found it!  Now compare GUID with the key for the match
            foreach (DictionaryEntry Item in clientsHash)
            {
                if (Item.Key.Equals(toGUID))
                {
                    SendToClient(Item, msg);
                    break;
                }
            }
        }  //end sendOut function



        /*============================================================================================
         * Send the help file out to the user requesting
         *============================================================================================*/
        public void Help(string fromGUID, string subcat)
        {
            // found it!  Now compare GUID with the key for the match
            foreach (DictionaryEntry Item in clientsHash)
            {
                if (Item.Key.Equals(fromGUID))
                {
                    SendToClient(Item, string.Format("HELP {0}\r\n\r\n", (subcat.Length > 0 ? subcat.ToUpper() : "")));

                    // Help files are in the HELP folder
                    string fname = LocalPath + string.Format("HELP\\Help{0}.txt", (subcat.Length > 0 ? "-" + subcat.Replace(" ", "-") : ""));

                    if (File.Exists(fname))
                    {
                        string line;

                        // open the file
                        StreamReader file = new StreamReader(fname);
                        while ((line = file.ReadLine()) != null)
                        {
                            // send it out line by line
                            SendToClient(Item, line + "\r\n");
                        }

                        file.Close();
                    }
                    else
                    {
                        SendToClient(Item, "\r\n* * * NO HELP FOUND * * *");
                        mw.Display("Error: No help file found for " + fname);
                    }
                    break;
                }
            }
        }
    }
}
