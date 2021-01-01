/*
 * This is the User class that tracks the user's presence while in the system.  I added the
 * a change notification system that I found on CodeProject in order to be able to know when
 * one of the properties change.  Extensive mods to the notification code was made to
 * tailor it for my needs.
 * 
 * When a property changes, it's name is put into the Changes string in the format /<propName>/
 * once to indicate a changed state.
 * 
 * Since I don't anticipate a need to rollback the changes, I have no need for tracking the old
 * value.  The old notification code allowed that to happen (it's pretty cool on how it does it).
 * 
 * Propery List
 * ------------------------------------
 *
 *  Tracked
 *  --------------------------
 *  string   ThreadGUID                 ID assigned during thread creation
 *  string   FullName (fullName)        Users full name
 *  string   UserName (userName)        User name (all caps)
 *  string   NickName (nickName)        Users nickname
 *  string   Email (email)              Users email
 *  string   Status (status)            Registration Status 
 *  
 *  string   RegisteredGUID             ID assigned to user during registration
 *           (registeredGUID)
 *  string   UserRights (userRights)    System rights assigned to user 
 *  string   AwayMsg (awayMsg)          Away msg left by user
 *  string   IgnoreList (ignoreList)    List of thread IDs the user is ignoring
 *  string   CurrentRoom (currentRoom)  Room the user is currently located (default is LOBBY)
 *  int      Seating (seating)          Users room security level (0=public, 1=private, 3=speaker)
 *                                      Determines what a user can do in a room
 *  
 *  Untracked
 *  --------------------------
 *  bool     WasChanged                 Flag indicating if user info was changed so that the
 *                                      changes can filter back to the Users list
 *
 *                                      
 *  bool     IsAdmin                    Lets the system know if user is a systems admin
 *  string   ipAddress                  Remote IP of user's computer
 *  string   computerName               Name of computer user is currently on (sent by HELLO msg)
 *  int      msgCount                   Number of msgs sent by user during this session
 *  
 *  DateTime createTime                 Time the user was created
 *  DateTime lastMsgTime                Time of last msg, used to track user timeouts
 *  DateTime lastOn                     Last time user logged onto the system
 *  
 *  SmallFileHandlerStructure UserRec   Used to get/set the record in the registration data file
 *  
 */
using System;
using System.ComponentModel;

namespace WPFChatServer
{
    class ClassUsers : NotifyPropertyChangeObject
    {
        //public TcpClient tcpClient;
        public bool WasChanged { get => Changes.Length > 1; }
        public string ThreadGUID { get; set; }

        // Full name of user
        private string fullName;
        [DefaultValue("")]
        public string FullName { get { return fullName; } set { ApplyPropertyChange<ClassUsers, string>(ref fullName, o => o.FullName, value); } }

        // user name
        private string userName;
        [DefaultValue("")]
        public string UserName { get { return userName; } set { ApplyPropertyChange<ClassUsers, string>(ref userName, o => o.UserName, value); } }

        // nick name
        private string nickName;
        [DefaultValue("")]
        public string NickName { get { return nickName; } set { ApplyPropertyChange<ClassUsers, string>(ref nickName, o => o.NickName, value); } }

        // email address
        private string email;
        [DefaultValue("")]
        public string Email { get { return email; } set { ApplyPropertyChange<ClassUsers, string>(ref email, o => o.Email, value); } }

        // registration status
        private string status;
        [DefaultValue("")]
        public string Status { get { return status; } set { ApplyPropertyChange<ClassUsers, string>(ref status, o => o.Status, value); } }

        // ID given for registration
        private string registeredGUID;
        [DefaultValue("")]
        public string RegisteredGUID { get { return registeredGUID; } set { ApplyPropertyChange<ClassUsers, string>(ref registeredGUID, o => o.RegisteredGUID, value); } }

        // user rights (most definitions defined by controlling code)
        private string userRights;
        [DefaultValue("")]
        public string UserRights { get { return userRights; } set { ApplyPropertyChange<ClassUsers, string>(ref userRights, o => o.UserRights, value); } }

        // Away message
        private string awayMsg;
        [DefaultValue("")]
        public string AwayMsg { get { return awayMsg; } set { ApplyPropertyChange<ClassUsers, string>(ref awayMsg, o => o.AwayMsg, value); } }

        // list of user GUIDs to ignore
        private string ignoreList;
        [DefaultValue("")]
        public string IgnoreList { get { return ignoreList; } set { ApplyPropertyChange<ClassUsers, string>(ref ignoreList, o => o.IgnoreList, value); } }

        // current room user is in
        private string currentRoom;
        [DefaultValue("LOBBY")]
        public string CurrentRoom { get { return currentRoom; } set { ApplyPropertyChange<ClassUsers, string>(ref currentRoom, o => o.CurrentRoom, value); } }

        private int seating;
        [DefaultValue(0)]
        public int Seating
        {
            get => seating;
            set
            {
                if (value >= 0 && value <= 3)
                {
                    ApplyPropertyChange<ClassUsers, int>(ref seating, o => o.Seating, value);
                }
                else
                    throw new ArgumentOutOfRangeException("Value must be between 0 and 3", "Seating");
            }
        }


        public bool IsAdmin { get => userRights.Contains("/ADMIN/"); }

        public string ipAddress = string.Empty;
        public string computerName = string.Empty;
        public int msgCount = 0;
        public DateTime registeredDT = DateTime.Now;
        public DateTime lastMsgDT = DateTime.Now;
        public DateTime lastOnDT = DateTime.MinValue;

        public SmallFileHandlerStructure userRec;

        public ClassUsers(string guid, string uname, string nname, string ip, string cname)
        {
            ThreadGUID = guid;
            UserName = uname;
            NickName = nname;
            FullName = string.Empty;
            ipAddress = ip;
            computerName = cname;
            Status = "N";
            seating = 0;
            UserRights = string.Empty;
            IgnoreList = string.Empty;
            Email = string.Empty;
            RegisteredGUID = string.Empty;
            CurrentRoom = "LOBBY";
        }

        public bool Dif(string field)
        {
            return Changes.Contains("/" + field + "/");
        }

        

    }
}
