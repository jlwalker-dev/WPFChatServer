using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WPFChatServer
{
    class CommandsForRooms
    {
        ChatServer chatServer;
        public Hashtable roomHash = new Hashtable();
        public string[] cusswords = new string[0];
        Regex regex = new Regex("[^0-9a-zA-Z_-]+");
        Registration userReg;

        public CommandsForRooms(ChatServer cserver, CommandsForRegistration rgclass)
        {
            chatServer = cserver;
            userReg = rgclass.userRegistration;

            ClassRooms r = new ClassRooms("*", "LOBBY", "");
            roomHash.Add("LOBBY", r);

            // do we care about certain words in the room names?
            if (File.Exists(cserver.LocalPath + "RoomBlackList.txt"))
            {
                cusswords = File.ReadLines("c:\\file.txt").ToArray();
                for (int i = 0; i < cusswords.Length; i++)
                {
                    cusswords[i] = cusswords[i].ToLower();
                }
            }
        }

        public void Process(ref ClassCommandInfo cmdInfo)
        {
            string cmd;
            string msg = cmdInfo.msg;
            string replay;
            ClassUsers targetUser;
            ClassRooms rm;
            ClassRooms currentRoom = (ClassRooms)roomHash[cmdInfo.ThisUser.CurrentRoom];

            switch (cmdInfo.command)
            {
                case "MSG":  // private msg - MGS usernick msg
                    string toUser = msg.Substring(0, msg.IndexOf(' ')).Trim();
                    replay = string.Format("/{0} {1} {2}\r\n", cmdInfo.command, toUser, msg);

                    // must have a space or there is no message
                    if (msg.Contains(" "))
                    {
                        ClassUsers toU = chatServer.usersList.Find(x => x.NickName.Equals(toUser, StringComparison.OrdinalIgnoreCase));
                        rm = (ClassRooms)roomHash[toU.CurrentRoom];

                        // Are you trying to talk to a speaker?
                        bool OK2Speak = toU.Seating != 3;

                        // If they are talking to a speaker in the same room, then
                        if (OK2Speak == false && toU.CurrentRoom == cmdInfo.ThisUser.CurrentRoom)
                        {
                            // are they sysadmin?
                            OK2Speak = OK2Speak || cmdInfo.ThisUser.IsAdmin;

                            // are they moderators or admin?
                            OK2Speak = OK2Speak || ((rm.admin + rm.moderators).Contains(cmdInfo.ThisUser.RegisteredGUID));

                            // are they another speaker?
                            OK2Speak = OK2Speak || cmdInfo.ThisUser.Seating == 3;
                        }

                        if (OK2Speak)
                        {
                            chatServer.SendPrvMsg(cmdInfo.ThisUser, toU, msg.Substring(msg.IndexOf(' ')).Trim());
                        }
                        else
                        {
                            // general public tried to talk to a speaker
                            cmdInfo.msgOut = replay + "You are not authorized to send a private msg to a speaker";
                        }
                    }

                    cmdInfo.command = string.Empty;
                    break;


                case "ENTER":
                    if (cmdInfo.userIdx >= 0)
                    {
                        // break out the topic if it exists
                        string topic = string.Empty;
                        if (msg.Contains(" "))
                        {
                            topic = msg.Substring(msg.IndexOf(" ")).Trim();
                            msg = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // room name
                        }
                        else
                        {
                            msg = msg.ToUpper();
                        }

                        replay = string.Format("/{0} {1} {2}\r\n", cmdInfo.command, topic, msg);

                        if (roomHash.Contains(msg))
                        {
                            // get the room
                            rm = (ClassRooms)roomHash[msg];
                            int seating = 0;

                            // here we control seating - chack to see if they gave a password
                            if (topic.Length > 0 && rm.privateSeats > 0)
                            {
                                if (rm.password.Equals(topic))
                                {
                                    seating = 1;
                                }
                                else
                                {
                                    cmdInfo.msgOut = replay + "Invalid Password";
                                    seating = -1;
                                }
                            }

                            // is there any seating available?
                            if (seating == 0 && rm.privacySetting == 3)
                            {
                                // is this an invited speaker?
                                // if so, set seating to 3
                            }

                            // you're allowed to try to enter
                            if (seating >= 0)
                                EnterRoom(ref cmdInfo, msg, seating);
                        }
                        else
                        {

                            // clean up the room name so only has A-Z, 0-9, or _ and -
                            string roomName = regex.Replace(msg.ToUpper(), "");

                            // Is there a saved room with this name?
                            bool validName = (File.Exists(chatServer.LocalPath + roomName + SmallDataHandler.dataExt) == false);

                            if (validName)
                            {
                                if (cusswords.Length > 0)
                                {
                                    for (int i = 0; i < cusswords.Length; i++)
                                    {
                                        if (roomName.ToLower().Contains(cusswords[i]))
                                        {
                                            validName = false;
                                            break;
                                        }
                                    }
                                }

                                // is it a valid name and is it a valid length?
                                if (validName && Enumerable.Range(3, 15).Contains(roomName.Length))
                                {
                                    if (roomHash.Contains(roomName) == false)
                                    {
                                        cmdInfo.msgOut = replay + "You have created " + roomName;
                                        ClassRooms room = new ClassRooms(cmdInfo.ThisUser.RegisteredGUID, roomName, topic);
                                        roomHash.Add(roomName, room);
                                    }

                                    EnterRoom(ref cmdInfo, roomName, 0);
                                }
                                else
                                {
                                    cmdInfo.msgOut = replay + string.Format("Sorry, {0} is invalid.  It can only contain letters, numbers, underscore, and dashes with length between 3 and 15 characters and not contain black listed words.", msg.ToUpper());
                                }
                            }
                            else
                            {
                                // You can't create this room
                                cmdInfo.msgOut = replay + string.Format("Sorry, room {0} has a profile and must be loaded by room owner or a moderator", msg.ToUpper());
                            }

                        }
                    }

                    break;


                // PRIVACY - 0=not private, 1=invite only, 2=PW invite
                // SEATS - x/y/z where x is number of public seats, y is number of private seats, z is speaker seats
                // RECORD - NO|YES - turn recording on or off
                case "CHANGE": // update room properties - CHANGE PRIVACY|RECORD|SEATS|TOPIC <value>
                    msg += " ";

                    replay = string.Format("/{0} {1}\r\n", cmdInfo.command, msg);

                    if (cmdInfo.userIdx >= 0)
                    {
                        cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper();
                        msg = msg.Substring(msg.IndexOf(" ")).Trim();
                        int ivalue = 0;

                        rm = (ClassRooms)roomHash[cmdInfo.ThisUser.CurrentRoom];

                        if (rm.admin.Equals(cmdInfo.ThisUser.RegisteredGUID))
                        {

                            switch (cmd)
                            {
                                case "SPEAKER":  // extend or revoke speaker invitation
                                    cmd = "/" + msg.ToUpper() + "/";
                                    cmd = Names2IDs(cmd);

                                    if (rm.speakers.Contains(cmd))
                                        rm.moderators = rm.speakers.Replace(cmd, "/");
                                    else
                                        rm.speakers += cmd;

                                    rm.speakers = rm.speakers.Replace("//", "/");
                                    roomHash[cmdInfo.ThisUser.CurrentRoom] = rm;

                                    cmdInfo.msgOut = replay +
                                        string.Format("Speaker List\r\n--------------------\r\n{0}",
                                        IDs2Names(rm.speakers, "nickname").Substring(1).Replace("/", "\r\n"));
                                    break;

                                case "MOD":
                                    // find the nick
                                    cmd = "/" + msg.ToUpper() + "/";
                                    cmd = Names2IDs(cmd);

                                    if (rm.moderators.Contains(cmd))
                                        rm.moderators = rm.moderators.Replace(cmd, "/");
                                    else
                                        rm.moderators += cmd;

                                    rm.moderators = rm.moderators.Replace("//", "/");
                                    roomHash[cmdInfo.ThisUser.CurrentRoom] = rm;

                                    cmdInfo.msgOut = replay +
                                        string.Format("Moderator List\r\n--------------------\r\n{0}",
                                        IDs2Names(rm.moderators, "nickname").Substring(1).Replace("/", "\r\n"));
                                    break;

                                case "PASSWORD": // add a password for private seating
                                    rm.password = regex.Replace(msg, "");
                                    cmdInfo.msgOut = replay + string.Format("Password set to '{0}'", rm.password);
                                    break;

                                case "PRIVACY": // set privacy level
                                    if (int.TryParse(msg, out ivalue))
                                    {
                                        if (ivalue > 2)
                                        {
                                            cmdInfo.msgOut = replay + string.Format("Valid privacy settings are 0=OPEN, 1=INVITE, 2=PASSWORD\r\n"
                                                + "Higher values are automatically assigned based on other room settings.\r\n"
                                                + "Use /HELP command for more information.");
                                        }
                                        else
                                        {
                                            rm.privacySetting = ivalue;
                                            cmdInfo.msgOut = replay + string.Format("Privacy set to {0}", ivalue);
                                            rm.speakerSeats = 0;
                                        }
                                    }
                                    else
                                        cmdInfo.msgOut = replay + string.Format("Failed to convert '{0}' to an integer", msg);

                                    break;


                                case "RECORD":
                                    if (msg.ToUpper().Equals("ON"))
                                    {
                                        cmdInfo.msgOut = replay + "N/A - Recording turned on";
                                        rm.record = true;
                                    }
                                    else
                                    {
                                        cmdInfo.msgOut = replay + "N/A - Recording turned off";
                                        // turn off recording
                                        rm.record = false;
                                    }
                                    break;

                                case "SEATS":
                                    string[] s = msg.Split('/');
                                    if (s.Length > 2)
                                    {
                                        if (int.TryParse(s[0], out ivalue)) rm.publicSeats = ivalue; // anyone can join these - when privacy level>0 then listen only
                                        if (int.TryParse(s[1], out ivalue)) rm.privateSeats = ivalue; // password entry - can speak except when in forum, then submits questions to top of list
                                        if (int.TryParse(s[2], out ivalue)) rm.speakerSeats = ivalue; // invitation only - forum setting only and can speak freely

                                        if (rm.speakerSeats > 0) rm.privacySetting = 3; // if you have speakers, its a FORUM and entry is restricted
                                        cmdInfo.msgOut = replay + string.Format("Seats are set to {0} public, {1} private, and {2} speaker", rm.publicSeats, rm.privateSeats, rm.speakerSeats);
                                    }
                                    else
                                        cmdInfo.msgOut = replay + string.Format("You sent SEATS {0} and format must be 9/9/9", msg);

                                    break;

                                case "TOPIC":
                                    // filter content of topic
                                    rm.topic = msg;
                                    cmdInfo.msgOut = replay + string.Format("Room topic is '{0}'", rm.topic);
                                    break;

                                default:
                                    cmdInfo.msgOut = replay + string.Format("Unknown CHANGE command {0}", cmd);
                                    break;
                            }

                            roomHash[cmdInfo.ThisUser.CurrentRoom] = rm;
                        }
                        else
                        {
                            cmdInfo.msgOut = replay + "You are not an admin for this room";
                        }
                    }

                    break;

                // If in a speaker room, turn general chat on/off
                case "FORUM":
                    cmdInfo.msgOut = string.Format("/{0}\r\n", cmdInfo.command);

                    if (currentRoom.privacySetting > 2)
                    {
                        // room admin or moderator? (sysadmin can't control this)
                        if ((currentRoom.moderators + currentRoom.admin).Contains(cmdInfo.ThisUser.RegisteredGUID))
                        {
                            if (msg.ToUpper().Equals("START"))
                            {
                                chatServer.Broadcast("Quiet please, forum is starting...", cmdInfo.ThisUser.RegisteredGUID);
                                currentRoom.privacySetting = 4;
                            }

                            if (msg.ToUpper().Equals("END"))
                            {
                                currentRoom.privacySetting = 3;
                                chatServer.Broadcast("Thank you, the forum has ended...", cmdInfo.ThisUser.RegisteredGUID);
                            }

                            roomHash[cmdInfo.ThisUser.CurrentRoom] = currentRoom;
                        }
                    }

                    break;

                case "RMSAVE": // SAVE a room
                    msg += " ";

                    replay = string.Format("/{0}\r\n", cmdInfo.command);
                    rm = (ClassRooms)roomHash[cmdInfo.ThisUser.CurrentRoom];

                    if (rm.admin.Equals(cmdInfo.ThisUser.RegisteredGUID))
                    {
                        SmallDataHandler sdh = new SmallDataHandler(chatServer.LocalPath);
                        SmallFileHandlerStructure rec;

                        // Room table holds the following (reclen=150, fields=12)
                        // RMI|RoomName|Topic|Seats|Admin|PW
                        // MOD|Moderator List (10 per line)
                        // SPK|Speaker List (10 per line)
                        // BAN|Banned RegIDs (10 per line) 

                        // Save the file
                        try
                        {
                            // delete a bak file
                            if (File.Exists(chatServer.LocalPath + rm.name + ".bak"))
                            {
                                File.Delete(chatServer.LocalPath + rm.name + ".bak");
                            }

                            // rename the file to a BAK
                            if (File.Exists(chatServer.LocalPath + rm.name + SmallDataHandler.dataExt))
                            {
                                File.Move(chatServer.LocalPath + rm.name + SmallDataHandler.dataExt,
                                    chatServer.LocalPath + rm.name + ".bak");
                            }

                            // create a new one
                            if (sdh.Create(rm.name, 150, 12))
                            {
                                // Save the room info
                                //------------------------------
                                rec = sdh.BlankRec();
                                rec.Fields[0] = "RMI";
                                rec.Fields[1] = rm.name;
                                rec.Fields[2] = rm.topic;
                                rec.Fields[3] = "" + rm.publicSeats + "/" + rm.privateSeats + "/" + rm.speakerSeats;
                                rec.Fields[4] = rm.admin;
                                rec.Fields[5] = rm.password;
                                sdh.AddRec(rec);

                                // save moderators
                                //------------------------------
                                rec = sdh.BlankRec();
                                string[] info = rm.moderators.Split('/');

                                rec.Fields[0] = "MOD";
                                int j = 0;
                                int i = 0;

                                // You can only have 10 mods saved to file
                                while (j < info.Length && i < 10)
                                {
                                    // save a nonblank mod listing
                                    if (info[j].Length > 0)
                                        rec.Fields[1 + i++] = info[j];

                                    // next mod in list
                                    j++;
                                }

                                sdh.AddRec(rec);


                                // save speakers
                                //------------------------------
                                rec = sdh.BlankRec();
                                info = rm.speakers.Split('/');

                                rec.Fields[0] = "SPK";
                                j = 0;
                                i = 0;

                                // You can only have 10 speakers saved to file
                                while (j < info.Length && i < 10)
                                {
                                    // save a nonblank mod listing
                                    if (info[j].Length > 0)
                                        rec.Fields[1 + i++] = info[j];

                                    // next mod in list
                                    j++;
                                }

                                sdh.AddRec(rec);


                                // save Banned list
                                //------------------------------
                                info = rm.banned.Split('/');

                                j = 0;
                                i = 0;

                                // Banned list goes on forever
                                while (j < info.Length)
                                {
                                    if (j % 10 == 0)
                                    {
                                        if (j > 0)
                                            sdh.AddRec(rec);

                                        rec = sdh.BlankRec();
                                        rec.Fields[0] = "BAN";
                                        i = 0;
                                    }

                                    // save a nonblank mod listing
                                    if (info[j].Length > 0)
                                        rec.Fields[1 + i++] = info[j];

                                    // next mod in list
                                    j++;
                                }

                                if (rec.Fields[1].Length > 0) sdh.AddRec(rec);
                                cmdInfo.msgOut = string.Format("Room prifile for {0} has been saved", rm.name);
                            }
                            else
                            {
                                cmdInfo.msgOut = replay +
                                    string.Format("Failed to save room\r\nError: {0} - {1}", sdh.ErrorCode, sdh.ErrorMessage);

                            }
                        }
                        catch (Exception ex)
                        {
                            cmdInfo.msgOut = replay + "Failed to save room\r\nError: " + ex.Message;
                        }
                        finally
                        {
                            cmdInfo.command = string.Empty;
                            sdh.Close();
                        }

                    }
                    else
                    {
                        cmdInfo.msgOut = replay + "You aren't the room adminstrator";
                    }
                    break;

                case "RMLOAD":
                    replay = string.Format("/{0} {1}\r\n", cmdInfo.command, msg);
                    cmd = string.Empty;

                    if (msg.Contains(' '))
                    {
                        cmd = msg.Substring(msg.IndexOf(' ')).Trim();
                        msg = msg.Substring(0, msg.IndexOf(' ')).Trim();
                    }

                    if (msg.Length > 0)
                    {
                        SmallDataHandler sdh = new SmallDataHandler(chatServer.LocalPath);
                        SmallFileHandlerStructure rec;

                        if (roomHash.ContainsKey(cmd.Length > 0 ? cmd : msg))
                        {
                            cmdInfo.msgOut = replay + "Room is already loaded";
                        }
                        else
                        {
                            rm = new ClassRooms(msg, msg, "");

                            if (File.Exists(chatServer.LocalPath + msg + SmallDataHandler.dataExt))
                            {
                                try
                                {
                                    if (sdh.Open(msg))
                                    {
                                        rec = sdh.ReadAtIndex(1); // room info

                                        if (rec.Fields[0].Equals("RMI"))
                                        {
                                            if (cmd.Length == 0)
                                            {
                                                // load admin ID, room name, topic, and password
                                                rm = new ClassRooms(rec.Fields[4], rec.Fields[1], rec.Fields[2])
                                                {
                                                    password = rec.Fields[5]
                                                };
                                            }
                                            else
                                            {
                                                // load admin ID, room name, topic, and password
                                                rm = new ClassRooms(cmdInfo.ThisUser.RegisteredGUID, cmd, rec.Fields[2])
                                                {
                                                    password = rec.Fields[5]
                                                };
                                            }

                                            string[] seats = rec.Fields[3].Split('/');
                                            int.TryParse(seats[0], out rm.publicSeats);
                                            if (seats.Length > 1) int.TryParse(seats[1], out rm.privateSeats);
                                            if (seats.Length > 2) int.TryParse(seats[2], out rm.speakerSeats);
                                        }

                                        // if trying to copy, check to see if room is password protected
                                        if (cmd.Length == 0 || rm.password.Length == 0)
                                        {
                                            // continue to load info, read in moderators
                                            rec = sdh.ReadAtIndex(2); // moderators
                                            if (rec.Fields[0].Equals("MOD"))
                                            {
                                                for (int j = 1; j < rec.Fields.Length; j++)
                                                {
                                                    rm.moderators += rec.Fields[j] + "/";
                                                }

                                                while (rm.moderators.Contains("//"))
                                                    rm.moderators = rm.moderators.Replace("//", "/");
                                            }

                                            // read in speakers
                                            rec = sdh.ReadAtIndex(3);
                                            if (rec.Fields[0].Equals("SPK"))
                                            {
                                                for (int j = 1; j < rec.Fields.Length; j++)
                                                {
                                                    rm.speakers += rec.Fields[j] + "/";
                                                }

                                                while (rm.speakers.Contains("//"))
                                                    rm.speakers = rm.speakers.Replace("//", "/");
                                            }


                                            // read in banned users
                                            int i = 4;
                                            while (i <= sdh.RecCount)
                                            {
                                                rec = sdh.ReadAtIndex(i);
                                                if (rec.Fields[0].Equals("BAN"))
                                                {
                                                    for (int j = 1; j < rec.Fields.Length; j++)
                                                    {
                                                        rm.banned += rec.Fields[j] + "/";
                                                    }
                                                }

                                                i++;
                                            }

                                            while (rm.banned.Contains("//"))
                                                rm.banned = rm.banned.Replace("//", "/");

                                            // final check to see if ok for this user to load the room
                                            if (rm.admin.Equals(cmdInfo.ThisUser.RegisteredGUID) || rm.moderators.Contains(cmdInfo.ThisUser.RegisteredGUID))
                                            {
                                                // create the room in the hash table if it
                                                // hasn't been done already
                                                if (roomHash.ContainsKey(rm.name) == false)
                                                    roomHash.Add(rm.name, rm);

                                                // Enter the room
                                                EnterRoom(ref cmdInfo, rm.name, 0);

                                                if (cmd.Length > 0)
                                                    cmdInfo.msgOut = "You copied room profile " + msg + " as " + cmd;
                                                else
                                                    cmdInfo.msgOut = "You loaded room profile " + msg;
                                            }
                                            else
                                            {
                                                // not authorized
                                                cmdInfo.msgOut = "You are not an admin or moderator for this room profile";
                                            }
                                        }
                                        else
                                        {
                                            // can't copy a PW protected room
                                            cmdInfo.msgOut = "You cannot copy a password protected room";
                                        }
                                    }
                                    else
                                    {
                                        cmdInfo.msgOut = replay +
                                            string.Format("Failed to save room\r\nError: {0} - {1}", sdh.ErrorCode, sdh.ErrorMessage);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    cmdInfo.msgOut = replay + "Failed to save room\r\nError: " + ex.Message;
                                }
                                finally
                                {
                                    cmdInfo.command = string.Empty;
                                    sdh.Close();
                                }
                            }
                            else
                            {
                                cmdInfo.msgOut = "Table for room " + msg + " was not found";
                            }
                        }
                    }
                    break;

                case "BAN": // admin/moderator can ban someone from the room - BAN <nick>|<IP>
                            // BAN ? returns a list
                    replay = string.Format("/{0} {1}\r\n", cmdInfo.command, msg);

                    if (msg.Contains("?") == false)
                    {
                        if (cmdInfo.userIdx >= 0)
                        {
                            rm = (ClassRooms)roomHash[cmdInfo.ThisUser.CurrentRoom];
                            if (rm.admin.Equals(cmdInfo.ThisUser.RegisteredGUID) || rm.moderators.Contains("/" + cmdInfo.ThisUser.RegisteredGUID + "/")) // are you an admin or moderator?
                            {
                                // find the user to ban
                                string banID = "/" + userReg.GetUserRecord(msg).GetField("RecID") + "/";

                                if (banID.Length > 2) // found the nick 
                                {
                                    if (rm.banned.Contains(banID) == false)
                                    {
                                        currentRoom.banned += banID;
                                    }
                                    else
                                    {
                                        currentRoom.banned = currentRoom.banned.Replace(banID, "/");
                                    }

                                    currentRoom.banned = currentRoom.banned.Replace("//", "/");
                                    roomHash[cmdInfo.ThisUser.CurrentRoom] = currentRoom;
                                }
                                else
                                {
                                    cmdInfo.msgOut = replay + msg + " isn't a registered user";
                                }
                            }
                            else
                            {
                                cmdInfo.msgOut = replay + "You don't have the power to kick someone from this room";
                            }
                        }
                    }

                    cmdInfo.msgOut = replay + "\r\nBanned Users\r\n---------------------------------\r\n";
                    if (currentRoom.banned.Length > 2)
                    {
                        cmdInfo.msgOut += userReg.IDs2Names(currentRoom.banned, "nickname").Substring(1).Replace("/", "\r\n") + "\r\n";
                    }
                    else
                    {
                        cmdInfo.msgOut += "No banned users";
                    }

                    //cmdInfo.command = string.Empty;
                    break;


                case "KICK": // admin or moderator can kick someone out of private channel, but they can just come back in - KICK <usernick> [<message>]
                    replay = string.Format("/{0} {1}\r\n", cmdInfo.command, msg);

                    if (cmdInfo.userIdx >= 0)
                    {
                        rm = (ClassRooms)roomHash[cmdInfo.ThisUser.CurrentRoom];
                        if (rm.admin.Equals(cmdInfo.ThisUser.RegisteredGUID) || rm.moderators.Contains("/" + cmdInfo.ThisUser.RegisteredGUID + "/")) // are you an admin or moderator?
                        {
                            msg += " ";
                            cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // usernick
                            msg = msg.Substring(msg.IndexOf(" ")).Trim(); // message, if any

                            // find the user to kick
                            int targetIdx = chatServer.usersList.FindIndex(x => x.NickName.Equals(cmd, StringComparison.OrdinalIgnoreCase));

                            if (targetIdx >= 0) // found the nick 
                            {
                                targetUser = chatServer.usersList[targetIdx];

                                if (targetUser.CurrentRoom.Equals(cmdInfo.ThisUser.CurrentRoom)) // are they in this room?
                                {
                                    if (rm.admin.Equals(targetUser.UserName))  // is the person to be kicked an admin?
                                    {
                                        cmdInfo.msgOut = replay + "You can't kick the room admin";
                                    }
                                    else if (rm.moderators.Contains("/" + targetUser.RegisteredGUID + "/")) // is the person to be kicked a moderator
                                    {
                                        cmdInfo.msgOut = replay + "You can't kick a room moderator";
                                    }
                                    else
                                    {
                                        // OK, kick them back to the lobby
                                        EnterRoom(ref cmdInfo, "LOBBY", 0);
                                        chatServer.BroadcastMsgToRoom(cmdInfo.ThisUser.CurrentRoom, targetUser.NickName + " kicked out of the room by " + cmdInfo.ThisUser.NickName);
                                        chatServer.SendToGUID(targetUser.ThreadGUID, "You were kicked to the lobby" + (msg.Length > 0 ? " with message " + msg : ""));
                                        cmdInfo.command = string.Empty;
                                    }
                                }
                                else
                                {
                                    cmdInfo.msgOut = replay + targetUser.NickName + " isn't in this room";
                                }
                            }
                            else
                            {
                                cmdInfo.msgOut = replay + cmd + " isn't in this room";
                            }
                        }
                        else
                        {
                            cmdInfo.msgOut = replay + "You don't have the power to kick someone from this room";
                        }
                    }

                    break;

                case "ROOMS":
                    string rooms = "/ROOMS\r\n\r\nRoom List\r\n------------------------------\r\n\r\n";
                    foreach (DictionaryEntry r in roomHash)
                    {
                        ClassRooms rInfo = (ClassRooms)r.Value;
                        string privacy = "O";

                        privacy = PrivacyType(rInfo.privacySetting);

                        // get number of people in each seating type
                        int[] seated = new int[] { rInfo.publicSeats, rInfo.privateSeats, rInfo.speakerSeats };
                        for (int i = 0; i < chatServer.usersList.Count; i++)
                        {
                            if (chatServer.usersList[i].CurrentRoom.Equals(rInfo.name))
                            {
                                seated[chatServer.usersList[i].Seating]--;
                            }
                        }

                        // list the room and seats available
                        if (rInfo.privateSeats > 0 || rInfo.speakerSeats > 0)
                            privacy += string.Format(" - {0}/{1}/{2}", seated[0], seated[1], seated[2]);
                        else
                            privacy += string.Format(" - {0}", seated[0]);

                        rooms += string.Format("{0} ({1} seats) {2}", rInfo.name, privacy, (rInfo.topic.Length > 0 ? " - " + rInfo.topic : "")) + "\r\n";
                    }

                    cmdInfo.msgOut = rooms + "\r\n------------------------------\r\n";
                    break;

                case "IGNORE": // toggle ignoring someone
                    replay = string.Format("/{0} {1}", cmdInfo.command, msg);
                    chatServer.Ignore(cmdInfo.ThisUser.ThreadGUID, msg);
                    cmdInfo.msgOut = "Ignore List\r\n-----------------------\r\n" + IDs2NameList(cmdInfo.ThisUser.IgnoreList, "nickname");
                    cmdInfo.command = string.Empty;
                    break;

                case "RMINFO":
                    rm = (ClassRooms)roomHash[cmdInfo.ThisUser.CurrentRoom];

                    cmdInfo.msgOut = "/RMINFO\r\n";
                    cmdInfo.msgOut += string.Format("Name:       {0}\r\n", rm.name);
                    cmdInfo.msgOut += string.Format("Topic:      {0}\r\n", rm.topic);
                    cmdInfo.msgOut += string.Format("Seating:    {0}/{1}/{2}\r\n", rm.publicSeats, rm.privateSeats, rm.speakerSeats);
                    cmdInfo.msgOut += string.Format("Password:   {0}\r\n", (rm.password.Length > 0 ? "**********" : ""));
                    cmdInfo.msgOut += string.Format("Privacy:    {0} - {1}\r\n", rm.privacySetting, PrivacyType(rm.privacySetting));
                    cmdInfo.msgOut += string.Format("Admin:      {0}\r\n", IDs2NameList("/" + rm.admin + "/", "nickname"));
                    cmdInfo.msgOut += string.Format("Moderators: {0}\r\n", IDs2NameList(rm.moderators, "nickname"));
                    cmdInfo.msgOut += string.Format("Banned    : {0}\r\n\r\n", IDs2NameList(rm.banned, "nickname"));
                    break;
                    
                default:
                    break;
            }

            if (cmdInfo.msgOut.Length > 0) cmdInfo.command = string.Empty;
            return;
        }

        /*============================================================================================
         * When a user enters a room, we have to check if they are also leaving a room.  
         * When they leave the room, we need to clean up the old room in the room hash table.
         *============================================================================================*/
        public void EnterRoom(ref ClassCommandInfo cmdInfo, string roomName, int seating)
        {
            string oldRoom = cmdInfo.ThisUser.CurrentRoom;
            ClassRooms rm = (ClassRooms)roomHash[roomName];

            // If they are banned and not the room administrator or a system admin
            if (rm.banned.Contains("/" + cmdInfo.ThisUser.RegisteredGUID + "/") && rm.admin.Equals(cmdInfo.ThisUser.RegisteredGUID) == false && cmdInfo.ThisUser.IsAdmin == false)
            {
                cmdInfo.msgOut = "You are banned from entering the " + roomName + " room";
            }
            else if (cmdInfo.ThisUser.CurrentRoom.Equals(roomName))
            {
                cmdInfo.msgOut = "You are already in the " + roomName + " room";
            }
            else
            {
                ClassRooms oldRm = (ClassRooms)roomHash[oldRoom];

                // get number of people in each seating type
                int[] seated = new int[] { rm.publicSeats, rm.privateSeats, rm.speakerSeats };
                for (int i = 0; i < chatServer.usersList.Count; i++)
                {
                    if (chatServer.usersList[i].CurrentRoom.Equals(roomName))
                    {
                        seated[chatServer.usersList[i].Seating]--;
                    }
                }

                // are there any seats of this type left open?
                if (seated[seating] > 0)
                {
                    try
                    {
                        cmdInfo.ThisUser.CurrentRoom = roomName;
                        cmdInfo.ThisUser.Seating = seating;

                        // only indicate leaving if their seating is at lest at level of rooms privacy setting
                        if (cmdInfo.ThisUser.Seating >= oldRm.privacySetting)
                            chatServer.BroadcastMsgToRoom(oldRoom, string.Format("{0} has left {1}", cmdInfo.ThisUser.NickName, oldRoom));

                        if (roomName.Length > 0)
                        {
                            if (seating >= rm.privacySetting)
                            {
                                chatServer.BroadcastMsgToRoom(roomName, string.Format("{0} has entered {1}", cmdInfo.ThisUser.NickName, roomName));
                                cmdInfo.command=string.Empty;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        cmdInfo.msgOut = string.Format("A seating {0} error is keeping you out of room {1}\r\nError: {2}", seating, roomName, ex.Message);
                    }
                }
                else
                    cmdInfo.msgOut = string.Format("Sorry, no {0} seats left in {1}", (seating == 1 ? "private" : (seating == 2 ? "speaker" : "public")), roomName);
            }

            RoomCleanup(oldRoom);
        }


        /*============================================================================================
         * Now do room cleanup.  If nobody is left in the old room
         * then remove the room from the hash table
         * 
         * Keeping a room open is the job of a chat bot
         *
         * The admin and mods need to be registered by their name 
         * and not their guid so that they could lose connection
         * or leave and then come back later and pick up control
         * 
         *============================================================================================*/
        public void RoomCleanup(string oldRoom)
        {
            // room should always have something in it and we don't clean up the lobby
            if (oldRoom.Length > 0 && roomHash.ContainsKey(oldRoom) && oldRoom.Equals("LOBBY") == false)
            {
                ClassUsers iu = chatServer.usersList.Find(x => x.CurrentRoom.Equals(oldRoom));

                // if nobody is in the room, clear it
                if (iu == null)
                {
                    roomHash.Remove(oldRoom);
                    chatServer.mw.Display(string.Format("Room entry for {0} has been removed", oldRoom));
                }
            }

        }

        // convert list of usernames, nicknames, & emails to Registered ID list
        public string Names2IDs(string names)
        {
            return userReg.Names2IDs(names);
        }

        // convert registered ID list to info in format x,x,x
        public string IDs2NameList(string names, string type)
        {
            string a = IDs2Names(names, type).Replace("/", ",");
            if (a.Length > 1 && a.Substring(0, 1).Equals(",")) a = a.Substring(1);
            if (a.Length > 1 && a.Substring(a.Length - 1, 1).Equals(","))
                a = a.Substring(0, a.Length - 1);
            else
                a = string.Empty;
            return a;
        }

        // convert registered ID list to nicknames
        public string IDs2Names(string names, string type)
        {
            return userReg.IDs2Names(names, type);
        }

        public string PrivacyType(int privacy)
        {
            string[] ptype = new string[] { "Open", "Invite", "Private", "Forum - Not In Session", "Forum - In Session", "Unregistered" };
            return (privacy >= 0 && privacy < ptype.Length ? ptype[privacy] : ptype[ptype.Length - 1]);
        }
    }
}
