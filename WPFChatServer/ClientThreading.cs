/*
 * Start this thread for each client connection
 * which will listen for client input, process
 * commands, and send out messages a required
 * 
 */
using System;
using System.Net.Sockets;
using System.Threading;

namespace WPFChatServer
{
    class ClientThreading
    {
        public bool destroyMe = false;
        public string ThreadGUID;
        public string nickName = "N/A";

        private TcpClient clientSocket;
        private MainWindow mw;
        private ChatServer cs;
        Thread ctThread;

        private string passedMsg;

        // set everything up and start the Chat Server thread
        public void StartClientThread(MainWindow m, ChatServer c, TcpClient inClientSocket, int idx, string msg)
        {
            cs = c;
            mw = m;
            passedMsg = msg;
            clientSocket = inClientSocket;
            ThreadGUID = cs.usersList[idx].ThreadGUID;  // never changes while active, so can keep local

            ctThread = new Thread(ClientThread);
            ctThread.Start();
        }


        /*
         * Loop that listens and reacts to all input for this connection
         */
        private void ClientThread()
        {
            int requestCount = 0;
            byte[] bytesFrom;
            string dataFromClient;
            string rCount;

            // Why isn't Connected working???
            while (clientSocket != null && clientSocket.Connected)
            {
                try
                {
                    requestCount++;
                    NetworkStream networkStream = clientSocket.GetStream();

                    // clear the buffer and wait for input
                    try
                    {
                        if (passedMsg.Length > 0)
                        {
                            dataFromClient = passedMsg;
                            passedMsg = "";
                        }
                        else
                        {
                            bytesFrom = new byte[1024];
                            networkStream.Read(bytesFrom, 0, bytesFrom.Length);

                            dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                            dataFromClient = dataFromClient.Replace("\0", string.Empty).Trim();
                        }

                        rCount = Convert.ToString(requestCount);
                    }
                    catch (Exception ex)
                    {
                        if (this.clientSocket.Connected)
                        {
                            // read error
                            mw.Display("DOCHAT - Read error: " + ex.Message, 1);
                        }
                        else
                        {
                            // Client has disconnected
                            mw.Display("DOCHAT - Client disconnected ->" + ThreadGUID, 7);
                        }

                        dataFromClient = string.Empty;
                    }

                    // It's not possible to receive a zero length string
                    // so if we find one then we'll assume a disconnect
                    if (dataFromClient.Trim().Length == 0)
                    {
                        mw.Display(">>> Null data from client " + ThreadGUID + "\r\n>>>Closing Connection\r\n");
                        break;
                    }

                    dataFromClient = cs.MsgProc(dataFromClient.Trim(), ThreadGUID);

                    // If there's a message, send the info out to all connections
                    if (dataFromClient.Length > 0)
                        cs.Broadcast(dataFromClient, ThreadGUID);
                }
                catch (Exception ex)
                {
                    mw.Display(string.Format("DOCHAT - ID {0} - Error: {1}", ThreadGUID, ex.ToString()), 1);
                }
            }//end while

            mw.Display(string.Format("DOCHAT - Lost Connection {0}", ThreadGUID), 7);
            destroyMe = true;
        }//end doChat
    }
}
