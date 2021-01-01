using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFChatServer
{
    class CommandsForUsers
    {
        ChatServer chatServer;

        public CommandsForUsers(ChatServer cs)
        {
            chatServer = cs;
        }

        public void Process(ref ClassCommandInfo cmdInfo)
        {
            string msg = cmdInfo.msg;
            string replay;
            string myThreadID = cmdInfo.ThisUser.ThreadGUID;
            ClassUsers targetUser;



            switch (cmdInfo.command)
            {
                case "NICK": // Change the nickname
                             // no spaces, no parens, 20 char max - dems da rules
                    msg = msg.Replace("(", "").Replace(")", "").Replace(" ", "");
                    msg = (msg.Length > 20 ? msg.Substring(0, 20) : msg);
                    replay = string.Format("/{0} {1}", cmdInfo.command, msg);

                    if (chatServer.NickExists(msg) >= 0)
                    {
                        cmdInfo.Results = cmdInfo.ThisUser.NickName + " has changed their nickname to " + msg;
                        cmdInfo.ThisUser.NickName = msg.Trim();
                        cmdInfo.command = string.Empty;
                    }
                    else
                        cmdInfo.msgOut = replay + "Nickname already exists";

                    break;


                case "HELP": // return help screen - HELP.txt
                    chatServer.Help(cmdInfo.ThisUser.ThreadGUID, msg);
                    cmdInfo.command = string.Empty;
                    break;

                case "ONLINE": // return user list - LIST
                    string names = "/NAMES\r\n\r\nListing Who in Room @ time of last msg\r\n-------------------------------------------------------\r\n";
                    for (int i = 0; i < chatServer.usersList.Count; i++)
                        names += string.Format("{0} in {1} @ {2}", chatServer.usersList[i].NickName.ToUpper(), chatServer.usersList[i].CurrentRoom, chatServer.usersList[i].lastMsgDT.ToString("HH:mm")) + "\r\n";
                    cmdInfo.msgOut = names + "\r\n-------------------------------------------------------\r\n";
                    break;

                case "AWAY": // add or overwrite the away message
                    int me = chatServer.usersList.FindIndex(x => x.ThreadGUID.Equals(myThreadID));
                    if (me >= 0) cmdInfo.ThisUser.AwayMsg = msg.Trim();
                    cmdInfo.Results = cmdInfo.ThisUser.NickName + ": " + msg;
                    cmdInfo.command = string.Empty;
                    break;

                case "WHO": // more info on a user
                    replay = string.Format("/{0} {1}", cmdInfo.command, msg);
                    targetUser = chatServer.usersList.Find(x => x.NickName.Equals(msg, StringComparison.OrdinalIgnoreCase));
                    if (targetUser == null)
                        cmdInfo.msgOut = replay + "There is no one named " + msg.ToUpper().Trim();
                    else
                        cmdInfo.msgOut = replay + string.Format("{0} -> Name: {1}    IP: {2}    Machine: {3}    Msgs: {4}",
                            msg.Trim(), targetUser.UserName, targetUser.ipAddress, targetUser.computerName, targetUser.msgCount);
                    break;


                default:
                    break;
            }

            if (cmdInfo.msgOut.Length > 0) cmdInfo.command = string.Empty; ;
        }
    }
}
