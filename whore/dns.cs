//TODO cleanup getBytes streams returning streams of streams

using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

using Starksoft.Net.Proxy;

namespace whore
{
    partial class DnsTask : TorTask
    {
        DnsTransaction resolver;
        const int buffer_size = 1024;
        byte[] chunk = new byte[buffer_size];
        MemoryStream buffer = new MemoryStream();

        public DnsTask(DnsTransaction.QuestionRecord question)
        {
            this.resolver = new DnsTransaction(1, new DnsTransaction.DnsFlags(0x0100), question);
        }
        public override void onConnect(object sender, CreateConnectionAsyncCompletedEventArgs e)
        {
            base.onConnect(sender, e);
            if (e.Error == null)
            {
                TcpClient tcpClient = e.ProxyConnection;

                tcpClient.Client.Send(resolver.getBytes());
                try
                {
                    tcpClient.Client.BeginReceive(chunk, 0, chunk.Length, SocketFlags.None, new AsyncCallback(this.onRecieve), this);
                }
                catch(Starksoft.Net.Proxy.ProxyException suberr)
                {
                    Console.WriteLine(suberr.ToString());
                }
            }

            else
            {
                Console.WriteLine("Socket error has occured. " + e.ToString());
                this.tor.State = TorInstance.TorState.Error;
            }
        }
        protected void onRecieve(IAsyncResult result)
        {
            int nBytesRec = tor.proxy.TcpClient.Client.EndReceive(result);
            if (nBytesRec <= 0)
            {
                // tcp connection closing
            }
            else
            {
                buffer.Write(chunk, 0, nBytesRec);
                foreach (DnsTransaction answer in DnsTransaction.parseBuffer(buffer)) // cache all resourcerecords (answers/authoratative/additional)
                    callback(answer);

                this.tor.proxy.TcpClient.Client.BeginReceive(chunk, 0, chunk.Length, SocketFlags.None, new AsyncCallback(onRecieve), this);
            }
        }

        public override void execute(TorInstance torInstance, TaskResultHandler callback) 
        {
            base.execute(torInstance,callback);

            // TODO: Look up proper nameserver to use, from cache (or by resolving NS)

            endPoint = new DnsEndPoint("8.8.8.8", 53);
            torInstance.proxy.CreateConnectionAsyncCompleted += new EventHandler<CreateConnectionAsyncCompletedEventArgs>(onConnect);
            torInstance.proxy.CreateConnectionAsync(endPoint.Host, endPoint.Port);
        }
    }

    public class DnsTransaction
    {
        public UInt16 txnId;
        public DnsFlags flags;

        public List<QuestionRecord> queries;
        public List<ResourceRecord> answerRecords;
        public List<ResourceRecord> authorityRecords;
        public List<ResourceRecord> additionalRecords;

        public class MalformedRecord : System.ApplicationException { };

        public enum QTYPE
        {
            A = 1,
            NS,
            MD,
            MF,
            CNAME,
            SOA,
            MB,
            MG,
            MR,
            NULL,
            WKS,
            PTR,
            HINFO,
            MINFO,
            MX,
            TXT,
            RP,
            AFSDB,
            SIG = 24,
            KEY,
            AAAA = 28,
            LOC = 29,
            SRV = 33,
            NAPTR = 35,
            KX,
            CERT,
            DNAME = 39,
            APL = 42,
            DS,
            SSHFP,
            IPSECKEY,
            RRSIG,
            NSEC,
            DNSKEY,
            DHCID,
            NSEC3,
            NSEC3PARAM,
            TLSA,
            HIP = 55,
            SPF = 99,
            TKEY = 249,
            TSIG,
            AXFR = 252,
            MAILB,
            MAILA,
            ANY,
            CAA = 257,
            TA = 32768,
            DLV
        }

        public enum RCLASS
        {
            IN = 1,
            CS,
            CH,
            HS
        }

