using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFChatServer
{
    class ClassCommandInfo
    {
        public string command;
        public string msg;
        public string msgOut = string.Empty;
        public string display = string.Empty;
        public string Results = string.Empty;
        public bool Success = false;
        public int userIdx = -1;

        public ClassUsers ThisUser;

        public ClassCommandInfo(int uidx, ClassUsers userItem)
        {
            ThisUser = userItem;
            userIdx = uidx;
            ThisUser.Reset();
        }

    }
}
