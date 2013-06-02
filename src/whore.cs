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
    class Whore
    {
        DB db;

        public Queue<Task> taskList = new Queue<Task>();
        public List<TorInstance> torInstances = new List<TorInstance>();

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
                torInstances.Add(new TorInstance(torloc, controlPort, socksPort, true));
            }
        }

        TorInstance getAvailableTorInstance()
        {
            while (true)
            {
                foreach (TorInstance torInstance in torInstances)
                {
                    switch (torInstance.State)
                    {
                        case TorInstance.TorState.Ready:
//                            torInstance.StartTorThread();
                            return torInstance;
                        case TorInstance.TorState.Terminated:
                            torInstance.StartTorThread();
                            Debug.WriteLine("Restarting terminated TorInstance({0})", torInstance.GetHashCode());
                            goto default;
                        default:
                        case TorInstance.TorState.Busy:
                        case TorInstance.TorState.Bootstrapping:
                        case TorInstance.TorState.Error:
                            continue;
                    }
                }
                Thread.Sleep(25);
            }

        }

        public void doWork()
        {
            if (taskList.Count > 0)
            {
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
                System.Console.WriteLine("No work to be done.");
                Thread.Sleep(50);
            }

        }
        public void displayResults(object buffer)
        {
            System.Console.WriteLine(buffer);
        }
        public void queue(Task task)
        {
            taskList.Enqueue(task);
        }
    }


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
            whore.queue(new WhoisTask("example.com"));
            whore.queue(new DnsTask(new DnsTransaction.QuestionRecord("example.com", DnsTransaction.QTYPE.A, DnsTransaction.RCLASS.IN)));
            #endregion

            // Main loop
            while (true)
            {
                whore.doWork();
            }
        }
    }
}
