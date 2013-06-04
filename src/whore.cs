//TODO: Save/cache the results, filtering unnesc work. (And implement root-hints).
//TODO: Tasks should be able to Async create other tasks ("chains of tasks" too?)
//TODO: Error handling. 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using Starksoft.Net.Proxy;
using System.IO;

namespace whore
{

    // <summary>
    // 	The main instance class. Maintains a list of Tasks which need to be
    // 	performed, and a list of Tor instances which can be used to perform
    // 	them.
    // </summary> 
    class Whore
    {
        DB db;

        public Queue<Task> taskList = new Queue<Task>();
        public List<TorInstance> torInstances = new List<TorInstance>();

	// <summary>
	// Constructor. Initialises data directory if necessary and creates the
	// required number of TorInstance objects. </summary>
	// <param name="_db">The database object to cache results with</param>
	// <param name="torloc">The location of the Tor executable to be used by
	// the TorInstances </param>
	// <param name="basePort">The port number to be used as the basis of Tor
	// communications. basePort +-<paramref>threads</paramref> should all be free.
	// <param name="threads"> The number of TorInstance objects to use. 
        public Whore(DB _db, string torloc, int basePort = 9049, byte threads = 1)
        {
            db = _db;
            // Create data subdirectory if it doesn't already exist. 
            if (!System.IO.Directory.Exists("./data"))
            {
                try
                {
                    System.IO.Directory.CreateDirectory("./data");
                }
                catch
                {
                    Console.WriteLine("Failed to create data directory.");
                }
            }

            // Create TorInstances
            for (int i = 1; i <= threads; i++)
            {
                int controlPort = basePort - i;
                int socksPort = basePort + i;
                // useExisting?
                torInstances.Add(new TorInstance(torloc, controlPort, socksPort));
            }
        }

	//<summary> Return an available TorInstance from the queue.</summary>
	//<return> A TorInstance which is available for work. </return>
        TorInstance getAvailableTorInstance()
        {
            while (true)
            {
                foreach (TorInstance torInstance in torInstances)
                {
//                    System.Console.WriteLine(string.Format("State {0}", torInstance.State));
                    switch (torInstance.State)
                    {
                        case TorInstance.TorState.Ready:
                            return torInstance;
                        case TorInstance.TorState.Terminated:
                            torInstance.StartTorThread();
                            System.Console.WriteLine("Restarting terminated TorInstance({0})", torInstance.GetHashCode());
                            goto default;
                        default:
                        case TorInstance.TorState.Busy:
                        case TorInstance.TorState.Bootstrapping:
                        case TorInstance.TorState.Error:
                            continue;
                    }
                }
//		        System.Console.WriteLine("Waiting on available TorInstance");
                Thread.Sleep(25);
            }

        }

	//<summary> Takes a task from the queue and has a TorInstance handle it.
	//</summary>
        public void doWork()
        {
            if (taskList.Count > 0)
            {
                System.Console.WriteLine("Going to dequeue a task.");
                Task task = taskList.Dequeue();
                if (task is TorTask)
                {
                    TorTask torTask = (TorTask)task;
                    TorInstance torInstance = getAvailableTorInstance();
                    Task.TaskResultHandler a, b, c;
                    a = displayResults;
                    b = db.cache;
                    c = a + b;

                    torTask.execute(torInstance,c);
                }
            }
            else
            {
//                System.Console.WriteLine("No work to be done.");
                Thread.Sleep(50);
            }

        }

	//<summary> Display the result of a Task execution. </summary>
	//<param name="buffer"> The result object to be displayed. </param>
        public void displayResults(object buffer)
        {
            System.Console.WriteLine(buffer);
        }

	//<summary> Add a task to the work queue. </summary>
	//<param name="task"> The Task object to enqueue. </summary>
        public void queue(Task task)
        {
		
            taskList.Enqueue(task);
        }

    }//End of Class


    //<summary> The driver class. Creates a Whore instance, queues a few tasks
    //and then has it work through them. </summary>
    class MainLoop
    {
        static int Main(string[] args)
        {
	    if(args.Length < 2){
		System.Console.WriteLine("You should provide:\n\t1) The database connection string\n\t2) The location of your Tor executable");
		System.Environment.Exit(1);
	    }
	    string dbstring = args[0];
	    string torloc = args[1];
	    System.Console.WriteLine(string.Format("DBSTRING: {0} | TORLOC: {1}", dbstring, torloc));
            DB db = new DB(dbstring);

            Whore whore = new Whore(db, torloc);
            //  Dns.readRootHints();
            
            #region StaticTasks
            // Assign some tasks
//            whore.queue(new WhoisTask("example.com"));
            whore.queue(new DnsTask(new DnsTransaction.QuestionRecord("example.com", DnsTransaction.QTYPE.A, DnsTransaction.RCLASS.IN)));
            whore.queue(new DnsTask(new DnsTransaction.QuestionRecord("gungo.com", DnsTransaction.QTYPE.A, DnsTransaction.RCLASS.IN)));
            #endregion

            // Main loop
            while (true)
            {
                whore.doWork();
            }
        }
    }//End of Class

}