        public static IEnumerable<DnsTransaction> parseBuffer(MemoryStream buffer)
        {
            buffer.Seek(0, SeekOrigin.Begin);
            BinaryReader br = new BinaryReader(buffer);
            if (buffer.Length > 2) // if its long enough to read the length
            {
                ushort l = GetLEUInt16(br.ReadBytes(2));
                if (buffer.Length >= l + 2)
                {
                    yield return new DnsTransaction(br.ReadBytes(l));
                    // read more querries?
                }
            }
            buffer.Seek(0, SeekOrigin.End);

        }
        public IEnumerable<ResourceRecord> getRecords()
        {
            foreach (ResourceRecord rr in answerRecords)
                yield return rr;
            foreach (ResourceRecord rr in authorityRecords)
                yield return rr;
            foreach (ResourceRecord rr in additionalRecords)
                yield return rr;
        }
        private bool bitSet(UInt16 val, byte n)
        {
            return ((val & (2 << n)) != 0);
        }

        public class DnsFlags
        {
            public ushort val;
            bool response;
            byte opcode; //4 bits
            bool truncated;
            bool recursionDesired;
            bool authoritative;
            bool recursionAvailable;
            bool answerAuthenticated;
            bool acceptUnauthenticatedData;
            byte replyCode;//4 bits

            public DnsFlags(UInt16 flags)
            {
                val = flags;
                response = BitSet(15);
                opcode = (byte)((flags >> 11) & 15);
                authoritative = BitSet(10);
                truncated = BitSet(9);
                recursionDesired = BitSet(8);
                recursionAvailable = BitSet(7);
                //z reserved
                answerAuthenticated = BitSet(5);
                acceptUnauthenticatedData = BitSet(4);
                replyCode = (byte)(flags & 15);
            }

            private bool BitSet(byte p)
            {
                return (val & (1<<p)) != 0;
            }
        }
        
        private static UInt16 GetLEUInt16(byte[] bytes, ref ushort offset)
        {
            UInt16 ret = (UInt16)(bytes[offset] << 8 | bytes[offset + 1]);
            offset += 2;
            return ret;
        }
        private static UInt16 GetLEUInt16(byte[] bytes, ushort offset = 0)
        {
            return (UInt16)(bytes[offset] << 8 | bytes[offset + 1]);
        }

        private static UInt32 GetLEUInt32(byte[] bytes, ref ushort offset)
        {
            UInt32 ret = (UInt32)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
            offset += 4;
            return ret;
        }

        private static string readLabel(byte[] bytes, ref ushort offset)
        {
            // should calculate _total_ length, not just the length of an individual segment, or may end up recursing forever
            byte length;
            StringBuilder label = new StringBuilder();

            // Read labels into canonical form
            while ((length = bytes[offset++]) != 0)
            {
                if (label.Length + length > 256)
                    throw new MalformedRecord();
                if (length < 64)
                {
                    // ASCII labels
                    label.Append(Encoding.ASCII.GetString(bytes, offset, length));
                    offset += length;
                    if (bytes[offset] != 0)
                        label.Append('.');
                }
                else if (length == 0xC0)
                {
                    // "compressed" labels (pointers)
                    ushort tmp = bytes[offset];
                    if (bytes[bytes[offset]] >= 64) // and is a valid-text label that doesn't recurse to itself? and is below 256 byte length...
                        throw new MalformedRecord();
                    label.Append(readLabel(bytes, ref tmp));
                    offset++;
                    break; // Rest of label would be part of the recursive call
                }
            }
            return label.ToString();
        }

        public static byte[] getLabelBytes(string label)
        {
            MemoryStream stream = new MemoryStream();
            foreach (string sublabel in label.Split('.'))
            {
                stream.WriteByte((byte) sublabel.Length);
                stream.Write(ASCIIEncoding.ASCII.GetBytes(sublabel), 0, sublabel.Length);
            }
            
            stream.WriteByte((byte)0); // null label
            return stream.ToArray();
        }

