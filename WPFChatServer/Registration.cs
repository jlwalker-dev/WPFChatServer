/*
 * This is where all registration work is done.
 * 
 * History
 *  2020-11-19
 *      Initial setup.  This module is designed to do all of the work
 *      related to registering, logging in, and handling the database
 *      for the user registration system.
 *      
 *      When a person is registered, their information will be placed 
 *      into the public properties.  Each time this class is run, it
 *      should either clear out or update the properties.
 *      
 *      This class is intended to be self contained an not require
 *      anything from the associated project, making it possible to
 *      attach a registration system to any project.
 * 
 * 2020-11-20
 *      Have Small Data Handler class working, along with registering
 *      a user, logging them in, and changing password... well damn.
 *
 *  2020-11-22
 *      Got back to it today and fished up initial coding of the
 *      registration module.  This code is designed to be able to
 *      drop into any project and give simple registration capabilities
 *      to any project.
 *      
 *  2020-11-28
 *      Starting switch over to field names instead of field indexes
 *      
 *      
 * Data record is layed out in a 300 character record expecting 12 fields:
 *
 * FieldNames string: RECID,FULLNAME,USERNAME,NICKNAME,PASSWORD,EMAIL,REGISTERDATE,LASTDATE,STATUS,VERIFYCODE,RIGHTS
 *                          Expected
 * Field    Name            Max Length
 *  0       GUID             25
 *  1       Full Name        40
 *  2       User Name        20
 *  3       Nick Name        20
 *  4       Password         20
 *  5       EMail            40
 *  6       Register Date    20
 *  7       Last On          20
 *  8       Status            1
 *  9       VerifyCode       10
 * 10       Rights           10
 * 11       unused            0
 *                          ---
 *                          216
 *  
 */
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace WPFChatServer
{
    class Registration
    {
        // set up some static registration properties used in the system
        private static int recordLength = 300;
        private static int fieldCount = 12;
        private static string statusCodes = "AINPV";  // Active,Inactive Verified, Not active not verified, Probation, Verify pending
        public static int RecordLength { get => recordLength; private set => recordLength = value; }
        public static int FieldCount { get => fieldCount; private set => fieldCount = value; }
        public static string StatusCodes { get => statusCodes; private set => statusCodes = value; }


        // ThisUser is cleared whenever a method is called that may alter the
        // user properties and then changed by the method.  The change tracking
        // code in the class will then indicate what needs to be updated by the 
        // calling routine to keep the original user record up to date.
        public ClassUsers ThisUser;

        public int errorCode = 0;
        public string errorMessage = string.Empty;

        private long Counter = 0;
        private int ProbationPeriod = 90;

        private string AdminRightsPW = "TooShort";

        public bool Available { get; private set; }

        private SmallDataHandler sdh;
        private string RegistrationFile = "ChatUserRegister";  // Greater than 15 chars so a room name won't overwrite
        public readonly string localPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";

        Regex regex = new Regex("[^0-9a-zA-Z_-]+");

        public Registration(string dataPath)
        {
            LoadConfig();

            Available = false;

            try
            {
                sdh = new SmallDataHandler(dataPath);

                // do we need to create the user register?
                if (File.Exists(sdh.DataPath + RegistrationFile + SmallDataHandler.dataExt) == false)
                {
                    if (sdh.Create(RegistrationFile, recordLength, fieldCount))
                        sdh.Close();
                    else
                    {
                        errorCode = 300;
                        errorMessage = string.Format("Registration system is not available\r\n"
                            + "Data handler returned error {0} - {1}", sdh.ErrorCode, sdh.ErrorMessage);
                    }
                }

                // now open the user register
                if (sdh.Open(RegistrationFile))
                {
                    // make sure it has field names
                    if (sdh.HasFieldNames == false)
                        sdh.SetFieldNames("RECID,FULLNAME,USERNAME,NICKNAME,PASSWORD,EMAIL,REGISTERDATE,LASTDATE,STATUS,VERIFYCODE,RIGHTS");

                    Available = true;
                    ClearAll();
                }
                else
                {
                    errorCode = 300;
                    errorMessage = string.Format("Registration system is not available\r\n"
                        + "Data handler returned error {0} - {1}", sdh.ErrorCode, sdh.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                errorCode = 300;
                errorMessage = string.Format("Registration system is not available\r\n"
                    + "System error {0}", ex.Message);
            }
            finally
            {
                // always close the file after use
                if (sdh != null) sdh.Close();
            }
        }


        /*
         * Clear the class properties
         */
        public void ClearAll()
        {
            if (Available)
            {
                // clear the error codes
                errorCode = 0;
                errorMessage = string.Empty;

                Counter++;  // make sure counter is incremented often

                ThisUser = new ClassUsers(CreateGUID(false), string.Empty, string.Empty, string.Empty, string.Empty);
                ThisUser.userRec = BlankRec();
                ThisUser.Reset();
            }
            else
            {
                // if you didn't pay attention to the fact that the system
                // is not available, I'm going to be inelegant telling you about it
                throw new FileLoadException("Registration system is not available");
            }
        }


        /*
         * Look for the email (if a @ is present), otherwise the username
         * and verify the password.  If correct, load the class properties 
         * with the information for this user.
         */
        public bool Login(string searchVal, string password)
        {
            bool Result = false;

            // clear the user related properties
            ClearAll();

            // can we open the registration file?
            if (sdh.Open(RegistrationFile))
            {
                // try to validate the user
                try
                {
                    // find by email or user name
                    //SmallFileHandlerStructure rec = sdh.FindRec((email.Contains("@") ? 5 : 2), email, SmallDataHandler.Match.ExactCaseInsensitive);
                    SmallFileHandlerStructure rec =
                        sdh.FindRec((searchVal.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("username")), searchVal, SmallDataHandler.Match.ExactCaseInsensitive);

                    // recno is > 0 if we found it so check to see if a deleted record
                    // and also make sure they are not inactive, suspended, or banned
                    if (rec.RecNo > 0 && rec.Deleted == false && "IN".Contains(rec.GetField("status")) == false)  //.Fields[8]
                    {
                        // Check the PW
                        if (password.Equals(rec.GetField("password"))) //rec.Fields[4]))
                        {
                            ThisUser.userRec = rec;
                            ThisUser.userRec.RecNo = rec.RecNo;
                            ThisUser.RegisteredGUID = rec.GetField("recid"); // rec.Fields[0];
                            ThisUser.FullName = rec.GetField("fullname"); // rec.Fields[1];
                            ThisUser.UserName = rec.GetField("username"); // rec.Fields[2];
                            ThisUser.NickName = rec.GetField("nickname"); //  rec.Fields[3];
                            ThisUser.Email = rec.GetField("email"); // rec.Fields[5];

                            if (DateTime.TryParse(rec.GetField("registerdate"), out ThisUser.registeredDT) == false) //.Fields[6]
                            {
                                ThisUser.registeredDT = DateTime.Now;
                                errorMessage += "User Creation Time set to current time.";
                            }

                            if (DateTime.TryParse(rec.GetField("lastdate"), out ThisUser.lastOnDT) == false) //.Fields[7]
                            {
                                errorMessage += "User Last-On Time set to current time.";
                                ThisUser.lastOnDT = DateTime.Now;
                            }

                            ThisUser.Status = rec.GetField("status"); // .Fields[8];
                            ThisUser.UserRights = rec.GetField("rights"); //.Fields[10];

                            // remember when the last time they were on
                            rec.SetField("LASTDATE", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));

                            // Validated user!  Now check status - if P and over 90 days, change to A
                            if (ThisUser.registeredDT.Year > 1900)
                            {
                                TimeSpan d = DateTime.Now - ThisUser.registeredDT;
                                if (d.Days >= ProbationPeriod)
                                {
                                    // Change status to A
                                    ThisUser.Status = "A";

                                    // And update the record
                                    rec.SetField("status", "A"); //.Fields[8]
                                }
                            }
                            else
                            {
                                // put in a new value
                                ThisUser.registeredDT = DateTime.Now;
                                rec.SetField("registerdate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                            }

                            // save the changes
                            sdh.SaveRec(rec);

                            Result = true;
                        }
                    }

                    if (Result == false)
                    {
                        if (rec.Deleted)
                        {
                            errorCode = 2;
                            errorMessage = "Registration record is marked as deleted";
                        }
                        else if (rec.RecNo > 0)
                        {
                            errorCode = 3;
                            errorMessage = "Registration marked as inactive.";
                        }
                        else
                        {
                            errorCode = 1;
                            errorMessage = "User or Password mismatch";
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCode = 101;
                    errorMessage = string.Format("LOGIN error - {0}", ex.Message);
                }

                // try to close the registration file
                try
                {
                    sdh.Close();
                }
                catch (Exception ex)
                {
                    errorCode = 102;
                    errorMessage = string.Format("LOGIN error on Close() - {0}", ex.Message);
                }
            }
            else
            {
                errorCode = 121;
                errorMessage = "Could not open registration file";
            }

            sdh.Close();

            return Result;
        }


        /*
         * Look for a Regsitration.xml and if found, update the internal properties
         * If it doesn't exist, create one so the user can update it later.
         * 
         * properties that can be changed are:
         * 
         *  Probation (min 0=none, max 180 days);
         *  AdminPW (must be at least 9 characters)
         *  StatusCodes (Can add onto the basic 4)
         *  DataFile (name must be over 15 characters)
         * 
         */
        private void LoadConfig()
        {
            string configFile = localPath + "Registration.xml";
            string value;
            string setting;

            if (File.Exists(configFile) == false)
            {
                // create the file
                string x = "<Registration>\r\n" +
                            "  <Properties>\r\n" +
                            "    <AdminPW>CoviD-19</AdminPW>\r\n" +
                            "  </Properties>\r\n" +
                            "</Registration>";

                File.WriteAllText(configFile, x);
            }

            if (File.Exists(configFile))
            {
                try
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
                            if (int.TryParse(value, out int ivalue) == false) ivalue = 0;

                            setting = xmlnode[i].ChildNodes.Item(j).Name.ToUpper().Trim();

                            switch (setting)
                            {
                                case "ADMINPW":
                                    AdminRightsPW = (Enumerable.Range(0, 180).Contains(value.Length) ? value : "TooShort");
                                    break;

                                case "STATUSCODES":
                                    statusCodes = "ANPV" + value;
                                    break;

                                case "DATAFILE":
                                    RegistrationFile = (value.Length > 15 ? value : "ChatUserRegister");
                                    break;

                                case "PROBATION":
                                    ProbationPeriod = (Enumerable.Range(0, 180).Contains(ivalue) ? ivalue : 90);
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCode = 100;
                    errorMessage = ex.Message;
                }
            }

        }

        public SmallFileHandlerStructure BlankRec()
        {
            SmallFileHandlerStructure Result = null;

            if (Available)
            {
                if (sdh.IsOpen() == false) sdh.Open(RegistrationFile);
                Result = sdh.BlankRec();
                sdh.Close();
            }

            return Result;
        }


        /*
         * Register an email, username, fullname, nickname into the database
         * and set as Verification Pending.  If there is a conflict with
         * email, username, or nickname, then return a false and set the 
         * error properties accordingly.
         */
        public bool Register(SmallFileHandlerStructure regInfo)
        {
            bool Result = false;

            ClearAll();

            ThisUser.RegisteredGUID = CreateGUID(false);
            ThisUser.Status = "V";
            ThisUser.registeredDT = DateTime.Now;
            ThisUser.lastOnDT = DateTime.Now;

            //------------------------------------------------------------
            // do some initial sanity checks on required information
            //------------------------------------------------------------

            // check email validity 
            string test = regInfo.GetField("email");
            if (test.Contains("@") == false || test.Contains(".") == false || test.Length < 7)
            {
                errorCode = 1;
                errorMessage = "Email format is incorrect";
            }

            // check fullname
            test = regInfo.GetField("fullname");
            if (test.Contains(" ") == false || test.Length < 4)
            {
                errorCode = 2;
                errorMessage = "Full name requires first/last names";
            }

            // check username
            test = regInfo.GetField("username");
            if (test.Length < 3)
            {
                errorCode = 3;
                errorMessage = "Username is too short";
            }

            // check nickname
            test = regInfo.GetField("nickname");
            if (test.Length < 3)
            {
                errorCode = 4;
                errorMessage = "Nickname is too short";
            }

            if (errorCode == 0)
            {
                try
                {
                    // open the fle
                    if (sdh.Open(RegistrationFile))
                    {
                        // check for confilcts
                        SmallFileHandlerStructure rec = sdh.FindRec(sdh.GetFieldIdx("email"), regInfo.GetField("email"), SmallDataHandler.Match.ExactCaseInsensitive);

                        if (rec.RecNo == 0)
                        {
                            rec = sdh.FindRec(sdh.GetFieldIdx("username"), regInfo.GetField("username"), SmallDataHandler.Match.ExactCaseInsensitive);

                            if (rec.RecNo == 0)
                            {
                                rec = sdh.FindRec(sdh.GetFieldIdx("nickname"), regInfo.GetField("nickname"), SmallDataHandler.Match.ExactCaseInsensitive);
                                if (rec.RecNo == 0)
                                {
                                    // if none, append the record
                                    regInfo.SetField("recid", ThisUser.RegisteredGUID);
                                    regInfo.SetField("registerdate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                                    regInfo.SetField("lastdate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                                    regInfo.SetField("status", "V");
                                    regInfo.SetField("verifycode", CreateGUID(false).Substring(0, 6).Replace("/", "A").Replace("+", "6").ToUpper());
                                    regInfo.SetField("rights", string.Empty);

                                    ThisUser.Reset();

                                    regInfo = sdh.AddRec(regInfo);

                                    // recno>0 means it was added
                                    if (regInfo.RecNo > 0)
                                    {
                                        Result = true;

                                        ThisUser.RegisteredGUID = regInfo.GetField("recid"); // rec.Fields[0];
                                        ThisUser.FullName = regInfo.GetField("fullname"); // rec.Fields[1];
                                        ThisUser.UserName = regInfo.GetField("username"); // rec.Fields[2];
                                        ThisUser.NickName = regInfo.GetField("nickname"); // rec.Fields[3];
                                        ThisUser.Email = regInfo.GetField("email"); // rec.Fields[5];
                                        ThisUser.UserRights = regInfo.GetField("rights"); // string.Empty;
                                        ThisUser.registeredDT = DateTime.Now;
                                        ThisUser.lastOnDT = DateTime.Now;

                                        ThisUser.Status = regInfo.GetField("status"); //rec.Fields[8];

                                        // Now send out the verification email
                                        SendSMTP smtp = new SendSMTP();
                                        if (smtp.SendVerifyCode(ThisUser.Email, regInfo.GetField("verifycode")) == false)
                                        {
                                            errorCode = 39;
                                            errorMessage = smtp.ErrorMessage;
                                        }
                                    }
                                    else
                                    {
                                        errorCode = 34;
                                        errorMessage = "Failed to save registration record" + sdh.ErrorMessage;
                                    }
                                }
                                else
                                {
                                    errorCode = 31;
                                    errorMessage = "Nickname is already in use";
                                }
                            }
                            else
                            {
                                errorCode = 32;
                                errorMessage = "Username is already in use";
                            }
                        }
                        else
                        {
                            errorCode = 33;
                            errorMessage = "Email account is already registered.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCode = (sdh.ErrorCode > 0 ? sdh.ErrorCode : 109);
                    errorMessage = ex.Message + "\r\n" + sdh.ErrorMessage;
                }
                finally
                {
                    // close the file up
                    sdh.Close();
                }
            }

            return Result;
        }

        public bool SendVerify(string guid)
        {
            bool Results = true;

            if (Available)
            {
                // open the file
                if (sdh.Open(RegistrationFile))
                {
                    // find the record
                    SmallFileHandlerStructure rec =
                        sdh.FindRec((guid.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("recid")), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                    if (rec.RecNo > 0)
                    {
                        // Now send out the verification email
                        SendSMTP smtp = new SendSMTP();
                        if (smtp.SendVerifyCode(rec.GetField("Email"), rec.GetField("verifycode")) == false)
                        {
                            Results = false;
                            errorCode = 39;
                            errorMessage = smtp.ErrorMessage;
                        }
                    }
                }

                sdh.Close();
            }
            else
            {
                Results = false;
                errorCode = 39;
                errorMessage = "Registration system unavailable";
            }
            return Results;
        }


        /*
         * Backdoor way to set up an administrator.  Set the PW via 
         * via the configuration file.  If the PW is less than eight
         * characters in length, the command is disabled.
         */
        public bool Elevate(string guid, string pw)
        {
            bool Result = false;

            if (AdminRightsPW.Length < 9)
            {
                errorCode = 330;
                errorMessage = "Elevate command has been disabled";
            }
            else if (pw.Equals(AdminRightsPW))
            {
                // get rights of user
                string rights = GetProperty(guid, "RIGHTS");
                if (rights == null)
                {
                    errorCode = 330;
                    errorMessage = "Unexpected NULL returned from data handler";
                }
                else if (rights.Contains("/ADMIN/"))
                {
                    errorCode = 330;
                    errorMessage = "User is already an administrator.";
                }
                else
                {
                    // add /ADMIN/ to it
                    rights = (rights + "/ADMIN/").Replace("//", "/");

                    // save the rights of the user
                    if (SetRights(guid, rights))
                    {
                        Result = true;
                    }
                    else
                    {
                        errorCode = 330;
                        errorMessage = string.Format("Failed to set rights in data handler.\r\nError: {0} - {1}",
                            sdh.ErrorCode, sdh.ErrorMessage);
                    }
                }
            }

            return Result;
        }

        public bool EPass(string oldPW, string newPW)
        {
            bool Result = false;
            string configFile = localPath + "Registration.xml";

            if (oldPW.Equals(AdminRightsPW))
            {
                try
                {
                    XmlDocument xmldoc = new XmlDocument();
                    xmldoc.Load(configFile);
                    XmlNode aNode = xmldoc.SelectSingleNode("/Registration/Properties/AdminPW");
                    aNode.InnerText = newPW;
                    xmldoc.Save(configFile);
                    AdminRightsPW = newPW;
                    Result = true;
                }
                catch
                {
                    errorCode = 300;
                    errorMessage = "Failed to save new elevation password";
                }
            }
            else
            {
                errorCode = 300;
                errorMessage = "Old password does not match";
            }

            return Result;
        }

        /*
         * Verify the code given by the user against the expected
         * verification code.  If it matches, put user into P status.
         *
         * After x days, user automatically reverts to A status
         * during login.
         * 
         * If the guid has a @ then search by email
         */
        public bool VerifyCode(string guid, string code)
        {
            bool Results = true;
            ClearAll();

            try
            {
                if (sdh.Open(RegistrationFile))
                {
                    SmallFileHandlerStructure rec =
                        sdh.FindRec((guid.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("recid")), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                    if (rec.RecNo > 0)
                    {
                        // field 9 is verify code
                        if (code.Equals(rec.GetField("verifycode")) && rec.GetField("status").Equals("V")) // 9 & 8
                        {
                            // if verified, put to P)robationary status
                            rec.SetField("status", "P");

                            // Update Registration date
                            rec.SetField("registerdate", DateTime.Now.ToUniversalTime().ToString("yyyy:MM:dd HH:mm:ss"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorCode = (sdh.ErrorCode > 0 ? sdh.ErrorCode : 109);
                errorMessage = ex.Message + "\r\n" + sdh.ErrorMessage;
            }

            sdh.Close();

            return Results;
        }


        /*
         * Change users password if there is a match with the old password
         * 
         * If the guid has a @ then search by email
         */
        public bool ChangePW(string guid, string oldpw, string newpw)
        {
            bool Results = false;

            ClearAll();

            // open the file
            if (sdh.Open(RegistrationFile))
            {
                try
                {
                    // check for confilcts
                    SmallFileHandlerStructure rec =
                        sdh.FindRec((guid.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("recid")), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                    if (rec.RecNo > 0)
                    {
                        if (rec.GetField("password").Equals(oldpw))  // field 4
                        {
                            rec.SetField("password", newpw);
                            rec = sdh.SaveRec(rec);

                            if (rec.RecNo == 0)
                            {
                                errorCode = sdh.ErrorCode;
                                errorMessage = string.Format("Failed to save password\r\nError: {0} - {1}", sdh.ErrorCode, sdh.ErrorMessage);
                            }
                            else
                            {
                                Results = true;
                            }
                        }
                        else
                        {
                            errorCode = 300;
                            errorMessage = "Old password does not match.  Password was not changed.";
                        }
                    }
                    else
                    {
                        errorCode = 300;
                        errorMessage = "Could not find registration record";
                    }
                }
                catch (Exception ex)
                {
                    errorCode = 100;
                    errorMessage = ex.Message;
                }
            }
            else
            {
                errorCode = 300;
                errorMessage = string.Format("Registration file did not open.\r\nError {0} - {1}", sdh.ErrorCode, sdh.ErrorMessage);
            }

            sdh.Close();

            return Results;
        }


        /*
         * Set the member's status to 
         *    A)ctive and Verified
         *    I)nactive and verified
         *    P)robationary
         *
         *    N)ot active and not verified
         *    V)erification pending
         *    
         *    if the GUID has a @ then search by email
         */
        public bool SetStatus(string guid, string status)
        {
            bool Result = false;

            ClearAll();

            try
            {
                if (sdh.Open(RegistrationFile))
                {
                    SmallFileHandlerStructure rec =
                        sdh.FindRec((guid.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("recid")), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                    // status is 1 character and upper case
                    status = status.ToUpper().Substring(0, 1);

                    if (rec.RecNo > 0)
                    {
                        if (StatusCodes.Contains(status))
                        {
                            if ("VN".Contains(rec.GetField("Status")) && "VN".Contains(status) == false)
                            {
                                errorCode = 300;
                                errorMessage = "You cannot change an unverified status code to a verified status code";
                            }
                            else
                            {
                                // update the status in the user's record
                                rec.SetField("Status", status);
                                sdh.SaveRec(rec);
                                ThisUser.Status = status;
                                Result = true;
                            }
                        }
                        else
                        {
                            errorCode = 300;
                            errorMessage = "Invalid status code " + status;
                        }
                    }
                    else
                    {
                        errorCode = 300;
                        errorMessage = "Registration record not found";
                    }
                }
            }
            catch (Exception ex)
            {
                errorCode = (sdh.ErrorCode > 0 ? sdh.ErrorCode : 109);
                errorMessage = ex.Message + "\r\n" + sdh.ErrorMessage;
            }

            sdh.Close();
            return Result;
        }



        /*
         * Set the member's status to 
         *
         *    if the GUID has a @ then search by email
         */
        public bool SetRights(string guid, string rights)
        {
            bool Result = false;

            ClearAll();

            try
            {
                if (sdh.Open(RegistrationFile))
                {
                    SmallFileHandlerStructure rec =
                        sdh.FindRec((guid.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("recid")), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                    // Rights are uppercase
                    rights = rights.ToUpper();

                    if (rights.Contains("|"))
                    {
                        errorCode = 300;
                        errorMessage = "You cannot put a pipe character '|' into the rights field.";
                    }
                    else
                    {
                        // update the rights in the user's record
                        rec.SetField("Rights", rights);
                        sdh.SaveRec(rec);
                        ThisUser.UserRights = rights;
                        Result = true;
                    }

                }
            }
            catch (Exception ex)
            {
                errorCode = (sdh.ErrorCode > 0 ? sdh.ErrorCode : 109);
                errorMessage = ex.Message + "\r\n" + sdh.ErrorMessage;
            }

            sdh.Close();
            return Result;
        }


        public SmallFileHandlerStructure GetRecord(int rec)
        {
            SmallFileHandlerStructure r = null;

            if (sdh.Open(RegistrationFile))
            {
                r = sdh.ReadAtIndex(rec);
            }
            else
            {
                errorCode = sdh.ErrorCode;
                errorMessage = sdh.ErrorMessage;
            }

            sdh.Close();

            return r;
        }


        /*
         * Set a property in the user's record
         * 
         * Make your changes here for system specific needs, like
         * phone number, address, etc.  Remember to adjust field
         * count and record length when you add properies.
         * 
         * If the guid has a @ then search by email
         */
        public bool SetProperty(string guid, string property, string value)
        {
            bool Result;
            ClearAll();

            if (CheckProperty(property, value))
            {
                try
                {
                    if (sdh.Open(RegistrationFile))
                    {
                        SmallFileHandlerStructure rec =
                            sdh.FindRec((guid.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("recid")), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                        if (rec.RecNo > 0)
                        {
                            switch (property)
                            {
                                case "FULLNAME":

                                    // make sure they are sending us something
                                    // that might actually be a real name
                                    rec.SetField("FullName", value);
                                    sdh.SaveRec(rec);
                                    if (sdh.ErrorCode == 0)
                                        ThisUser.FullName = value; // save was successful so update value
                                    else
                                    {
                                        this.errorCode = sdh.ErrorCode;
                                        this.errorMessage = sdh.ErrorMessage;
                                    }
                                    break;

                                case "USERNAME":
                                    rec.SetField("Username", value.ToUpper());
                                    sdh.SaveRec(rec);
                                    if (sdh.ErrorCode == 0)
                                        ThisUser.UserName = value.ToUpper();
                                    else
                                    {
                                        this.errorCode = sdh.ErrorCode;
                                        this.errorMessage = sdh.ErrorMessage;
                                    }

                                    break;

                                case "NICKNAME":
                                    rec.SetField("Nickname", value.ToUpper());
                                    sdh.SaveRec(rec);
                                    if (sdh.ErrorCode == 0)
                                        ThisUser.NickName = value.ToUpper();
                                    else
                                    {
                                        this.errorCode = sdh.ErrorCode;
                                        this.errorMessage = sdh.ErrorMessage;
                                    }

                                    break;

                                case "PASSWORD":
                                    rec.SetField("Password", value);
                                    sdh.SaveRec(rec);
                                    if (sdh.ErrorCode != 0)
                                    {
                                        this.errorCode = sdh.ErrorCode;
                                        this.errorMessage = sdh.ErrorMessage;
                                    }
                                    break;

                                case "EMAIL":
                                    if (rec.GetField("email").Equals(value, StringComparison.InvariantCultureIgnoreCase) == false)
                                    {
                                        // new email must be verified
                                        rec.SetField("email", value.ToLower());

                                        rec.SetField("status", "V"); // force a new verify of email
                                        sdh.SaveRec(rec);

                                        if (sdh.ErrorCode == 0)
                                            ThisUser.Email = value.ToLower();
                                        else
                                        {
                                            this.errorCode = sdh.ErrorCode;
                                            this.errorMessage = sdh.ErrorMessage;
                                        }
                                    }
                                    break;

                                default:
                                    this.errorCode = 300;
                                    this.errorMessage = string.Format("Unknown property '{0}'", property);

                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCode = (sdh.ErrorCode > 0 ? sdh.ErrorCode : 109);
                    errorMessage = ex.Message + "\r\n" + sdh.ErrorMessage;
                    Result = false;
                }
            }

            Result = (this.errorCode == 0);

            sdh.Close();
            return Result;
        }


        public bool CheckProperty(string property, string value)
        {
            bool Result = false;

            switch (property)
            {
                case "FULLNAME":

                    // make sure they are sending us something
                    // that might actually be a real name
                    string test = value.Replace(" ", "_");
                    test = Regex.Replace(test, "[^a-zA-Z_]+", "").Replace("_", " ");

                    if (value.Length > 4 && value.Contains(" ") && test.Equals(value))
                    {
                        Result = true;
                    }
                    else
                    {
                        this.errorCode = 300;
                        this.errorMessage = "Full names must be in at least two parts and contain only letters and spaces. Ex: John Smith";
                    }
                    break;

                case "USERNAME":
                    // make sure they are sending us something
                    // that might actually be a real name
                    if (value.Length > 2 && regex.Replace(value, "").Equals(value) && "ABCDEFGHIJKLMNOPQURSTUVWXYZ".Contains(value.Substring(0, 1)))
                    {
                        Result = true;
                    }
                    else
                    {
                        this.errorCode = 300;
                        this.errorMessage = "User names must not have any spaces and contain only letters and numbers and start with a letter";
                    }


                    break;

                case "NICKNAME":

                    if (value.Length > 2 && regex.Replace(value, "").Equals(value))
                    {
                        Result = true;
                    }
                    else
                    {
                        this.errorCode = 300;
                        this.errorMessage = "Nick names must be one word and contain only letters and numbers";
                        Result = false;
                    }

                    break;

                case "PASSWORD":
                    if (value.Length > 7)
                    {
                        Result = true;
                    }
                    else
                    {
                        this.errorCode = 300;
                        this.errorMessage = "Passwords must be at least 8 characters in length";
                    }
                    break;

                case "EMAIL":
                    if (value.Length > 3 && value.Contains("@") && value.Contains("."))
                    {
                        Result = true;
                    }
                    else
                    {
                        this.errorCode = 300;
                        this.errorMessage = "This does not appear to be a valid email address";
                    }
                    break;

                default:
                    this.errorCode = 300;
                    this.errorMessage = string.Format("Unknown property '{0}'", property);

                    break;
            }

            return Result;
        }


        /*
         * Return a user record by searching for email, registered id, nickname, or username
         * 
         */
        public SmallFileHandlerStructure GetUserRecord(string guid)
        {
            SmallFileHandlerStructure rec = null;

            if (sdh.Open(RegistrationFile))
            {
                // try by email or registered guid
                rec = sdh.FindRec((guid.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("recid")), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                // if no joy, then by usernick
                if (rec == null || rec.RecNo == 0) rec = sdh.FindRec(sdh.GetFieldIdx("nickname"), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                // again, if no luck, by username
                if (rec == null || rec.RecNo == 0) rec = sdh.FindRec(sdh.GetFieldIdx("username"), guid, SmallDataHandler.Match.ExactCaseInsensitive);
            }
            else
            {
                errorCode = 300;
                errorMessage = "Could not open registration file";
            }

            sdh.Close();

            return rec;
        }



        /*
         * Get a property from the user's record
         * 
         * If the guid has a @ then search by email
         */
        public string GetProperty(string guid, string property)
        {
            string Result = string.Empty;

            ClearAll();

            try
            {
                if (sdh.Open(RegistrationFile))
                {
                    SmallFileHandlerStructure rec =
                        sdh.FindRec((guid.Contains("@") ? sdh.GetFieldIdx("email") : sdh.GetFieldIdx("recid")), guid, SmallDataHandler.Match.ExactCaseInsensitive);

                    if (rec.RecNo > 0)
                    {
                        switch (property)
                        {
                            case "FULLNAME":
                                Result = rec.Fields[1];
                                break;

                            case "USERNAME":
                                Result = rec.Fields[2];
                                break;

                            case "NICKNAME":
                                Result = rec.Fields[3];
                                break;

                            case "PASSWORD":
                                Result = rec.Fields[4];
                                break;

                            case "EMAIL":
                                Result = rec.Fields[5];
                                break;

                            case "RIGHTS":
                                Result = rec.Fields[10];
                                break;

                            case "STATUS":
                                switch (rec.Fields[8])
                                {
                                    case "A":
                                        Result = "Active Verified";
                                        break;

                                    case "I":
                                        Result = "Inactive Verified";
                                        break;

                                    case "N":
                                        Result = "Inactive Unverified";
                                        break;

                                    case "P":
                                        Result = "Probationary";
                                        break;

                                    case "V":
                                        Result = "Pending Verification";
                                        break;

                                    default:
                                        Result = "UNKNOWN " + rec.Fields[8];
                                        break;
                                }

                                break;

                            default:
                                Result = null;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorCode = (sdh.ErrorCode > 0 ? sdh.ErrorCode : 109);
                errorMessage = ex.Message + "\r\n" + sdh.ErrorMessage;
            }

            sdh.Close();
            return Result;
        }


        /*
         * Take a list of RecIDs in format /ID/ID/ID../
         * and convert to a list of fields given in parameter type
         */
        public string IDs2Names(string ids, string type)
        {
            string Result = "/";

            ClearAll();

            try
            {

                if (sdh.Open(RegistrationFile))
                {
                    SmallFileHandlerStructure rec = sdh.BlankRec();

                    for (int i = 1; i <= sdh.RecCount; i++)
                    {
                        rec = sdh.ReadAtIndex(i);
                        if (ids.Contains("/" + rec.GetField("RecID") + "/"))
                        {
                            // Convert the ID to chosend field
                            Result += rec.GetField(type) + "/";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorCode = 300;
                errorMessage = ex.Message;
            }
            finally
            {
                sdh.Close();
            }

            return Result;
        }

        /*
         * Take a list of email, UserName, NickName fields in
         * the format of /x1/x2/x3../ and return them as a
         * list of RecIDs in a similar format
         */
        public string Names2IDs(string names)
        {
            string Result = "/";
            names = names.ToUpper();

            ClearAll();

            try
            {

                if (sdh.Open(RegistrationFile))
                {
                    SmallFileHandlerStructure rec = sdh.BlankRec();

                    for (int i = 1; i <= sdh.RecCount; i++)
                    {
                        rec = sdh.ReadAtIndex(i);
                        if (names.Contains("/" + rec.GetField("UserName").ToUpper() + "/"))
                        {
                            Result += rec.GetField("RECID") + "/";
                        }
                        else if (names.Contains("/" + rec.GetField("NickName").ToUpper() + "/"))
                        {
                            Result += rec.GetField("RECID") + "/";
                        }
                        else if (names.Contains("/" + rec.GetField("Email").ToUpper() + "/"))
                        {
                            Result += rec.GetField("RECID") + "/";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorCode = 300;
                errorMessage = ex.Message;
            }
            finally
            {
                sdh.Close();
            }

            return Result;
        }

        public int GetFieldIdx(string fn)
        {
            // return the index of the given field (-1 means not found)
            return sdh.GetFieldIdx(fn);
        }

        /*
         * Of sent a false value in realGUID, create a locally unique GUID 
         * otherwise return a globally unique GUID.
         * 
         */
        public string CreateGUID(bool realGUID)
        {
            string Result = Guid.NewGuid().ToString();
            Counter++;

            // Get a 12 character, locally unique GUID based on GMT and the internal counter
            // with fairly decent reliance that it's *locally* unique if the GUID is being 
            // created by only one machine, so not advised for multi machine envionments
            if (realGUID == false)
            {
                // let's create a 18 digit string using an easy to understand format plus the 4 least significant digits of the counter
                Result = DateTime.Now.ToUniversalTime().ToString("yyyy:MM:dd HH:mm:ss").Replace(" ", "").Replace(":", "") + (Counter % 10000L).ToString("D4");

                // break out the 18 digit string into 9 bytes
                byte[] hash = new byte[Result.Length / 2];

                for (int i = 0; i < Result.Length / 2; i++)
                {
                    byte.TryParse(Result.Substring(i * 2, 2), out hash[i]);
                }

                // Convert to base 64 to get a 12 character GUID
                Result = Convert.ToBase64String(hash);
            }

            return Result;
        }
    }
}
