using System;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace whore {

    class ClientInterface {

        private const int BUFSIZE = 32;
        private const int MAXSIZE = 2048;

        object notlock = new object();
        TcpClient client;
        NetworkStream connStream;
        Whore creator;
        int taskcount;
        Task[] clientTasks = null;

        //<summary> Handles a client's requests, adding their tasks to the task
        //queue. Currently does not return results, need to modify callback operation
        //for that to work. </summary>
        public ClientInterface(Whore _creator){
            creator = _creator;
        }

   
        public void handleClient(object arg){
            client = (TcpClient)arg;
            byte[] rcvbuff = new byte[BUFSIZE];
            byte[] rplybuff;
            int bytesrcvd = 0;
            int totbytes = 0;
            connStream = client.GetStream();
            string commandString = "";
            string loc;
         

            rcvbuff = Enumerable.Repeat((byte)0, rcvbuff.Length).ToArray();
            //Read the command string from the client (guarded against giant command strings);
            while((bytesrcvd = connStream.Read(rcvbuff, 0, rcvbuff.Length)) > 0 && totbytes < MAXSIZE){
                loc = Encoding.UTF8.GetString(rcvbuff);
                commandString += loc;
                Console.WriteLine(loc);
                if (loc.Contains("\0")) {
                    break;
                }
                rcvbuff = Enumerable.Repeat((byte)0, rcvbuff.Length).ToArray();
                totbytes += bytesrcvd;
            }
            Console.WriteLine("Command String: '"+commandString+"'");
            //Send response.
            try {
                clientTasks = ClientInterface.parseCommandString(commandString.Replace("\0", "").Trim());
            } catch (ArgumentException ae) {
                rplybuff = Encoding.UTF8.GetBytes(ae.Message);
                Console.WriteLine(ae.Message);
                Console.WriteLine(ae.StackTrace);
                connStream.Write(rplybuff, 0, rplybuff.Length);
            }
            if (clientTasks != null){
                foreach(Task t in clientTasks){
                    t.setOwner(this);
                    creator.queue(t);
                }
                rplybuff = Encoding.UTF8.GetBytes("Tasks Queued\n");
                connStream.Write(rplybuff, 0, rplybuff.Length);
            }
            taskcount = clientTasks.Length; 
        }
        

        //<summary> Send response to client. (Primitive Echo at the moment) </summary>
        //<param name="result"> The result of the client's queued task. </param>
        public void notify(object result){
            lock(this.notlock){
//                Console.WriteLine(result.ToString());
                byte[] rplybuff = Encoding.UTF8.GetBytes(result.ToString()+"\n");
                taskcount--; 
                connStream.Write(rplybuff, 0, rplybuff.Length);
                if(taskcount <= 0){
                    connStream.Close();
                    client.Close();
                }
            }
        }


        //<summary> Read a command string and transform it into a series of
        //Tasks to be run by the TorInstances. </summary>
        //<param name="commandString"> The string which indicates which tasks to
        //execute on what host, passed from the client. </param>
        public static Task[] parseCommandString(string commandString){
            Task[] retTasks;
            string taskList = "";
            String targetHost = "";
            string[] taskStrs;
            int i;

            for(i = 0; i < commandString.Length && commandString[i] != ':'; i++){
                taskList += commandString[i];
            }

            targetHost = (commandString.Substring(i+1, commandString.Length-(i+1))).Trim(); 

            if (Uri.CheckHostName(targetHost) == UriHostNameType.Unknown){
                throw new ArgumentException("Invalid target hostname: '"+targetHost+"' in "+commandString);
            }

            taskStrs = taskList.Split(',');
            if (taskStrs.Length < 1){
                throw new ArgumentException("Invalid task list: '"+taskList+"' -- no tasks parsed in"+commandString);
            }
            else {
                retTasks = new Task[taskStrs.Length];
            }

            for(int l = 0; l < taskStrs.Length; l++){
                switch(taskStrs[l].ToLower()){
                    case "dns":
                        retTasks[l] = new DnsTask(new DnsTransaction.QuestionRecord(targetHost, DnsTransaction.QTYPE.A, DnsTransaction.RCLASS.IN));
                        break;
                    case "whois":
                        retTasks[l] = new WhoisTask(targetHost);
                        break;
                    default:
                        throw new ArgumentException("Unknown task type: '"+taskStrs[l].ToLower()+"' in "+commandString);
                } 
            }   
            return retTasks;
        }
    }
}