        public override string ToString()
        {
            StringBuilder output = new StringBuilder();
            output.Append(string.Format("DnsTransaction<{0},{1},{2},{3}>: ", queries.Count, answerRecords.Count, authorityRecords.Count, additionalRecords.Count));
            foreach (QuestionRecord qr in queries)
                output.Append(qr.ToString());
            foreach (ResourceRecord rr in getRecords())
                output.Append(rr.ToString());

            return output.ToString();
        }
        
        public class QuestionRecord
        {
            public string name;
            public QTYPE _type;
            public RCLASS _class;

            public QuestionRecord(byte[] bytes, ref ushort i)
            {
                this.name = readLabel(bytes, ref i);
                this._type = (QTYPE)GetLEUInt16(bytes, ref i);
                this._class = (RCLASS)GetLEUInt16(bytes, ref i);
            }
            public QuestionRecord(string n, QTYPE t, RCLASS c)
            {
                if (n.Length > 256)
                    throw new MalformedRecord();
                this.name = n;
                this._type = t;
                this._class = c;
            }
            public byte[] getBytes()
            {
                MemoryStream stream = new MemoryStream();
                
                byte[] buf = getLabelBytes(name);
                stream.Write(buf, 0, buf.Length);
                stream.Write(getLEBytes((ushort)_type), 0, 2);
                stream.Write(getLEBytes((ushort)_class), 0, 2);
                return stream.ToArray();
            }
            public override string ToString()
            {
                return string.Format("QuestionRecord<{0},{1},{2}>", name, _type, _class);
            }
        }
        public class ResourceRecord
        {
            public QuestionRecord question;
            public UInt32 ttl;
            public byte[] data;

            public ResourceRecord(byte[] bytes, ref ushort offset)
            {
                question = new QuestionRecord(bytes, ref offset);
                this.ttl = GetLEUInt32(bytes, ref offset);
                UInt16 length;
                length = GetLEUInt16(bytes, ref offset);
                this.data = new byte[length];
                Array.Copy(bytes, offset, data, 0, length);
                offset += length;
            }
            public ResourceRecord(QuestionRecord question, UInt32 _ttl, byte[] _data)
            {
                this.question = question;
                this.ttl = _ttl;
                this.data = _data;
            }
            public ResourceRecord(string n, QTYPE t, RCLASS c, uint _ttl, byte[] _data)
            {
                this.question = new QuestionRecord(n, t, c);
                this.ttl = _ttl;
                this.data = _data;
            }
            public byte[] getBytes()
            {
                MemoryStream buffer = new MemoryStream(question.getBytes());
                buffer.Write(getLEBytes(this.ttl),0,4);
                buffer.Write(data, 0, data.Length);
                return buffer.ToArray();
            }
            public override string ToString()
            {
                string output;
                switch (question._type)
                {
                    case QTYPE.A:
                    case QTYPE.AAAA:
                        output = new IPAddress(data).ToString();
                        break;
                    default:
                        output = "blob";
                        break;
                }
                return string.Format("ResourceRecord<{0},{1},{2}>", question.ToString(), ttl, output);
            }
        }
        public DnsTransaction(ushort _txnid, DnsFlags _flags, List<QuestionRecord> _questions, List<ResourceRecord> _answerRRs = null, List<ResourceRecord> _authorityRRs = null, List<ResourceRecord> _additionalRRs = null)
        {
            txnId = _txnid;
            flags = _flags;
            if (_questions == null)
                throw new MalformedRecord();
            this.queries = _questions;
            if (_answerRRs == null)
                _answerRRs = new List<ResourceRecord>(0);
            this.answerRecords = _answerRRs;
            if (_authorityRRs == null)
                _authorityRRs = new List<ResourceRecord>(0);
            this.authorityRecords = _authorityRRs;
            if (_additionalRRs == null)
                _additionalRRs = new List<ResourceRecord>(0);
            this.additionalRecords = _additionalRRs;
        }
        public static List<T> oneItemList<T>(T _item)
        {
            List<T> l = new List<T>(1);
            l.Add(_item);
            return l;
        }
        public DnsTransaction(ushort _txnid, DnsFlags _flags, QuestionRecord _question)
            : this(_txnid, _flags, oneItemList<QuestionRecord>(_question))
        {
        }

