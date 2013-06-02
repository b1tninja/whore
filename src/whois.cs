using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

using Starksoft.Net.Proxy;

namespace whore
{
    class WhoisTask : TorTask
    {
        public readonly string Host;
        const int chunk_size = 65535;
        byte[] chunk = new byte[chunk_size];
        private StringBuilder buffer = new StringBuilder();

        private string getTLD()
        {
            return Host.Substring(Host.LastIndexOf(".") + 1);
        }
        public WhoisTask(string host)
        {
            Host = host;
        }
        public WhoisTask(IPAddress ip)
        {
            StringBuilder host = new StringBuilder();
            switch (ip.AddressFamily)
            {
                // Turn the address into its in-addr.arpa form.
                case System.Net.Sockets.AddressFamily.InterNetwork:
                    foreach (byte octet in ip.GetAddressBytes().Reverse())
                    {
                        host.Append(octet.ToString() + ".");
                    }
                    host.Append("in-addr.arpa");
                    break;
                // Turn the address into its ip6.arpa form.
                case System.Net.Sockets.AddressFamily.InterNetworkV6:
                    foreach (byte octet in ip.GetAddressBytes().Reverse())
                    {
                        string hex = string.Format("{0:x2}", octet);
                        host.Append(string.Format("{0}.{1}.", hex[1], hex[0]));
                    }
                    host.Append("ip6.arpa");
                    break;

            }
            Host = host.ToString();
        }
        protected void onRecieve(IAsyncResult result)
        {
            int nBytesRec = tor.proxy.TcpClient.Client.EndReceive(result);
            // If no bytes were received, the connection should be closing down.
            if (nBytesRec <= 0)
            {
                tor.proxy.TcpClient.Close();
                tor.State = TorInstance.TorState.Ready;
                if (callback != null)
                    callback(buffer.ToString());
            }
            else
            {
                buffer.Append(Encoding.ASCII.GetString(chunk));
                tor.proxy.TcpClient.Client.BeginReceive(chunk, 0, chunk.Length, SocketFlags.None, new AsyncCallback(onRecieve), this);
            }
        }
        public override void onConnect(object sender, CreateConnectionAsyncCompletedEventArgs e)
        {
            base.onConnect(sender, e);
            if (e.Error == null)
            {
                TcpClient tcp = e.ProxyConnection;
                tcp.Client.Send(ASCIIEncoding.ASCII.GetBytes(Host + "\r\n"));
                tcp.Client.BeginReceive(chunk, 0, chunk.Length, SocketFlags.None, new AsyncCallback(this.onRecieve), this);
            }
            else
            {
                Console.WriteLine("Socket error has occured. " + e.Error);
            }
        }
        public void dnsCallback(object result)
        {
            DnsTransaction.ResourceRecord rr = result as DnsTransaction.ResourceRecord;
            IPAddress addr = new IPAddress(rr.data);
            try
            {
                tor.proxy.CreateConnectionAsyncCompleted += new EventHandler<CreateConnectionAsyncCompletedEventArgs>(onConnect);
                tor.proxy.CreateConnectionAsync(addr.ToString(), 43);
            }
            catch (SocketException e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public override void execute(TorInstance torInstance, TaskResultHandler callback)
        {
            base.execute(torInstance, callback);
            // doesn't seem like the appropriate level to be spawning tasks... for instance, how to interface db?
            // spwans a DnsTask to resolve the CNAME that tells us where to connect to perform the whois. 
            new DnsTask(new DnsTransaction.QuestionRecord(string.Format("{0}.whois-servers.net", getTLD()), DnsTransaction.QTYPE.CNAME, DnsTransaction.RCLASS.IN)).execute(torInstance, callback);
        }
    }
}
