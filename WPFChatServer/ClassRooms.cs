using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFChatServer
{
    class ClassRooms
    {
        public string name = string.Empty;
        public string topic = string.Empty;
        public string password = string.Empty;
        public string admin = string.Empty;
        public string moderators = "/";
        public string speakers = "/";
        public string banned = "/";
        public bool record = false;

        public int privacySetting = 0;
        public int publicSeats = 20;
        public int speakerSeats = 0;
        public int privateSeats = 0;

        public ClassRooms(string usrGUID, string rmName, string rmTopic)
        {
            name = rmName;
            admin = usrGUID;
            topic = rmTopic;
        }

    }
}