        public DnsTransaction(byte[] bytes)
        {
            ushort offset = 0;
            ushort qc;
            ushort ansc;
            ushort authc;
            ushort addc;

            this.txnId = GetLEUInt16(bytes, ref offset);
            this.flags = new DnsFlags(GetLEUInt16(bytes, ref offset));

            qc = GetLEUInt16(bytes, ref offset);
            this.queries = new List<QuestionRecord>(qc);
            ansc = GetLEUInt16(bytes, ref offset);
            this.answerRecords = new List<ResourceRecord>(ansc);
            authc = GetLEUInt16(bytes, ref offset);
            this.authorityRecords = new List<ResourceRecord>(authc);
            addc = GetLEUInt16(bytes, ref offset);
            this.additionalRecords = new List<ResourceRecord>(addc);

            for (int n = 0; n < qc; n++)
                this.queries.Add(new QuestionRecord(bytes, ref offset));
            for (int n = 0; n < ansc; n++)
                this.answerRecords.Add(new ResourceRecord(bytes, ref offset));
            for (int n = 0; n < authc; n++)
                this.authorityRecords.Add(new ResourceRecord(bytes, ref offset));
            for (int n = 0; n < addc; n++)
                this.additionalRecords.Add(new ResourceRecord(bytes, ref offset));
        }

        public byte[] getBytes()
        {
            MemoryStream buffer = new MemoryStream();
            buffer.Seek(2, SeekOrigin.Begin); // padding for length becuase of tcp
            buffer.Write(getLEBytes(txnId), 0, 2);
            buffer.Write(getLEBytes(flags.val), 0, 2);
            buffer.Write(getLEBytes((ushort)queries.Count), 0, 2);
            buffer.Write(getLEBytes((ushort)answerRecords.Count), 0, 2);
            buffer.Write(getLEBytes((ushort)authorityRecords.Count), 0, 2);
            buffer.Write(getLEBytes((ushort)additionalRecords.Count), 0, 2);
            for (int n = 0; n < this.queries.Count; n++)
            {
                byte[] subbuf = this.queries[n].getBytes();
                buffer.Write(subbuf, 0, subbuf.Length);
            }
            for (int n = 0; n < this.answerRecords.Count; n++)
            {
                byte[] subbuf = this.answerRecords[n].getBytes();
                buffer.Write(subbuf, 0, subbuf.Length);
            }
            for (int n = 0; n < this.authorityRecords.Count; n++)
            {
                byte[] subbuf = this.authorityRecords[n].getBytes();
                buffer.Write(subbuf, 0, subbuf.Length);
            }
            for (int n = 0; n < this.additionalRecords.Count; n++)
            {
                byte[] subbuf = this.additionalRecords[n].getBytes();
                buffer.Write(subbuf, 0, subbuf.Length);
            }
            // apparently tcp requests prepend the length of the query
            buffer.Seek(0, SeekOrigin.Begin);
            buffer.Write(getLEBytes((ushort)(buffer.Length - 2)), 0, 2);

            return buffer.ToArray();
        }

        public static byte[] getLEBytes(ushort x)
        {
            byte[] buffer = new byte[2];
            buffer[0] = (byte)(x >> 8);
            buffer[1] = (byte)(x & 255);
            return buffer;
        }
        public static byte[] getLEBytes(UInt32 x)
        {
            byte[] buffer = new byte[4];
            buffer[0] = (byte)(x >> 24);
            buffer[1] = (byte)(x >> 16);
            buffer[2] = (byte)(x >> 8);
            buffer[3] = (byte)(x & 255);
            return buffer;
        }
    }

}