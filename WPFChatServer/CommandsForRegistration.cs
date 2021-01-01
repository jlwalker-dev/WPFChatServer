using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WPFChatServer
{
    class CommandsForRegistration
    {
        public Registration userRegistration;
        ChatServer cs;

        public CommandsForRegistration(ChatServer c)
        {
            cs = c;
            userRegistration = new Registration(cs.LocalPath);
        }


        public void Process(ref ClassCommandInfo cmdInfo)
        {
            string cmd;
            string msg = cmdInfo.msg;
            string replay;

            switch (cmdInfo.command)
            {
                case "LOGIN": // log into to get full rights /LOGIN <username or email> <password>
                    msg += " ";
                    cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // usernick
                    msg = msg.Substring(msg.IndexOf(" ")).Trim(); // message, if any
                    replay = string.Format("/{0} {1} **********\r\n", cmdInfo.command, cmd);

                    if (userRegistration.Login(cmd, msg))
                    {
                        string g = GetUserInfo("RecID");

                        if (cs.usersList.FindIndex(x => x.RegisteredGUID.Equals(g)) < 0)
                        {
                            cmdInfo.msgOut = replay + "Welcome " + (userRegistration.ThisUser.IsAdmin ? "administrator " : "")
                            + userRegistration.ThisUser.FullName + ".\r\nYou were last on " + userRegistration.ThisUser.lastOnDT.ToString("MMMM dd, yyyy @ h:mm tt");
                        }
                        else
                        {
                            // can't load up twice
                            cmdInfo.msgOut = replay + "You are already logged into the chat server";
                            userRegistration.ThisUser.Reset();
                        }
                    }
                    else
                    {
                        // Failure
                        if (userRegistration.errorCode > 100)
                        {
                            // System error
                            cmdInfo.display += string.Format("User registration error {0} - {1}",
                                userRegistration.errorCode, userRegistration.errorMessage);

                            cmdInfo.msgOut = replay + "You are not logged in.\r\n\r\nA system error has occured and will be logged for review";
                        }
                        else
                        {
                            // registration or login issue
                            cmdInfo.msgOut = replay + string.Format("{0}\r\nUse the /REG ? command if you need help to register", userRegistration.errorMessage);
                        }
                    }

                    if (userRegistration.errorMessage.Length > 0) cmdInfo.msgOut += (cmdInfo.msgOut.Length > 0 ? "\r\n" : "") + userRegistration.errorMessage;
                    break;

                case "REG": // register user - REG <email>/<username>/<nick>/<pw>/<full name> - /REG just returns the info collected
                    string[] userinfo = msg.Split('/');

                    if (userinfo.Length == 1)
                    {
                        // They just want to save or display of the information
                        if (msg.Equals("save", StringComparison.OrdinalIgnoreCase))
                        {
                            // Try to save
                            if (userRegistration.Register(cmdInfo.ThisUser.userRec))
                            {
                                // success!
                                cmdInfo.msgOut = "/REG SAVE\r\nYou have registered with the system";
                                cmdInfo.ThisUser.Status = "V";
                            }
                            else
                            {
                                // failed, so say why
                                cmdInfo.msgOut = "/REG SAVE\r\n" + userRegistration.errorMessage;
                            }
                        }
                        else
                        {
                            // just print out the help msg
                            cmdInfo.msgOut = "/REG " + msg + "\r\n" + userRegistration.errorMessage;
                            msg = "?";
                        }
                    }
                    else
                    {
                        // Create a blank record and fill it in
                        cmdInfo.ThisUser.userRec = userRegistration.BlankRec();

                        if (userinfo.Length > 0 && userRegistration.CheckProperty("email", userinfo[0]))
                            cmdInfo.ThisUser.userRec.SetField("email", userinfo[0].Trim().ToLower());
                        else
                            cmdInfo.msgOut += userRegistration.errorMessage + "\r\n";

                        if (userinfo.Length > 1 && userRegistration.CheckProperty("username", userinfo[1]))
                            cmdInfo.ThisUser.userRec.SetField("username", userinfo[1].Trim().ToUpper());
                        else
                            cmdInfo.msgOut += userRegistration.errorMessage + "\r\n";

                        if (userinfo.Length > 2 && userRegistration.CheckProperty("nickname", userinfo[2]))
                            cmdInfo.ThisUser.userRec.SetField("nickname", userinfo[2].Trim());
                        else
                            cmdInfo.msgOut += userRegistration.errorMessage + "\r\n";

                        if (userinfo.Length > 3 && userRegistration.CheckProperty("password", userinfo[3]))
                            cmdInfo.ThisUser.userRec.SetField("password", userinfo[3].Trim());
                        else
                            cmdInfo.msgOut += userRegistration.errorMessage + "\r\n";

                        if (userinfo.Length > 4 && userRegistration.CheckProperty("fullname", userinfo[4]))
                            cmdInfo.ThisUser.userRec.SetField("fullname", userinfo[4].Trim());
                        else
                            cmdInfo.msgOut += userRegistration.errorMessage + "\r\n";

                        msg = "?";
                    }

                    // do we need to print out the simple help message?
                    if (msg.Equals("?"))
                    {
                        // Help msg

                        if (cmdInfo.ThisUser.userRec != null)
                        {
                            // print out the user info if it exists
                            cmdInfo.msgOut += string.Format("Full Name {0}\r\n", cmdInfo.ThisUser.userRec.GetField("fullname"));
                            cmdInfo.msgOut += string.Format("User Name {0}\r\n", cmdInfo.ThisUser.userRec.GetField("username"));
                            cmdInfo.msgOut += string.Format("Nick Name {0}\r\n", cmdInfo.ThisUser.userRec.GetField("nickname"));
                            cmdInfo.msgOut += string.Format("Password  {0}\r\n", (cmdInfo.ThisUser.userRec.GetField("password").Length > 0 ? "**********" : ""));
                            cmdInfo.msgOut += string.Format("Email     {0}\r\n\r\n", cmdInfo.ThisUser.userRec.GetField("email"));
                        }

                        cmdInfo.msgOut += "/REG <email>/<username>/<nick>/<pw>/<full name>";
                        cmdInfo.msgOut += "\r\n";
                    }

                    break;

                case "UPDATE": // update your registration UPDATE <property> <new value>

                    if (cmdInfo.ThisUser.RegisteredGUID.Length == 0)
                    {
                        cmdInfo.msgOut = "/UPDATE is only available to registered users who are logged into the system.";
                    }
                    else
                    {
                        cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // usernick
                        msg = msg.Substring(msg.IndexOf(" ")).Trim(); // message, if any
                        replay = string.Format("/{0} {1} {2}\r\n", cmdInfo.command, cmd, msg);

                        if (cmd.Equals("password", StringComparison.OrdinalIgnoreCase))
                        {
                            // need to use the /PASS command
                            cmdInfo.msgOut = replay + "You must use the /PASS command to change your password";
                        }
                        else if (userRegistration.SetProperty(cmdInfo.ThisUser.RegisteredGUID, cmd, msg))
                        {
                            if (cmd.Equals("EMAIL", StringComparison.OrdinalIgnoreCase))
                            {
                                // need to verify again
                                cmdInfo.msgOut = replay + string.Format("You updated {0} to {1} and need to verify your new email address.  An email will be sent.", cmd, msg);

                                // send verify email start and set status
                                if (userRegistration.SendVerify(cmdInfo.ThisUser.RegisteredGUID))
                                    cmdInfo.msgOut = replay + "\r\nVerification email has been sent";
                                else
                                    cmdInfo.msgOut = replay +
                                        string.Format("\r\nError seding email {0} - {1}", userRegistration.errorCode, userRegistration.errorMessage);

                                cmdInfo.ThisUser.Status = "V";
                                userRegistration.SetProperty(cmdInfo.ThisUser.RegisteredGUID, "status", "V");
                            }
                            else
                            {
                                // made the change
                                cmdInfo.msgOut = replay + string.Format("You updated {0} to {1}", cmd, msg);
                            }
                        }
                        else
                            cmdInfo.msgOut = replay + string.Format("Could not update {0} to {1}", cmd, msg);
                    }

                    break;


                case "PASS": // change your password  /PASS <oldPW> <newPW>
                    msg += " ";
                    cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // usernick
                    msg = msg.Substring(msg.IndexOf(" ")).Trim(); // message, if any
                    replay = string.Format("/{0} {1} {2}\r\n", cmdInfo.command, "***", "***");

                    if (userRegistration.ChangePW(cmdInfo.ThisUser.RegisteredGUID, cmd, msg))
                    {
                        // Success
                        cmdInfo.msgOut = replay + "You've changed your password";
                    }
                    else
                    {
                        // Failure
                        cmdInfo.msgOut = replay + "Old password did not match or an invalid password was entered.";
                    }

                    break;

                case "ELEVATE":
                    msg += " ";
                    cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // guid/email/userid
                    msg = msg.Substring(msg.IndexOf(" ")).Trim(); // status
                    replay = string.Format("/{0} {1} {2}\r\n", cmdInfo.command, cmd, "***");

                    if (userRegistration.Elevate(cmd, msg))
                    {
                        cmdInfo.msgOut = replay + "Elevation complete";
                    }
                    else
                    {
                        cmdInfo.msgOut = replay + string.Format("Elevation error {0}\r\n{1}",
                            userRegistration.errorCode, userRegistration.errorMessage);
                    }

                    break;

                // resend the verify code - need to tie in for email update
                case "RESEND":
                    replay = string.Format("/{0} {1}\r\n", cmdInfo.command, msg);

                    if (cmdInfo.ThisUser.Status.Equals("V"))
                    {
                        // send a verification email
                        if (userRegistration.SendVerify(cmdInfo.ThisUser.RegisteredGUID))
                            cmdInfo.msgOut = replay + "\r\nVerification email has been sent";
                        else
                            cmdInfo.msgOut = replay +
                                string.Format("\r\nError sending email {0} - {1}", userRegistration.errorCode, userRegistration.errorMessage);
                    }
                    else
                    {
                        cmdInfo.msgOut = replay
                            + string.Format("Your status is {0}\r\nYou do not have a pending verify", cmdInfo.ThisUser.Status);
                    }

                    break;

                case "VERIFY":  // Verify by entering code sent to you during registration of email
                    replay = string.Format("/{0} {1}\r\n", cmdInfo.command, msg);

                    if (cmdInfo.ThisUser.Status.Equals("V"))
                    {
                        if (userRegistration.VerifyCode(cmdInfo.ThisUser.RegisteredGUID, msg))
                            cmdInfo.msgOut = replay + "You have verified your registration";
                        else
                            cmdInfo.msgOut = replay + "Verification failed";
                    }
                    else
                    {
                        cmdInfo.msgOut = replay + "You do not have a pending verify";
                    }
                    break;


                default:
                    break;
            }

            if (cmdInfo.ThisUser.IsAdmin)
            {
                switch (cmdInfo.command)
                {
                    case "EPASS":
                        msg += " ";
                        cmd = msg.Substring(0, msg.IndexOf(" ")).Trim(); // old PW
                        msg = msg.Substring(msg.IndexOf(" ")).Trim(); // new PW
                        replay = string.Format("/{0} {1} {2}\r\n", cmdInfo.command, "***", "***");

                        if (userRegistration.EPass(cmd, msg))
                        {
                            cmdInfo.msgOut = replay + "\r\nElevation password was changed";
                        }
                        else
                        {
                            cmdInfo.msgOut = replay + "\r\n" + userRegistration.errorMessage;
                        }
                        break;

                    case "STATUS":  // alter user status - /USERSTATUS <GUID|email> <status>
                        msg += " ";
                        cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // guid/email/userid
                        msg = msg.Substring(msg.IndexOf(" ")).Trim(); // status
                        replay = string.Format("/{0} {1} {2}\r\n", cmdInfo.command, cmd, msg);

                        if (msg.Length == 1 && Registration.StatusCodes.Contains(msg))
                        {
                            if (userRegistration.SetStatus(cmd, msg) == false)
                                cmdInfo.msgOut = replay + "Failed to set status";
                            else
                                cmdInfo.msgOut = replay + "Status updated";
                        }
                        else
                        {
                            if (msg.Length > 1)
                            {
                                // invalid status
                                cmdInfo.msgOut = replay
                                    + "Status codes are 1 character of the following: "
                                    + String.Join<char>(",", Registration.StatusCodes);
                            }
                            else
                            {
                                if (msg.Equals("?"))
                                {
                                    cmdInfo.msgOut = replay
                                        + string.Format("Satus is {1}", userRegistration.GetProperty(cmd, "STATUS"));
                                }
                            }
                        }
                        break;

                    case "USERRIGHTS": // alter user rights /USERRIGHTS <GUID|email> <rights list>
                        msg += " ";
                        cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // guid/email/userid
                        msg = msg.Substring(msg.IndexOf(" ")).Trim(); // rights list
                        replay = string.Format("/{0} {1} {2}\r\n", cmdInfo.command, cmd, msg);

                        if (msg.Equals("?"))
                        {
                            cmdInfo.msgOut = replay;
                            // TODO - query users rights
                        }
                        else
                        {
                            if (msg.Contains("/ADMIN/"))
                            {
                                cmdInfo.msgOut = replay + "You can not insert the ADMIN flag";
                            }
                            else
                            {
                                // try to set the rights - if sys admin, include the /ADMIN/ flag
                                if (userRegistration.SetRights(cmd, msg + (cmdInfo.ThisUser.IsAdmin ? "/ADMIN/" : "").Replace("//", "/")))
                                {
                                    // Success
                                    cmdInfo.msgOut = replay + "User rights updated to " + msg;
                                }
                                else
                                {
                                    // Failure, so send error info back
                                    cmdInfo.msgOut = replay + string.Format("Failed to set rights\r\nError: {0} - {1}",
                                        userRegistration.errorCode, userRegistration.errorMessage);
                                }
                            }
                        }

                        break;

                    case "CHREG": // change a property of a registration - /CHREG <GUID|email> <property> <value>
                        msg += " ";
                        cmd = msg.Substring(0, msg.IndexOf(" ")).Trim().ToUpper(); // guid/email/userid
                        string property = msg.Substring(0, msg.IndexOf(" ")).Trim(); // 
                        msg = msg.Substring(msg.IndexOf(" ")).Trim(); // status
                        replay = string.Format("/{0} {1} {2} {3}\r\n", cmdInfo.command, cmd, property, msg);


                        if (msg.Equals("?"))
                        {
                            cmdInfo.msgOut = userRegistration.GetProperty(cmd, property);

                            if (cmdInfo.msgOut == null)
                            {
                                cmdInfo.msgOut = replay + "Invalid property request";
                            }
                            else
                            {
                                cmdInfo.msgOut = replay + cmdInfo.msgOut;
                            }
                        }
                        else
                        {
                            if (userRegistration.SetProperty(cmd, property, msg))
                            {
                                cmdInfo.msgOut = replay + "Property set";
                            }
                            else
                            {
                                cmdInfo.msgOut = replay + string.Format("Failed to set property\r\nError: {0} - {1}",
                                        userRegistration.errorCode, userRegistration.errorMessage);
                            }
                        }

                        break;

                    case "REGLIST": // list all user registrations 
                                    // /REGLIST [fieldname/] [start of range [- ending of range]]
                        replay = string.Format("/{0} {1} \r\n", cmdInfo.command, msg);

                        if (msg.Contains("/"))
                        {
                            cmd = msg.Substring(0, msg.IndexOf('/') - 1).Trim();
                            msg = msg.Substring(msg.IndexOf('/') + 1);
                        }
                        else
                        {
                            cmd = "fullname";
                        }

                        if (userRegistration.GetFieldIdx(cmd) < 0)
                        {
                            // field is not in the table
                            cmdInfo.msgOut = "Field " + cmd.ToUpper() + " is not valid";
                        }
                        else
                        {
                            // split up the range values if any
                            string[] rng = msg.Split('-');
                            if (rng.Length > 0) rng[0] = rng[0].Trim().ToUpper();
                            if (rng.Length > 1) rng[1] = rng[1].Trim().ToUpper();

                            // step through the table and get all matches to a list
                            int i = 1;
                            SmallFileHandlerStructure r;
                            List<SmallFileHandlerStructure> rlist = new List<SmallFileHandlerStructure>();
                            string m = string.Empty;
                            bool useit = false;

                            while (true)
                            {
                                r = userRegistration.GetRecord(i++);

                                if (r == null || r.RecNo < 1) break;

                                // if the full name falls into the range
                                useit = rng.Length == 0 || r.GetField(cmd).ToUpper().CompareTo(rng[0]) >= 0 && (rng.Length < 2 || r.GetField(cmd).ToUpper().CompareTo(rng[1]) < 1);

                                if (useit)
                                {
                                    // look for a place to insert this record
                                    for (int j = 0; j < rlist.Count; j++)
                                    {
                                        if (r.GetField("fullname").ToUpper().CompareTo(rlist[j].GetField("fullname").ToUpper()) < 0)
                                        {
                                            rlist.Insert(j, r);
                                            r = null;
                                            break;
                                        }
                                    }

                                    // did not get inserted, so add to end of list
                                    if (r != null) rlist.Add(r);
                                }
                            }

                            // sort the list if it's greater than 1 element
                            if (rlist.Count > 1)
                            {
                                // Sort the list
                                rlist.Sort((x, y) => x.GetField("fullname").CompareTo(y.GetField("fullname")));
                            }

                            // Format the report to return to client
                            for (int j = 0; j < rlist.Count; j++)
                            {
                                m += rlist[j].GetField("FullName") + " (" + rlist[j].GetField("NickName") + ")"
                                    + "\r\n    Email :" + rlist[j].GetField("email")
                                    + "\r\n    Status: " + rlist[j].GetField("Status") + "\r\n\r\n";

                            }

                            cmdInfo.msgOut = replay + (m.Length > 0 ? m : "No Matching Records");
                        }

                        break;

                    default:
                        break;
                }
            }

            // Was anything updated?
            if (userRegistration.ThisUser.WasChanged)
            {
                if (userRegistration.ThisUser.Dif("FullName"))
                    cmdInfo.ThisUser.FullName = GetUserInfo("FullName"); // userRegistration.ThisUser.FullName;

                if (userRegistration.ThisUser.Dif("NickName"))
                    cmdInfo.ThisUser.NickName = GetUserInfo("NickName"); // userRegistration.ThisUser.NickName;

                if (userRegistration.ThisUser.Dif("UserName"))
                    cmdInfo.ThisUser.UserName = GetUserInfo("UserName"); // userRegistration.ThisUser.UserName;

                if (userRegistration.ThisUser.Dif("RegisteredGUID"))
                    cmdInfo.ThisUser.RegisteredGUID = GetUserInfo("RecID"); // userRegistration.ThisUser.RegisteredGUID;

                if (userRegistration.ThisUser.Dif("Email"))
                    cmdInfo.ThisUser.Email = GetUserInfo("Email"); // userRegistration.ThisUser.Email;

                if (userRegistration.ThisUser.Dif("UserRights"))
                    cmdInfo.ThisUser.UserRights = GetUserInfo("Rights"); // userRegistration.ThisUser.UserRights;

                if (userRegistration.ThisUser.Dif("Status"))
                    cmdInfo.ThisUser.Status = GetUserInfo("Status"); // userRegistration.ThisUser.Status;
            }

            if (cmdInfo.msgOut.Length > 0) cmdInfo.command = string.Empty;
        }

        public SmallFileHandlerStructure FindUser(string info)
        {
            return userRegistration.GetUserRecord(info);
        }

        public dynamic GetUserInfo(string guid, string field)
        {
            SmallFileHandlerStructure rec = FindUser(guid);
            return rec?.GetField(field.ToUpper());
        }

        public dynamic GetUserInfo(string field)
        {
            dynamic Result = "";

            switch (field.ToUpper())
            {
                case "LASTONDT":
                    Result = userRegistration.ThisUser.lastOnDT;
                    break;

                case "REGISTEREDDT":
                    Result = userRegistration.ThisUser.registeredDT;
                    break;

                case "MSGCOUNT":
                    Result = userRegistration.ThisUser.msgCount;
                    break;

                case "IPADDRESS":
                    Result = userRegistration.ThisUser.ipAddress;
                    break;

                case "LASTMSGDT":
                    Result = userRegistration.ThisUser.lastMsgDT;
                    break;

                case "FULLNAME":
                    Result = userRegistration.ThisUser.FullName;
                    break;

                case "USERNAME":
                    Result = userRegistration.ThisUser.UserName;
                    break;

                case "NICKNAME":
                    Result = userRegistration.ThisUser.NickName;
                    break;

                case "EMAIL":
                    Result = userRegistration.ThisUser.Email;
                    break;

                case "USERRIGHTS":
                    Result = userRegistration.ThisUser.UserRights;
                    break;

                case "STATUS":
                    Result = userRegistration.ThisUser.Status;
                    break;

                case "RECID":
                case "REGISTEREDGUID":
                    Result = userRegistration.ThisUser.RegisteredGUID;
                    break;
            }

            return Result;
        }


    }
}
