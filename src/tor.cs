using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;

using System.Net;
using System.Net.Sockets;

using Starksoft.Net.Proxy;


namespace whore
{
    abstract class TorTask : Task
    {
        public const int SLEEP_TIME = 25;

        public TorInstance tor;
        public DnsEndPoint endPoint;


        public TorTask()
        {
        }

        public virtual void execute(TorInstance _tor, TaskResultHandler callback)
        {
            base.execute(callback);
            tor = _tor;
            while(tor == null)
                Thread.Sleep(SLEEP_TIME);

	    tor.State = TorInstance.TorState.Busy;
            while (tor.State != TorInstance.TorState.Busy)
                Thread.Sleep(SLEEP_TIME);

        }

        public virtual void onConnect(object sender, CreateConnectionAsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                System.Console.WriteLine("Task({0}): Connection to {1} failed via TorInstance({2}).", this.GetHashCode(), this.endPoint, tor.GetHashCode());
		System.Console.WriteLine(e.Error);
		System.Console.Write(e.Error.StackTrace);
            }
            else
            {
                System.Console.WriteLine("Task({0}): Connection to {1} established via TorInstance({2}).", this.GetHashCode(), this.endPoint, tor.GetHashCode());
            }
        }
    }

    class TorInstance
    {
        class TorException : SystemException
        {

        }
	//Needs implementation? 
        class TorInvalidStateChange : TorException {}

        public enum TorState { Bootstrapping, Ready, Busy, Error, Terminated };

        private UInt16 controlPort;
        private UInt16 socksPort;
        private string dataDirectory;
        private string controlPassword;
	private string torloc;
        private Thread torThread;
        ProxyClientFactory factory;

        public IProxyClient proxy;
        private TorState state;

        public void cleanup()
        {
            this.proxy.TcpClient.Close();
            try
            {
                while (this.proxy.TcpClient.Available == 0)
                    Thread.Sleep(25);
            }
            catch (ObjectDisposedException)
            {
                Debug.Print("TcpClient instance disposed.");
            }
            proxy = factory.CreateProxyClient(ProxyType.Socks5, "127.0.0.1", socksPort);
            this.State = TorState.Ready;
        }


        public TorState State
        {
            get
            {
                return state;
            }
            set
            {
                // Allow a ready thread to become busy
                // And a busy thread to become ready
                if ((value == TorState.Busy && state == TorState.Ready) || (value == TorState.Ready && state == TorState.Busy))
                {
                    state = value;
                }
                else
                {
                    System.Console.WriteLine(string.Format("TorInstance({0}) is in an unexpected state: ({1}->{2}).",this.GetHashCode(),state.ToString(),value.ToString()));
                   // throw new TorInvalidStateChange(); // Shouldn't happen, but lets catch the race condition if possible.
                }
            }
        }
        public UInt16 ControlPort { get { return controlPort; } }
        public UInt16 SocksPort { get { return socksPort; } }
        public string DataDirectory { get { return dataDirectory; } }

        public TorInstance(string _torloc, int _controlPort, int _socksPort, bool useExistingTorPorts = false)
        {
            // Assign member variables
            //            state = TorState.Bootstrapping;
	    torloc = _torloc;
            controlPort = (ushort)_controlPort;
            socksPort = (ushort)_socksPort;
            dataDirectory = string.Format("./data/{0}", this.GetHashCode());


            System.Console.WriteLine("TorInstance({0:d}) created. Socks: {1:d}", this.GetHashCode(), socksPort);

            factory = new ProxyClientFactory();
            proxy = factory.CreateProxyClient(ProxyType.Socks5, "127.0.0.1", socksPort);

            if (!useExistingTorPorts)
            {
                StartTorThread();
            }
            else
            {
                state = TorState.Ready;
            }
        }

        public void StartTorThread()
        {
            torThread = new Thread(new ThreadStart(startTorProcess));
            torThread.IsBackground = true;
            torThread.Start();

            while (!torThread.IsAlive)
                Thread.Sleep(25);

        }

        private void parseSTDOUT(object sender, DataReceivedEventArgs e)
        {
            string messageType;
            string message;
            // Parse the stdout, to determine the state.
            if (sender != null && e != null && e.Data != null && e.Data != "")
            {
                messageType = e.Data.Substring(21, e.Data.IndexOf(']', 21) - 21);
                message = e.Data.Substring(23 + messageType.Length);

                switch (messageType)
                {
                    case "warn":
                        break;
                    case "notice":
                        switch (message)
                        {
                            case "Bootstrapped 100%: Done.":
                                state = TorState.Ready;
                                break;
                        }
                        break;
                    case "err":
                        state = TorState.Error;
                        break;
                }
                System.Console.WriteLine(string.Format("{0}: {1}", this.GetHashCode().ToString(), e.Data));
            }
        }

        private void startTorProcess()
        {
            // Setup Tor process
            StringBuilder outputBuffer = new StringBuilder(); ;
            ProcessStartInfo processStartInfo;
            Process process;
            processStartInfo = new ProcessStartInfo();
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.UseShellExecute = false;
	    System.Console.WriteLine(string.Format("cp {0}, sp {1}, dd {2}", controlPort, socksPort, dataDirectory));
            processStartInfo.Arguments = string.Format("--controlPort {0} --socksPort {1} --dataDirectory {2}", controlPort, socksPort, dataDirectory);
            //processStartInfo.Arguments = string.Format("--Socks5Proxy 127.0.0.1:1337 --controlPort {0} --socksPort {1} --dataDirectory {2}", controlPort, socksPort, dataDirectory);
            processStartInfo.FileName = torloc;

            process = new Process();

            process.StartInfo = processStartInfo;
            // enable raising events because Process does not raise events by default
            process.EnableRaisingEvents = true;
            // attach the event handler for OutputDataReceived before starting the process
            process.OutputDataReceived += new DataReceivedEventHandler(parseSTDOUT);
            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit(); // block thread, await potential process termination.
            process.CancelOutputRead();

            System.Console.WriteLine(this.ToString() + " process terminated!");
            state = TorState.Terminated;
        }
    }
}
