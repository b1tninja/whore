using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Text;

namespace whore
{
    class Client
    {
        private const int BUFSIZE = 32;
        static int Main(string[] args)
        {
            if(args.Length != 3){
                throw new ArgumentException("Parameters: <host> <port> <commandstring>");
            } 

            TcpClient client = null;
            int portno = Int32.Parse(args[1]);
            byte[] bb = Encoding.UTF8.GetBytes(args[2]);
            byte[] rcvbuff = new byte[BUFSIZE];
            string responseStr = "";
            string loc;

            try {
                //Establish connection.
                client = new TcpClient(args[0], portno);
                NetworkStream connectionStream = client.GetStream();
                //Send command string.
                connectionStream.Write(bb, 0, bb.Length);

                //Read completion/error response.
                while(connectionStream.Read(rcvbuff, 0, rcvbuff.Length) > 0){
                    loc = Encoding.UTF8.GetString(rcvbuff);
                    responseStr += loc;
                    if(loc.Contains("\n")){
                        Console.WriteLine(responseStr);
                        responseStr = "";
                    }
                    rcvbuff = Enumerable.Repeat((byte)0, rcvbuff.Length).ToArray();
                    Thread.Sleep(25);
                }

                //Print response and exit.
                Console.WriteLine(responseStr);
                connectionStream.Close();
                client.Close();
            } catch (Exception e) {
                Console.WriteLine("TCP send failed.\n"+e.Message);
            }
            return 0;
        }
    }
}
