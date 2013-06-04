//TODO move root hint parser into DNS Factory?

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Net;

using MySql.Data.MySqlClient;


namespace whore
{
    class DB
    {
        MySqlConnection db;
        public DB(string connectionString)
        {
            try
            {
                db = new MySqlConnection(connectionString);
                db.Open();

            }
            catch (Exception e)
            {
                System.Console.WriteLine("THIS"+e.ToString());
            }
        }
        #region Dns

        void parseRootHints()
        {
            foreach (DnsTransaction.ResourceRecord rr in readCsv(@"c:\root.csv"))
            {
                cache(rr);
            }
        }

        static IEnumerable<DnsTransaction.ResourceRecord> readCsv(string file)
        {
            string line;
            using (var reader = File.OpenText(file))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    // Skip comment
                    if (line[0] == '#')
                        continue;
                                            
                    string[] substr = line.Split(',');
                    if (substr.Length != 4)
                        continue;

                    DnsTransaction.QTYPE qtype;
                    if (Enum.TryParse<DnsTransaction.QTYPE>(substr[2], out qtype))
                    {
                        byte[] data = null;
                        switch (qtype)
                        {
                            case DnsTransaction.QTYPE.NS:
                                data = DnsTransaction.getLabelBytes(substr[3]);
                                break;
                            case DnsTransaction.QTYPE.AAAA:
                            case DnsTransaction.QTYPE.A:
                                IPAddress ip;
                                ip = IPAddress.Parse(substr[3]);
                                data = ip.GetAddressBytes();
                                break;
                            case DnsTransaction.QTYPE.MX:
                                MemoryStream stream = new MemoryStream();
                                string[] subsubstr = substr[3].Split(' ');
                                stream.Write(DnsTransaction.getLEBytes(ushort.Parse(subsubstr[0])), 0, 2);
                                stream.Write(DnsTransaction.getLabelBytes(subsubstr[1]), 0, subsubstr[1].Length);
                                data = stream.ToArray();
                                break;
                        }
                        yield return new DnsTransaction.ResourceRecord(substr[0], qtype, DnsTransaction.RCLASS.IN, uint.Parse(substr[1]), data);
                    }
                }
            }
        }

        static byte[] getBlob(string str)
        {
            IPAddress ip;
            if(IPAddress.TryParse(str, out ip)) {
                return ip.GetAddressBytes();

            } else {
                return ASCIIEncoding.ASCII.GetBytes(str + (char)0);
            }
        }
        public DnsTransaction.ResourceRecord lookup(DnsTransaction.QuestionRecord record)  {
            uint qid = getDnsQueryId(record);
            using (MySqlCommand sql = new MySqlCommand("SELECT (ttl,data) FROM records WHERE query=@query ORDER BY cached DESC LIMIT 1", db))
            {
                sql.Parameters.AddWithValue("@query", qid);
                sql.Prepare();

                using (MySqlDataReader results = sql.ExecuteReader())
                {
                    byte[] chunk = new byte[1024];
                    long n = 0;
                    long r;
                    MemoryStream s = new MemoryStream();
                    do {
                        r = results.GetBytes(1, n, chunk, 0,chunk.Length);
                        s.Write(chunk, 0,(int) r);

                    } while(r == chunk.Length);
 
                    return new DnsTransaction.ResourceRecord(record, results.GetUInt32(0), s.ToArray());
                }
            }
        }
        public void cache(object obj)
        {
            uint source = 0; // source nameserver id?
            if (obj is DnsTransaction)
            {
                DnsTransaction t = obj as DnsTransaction;
                // cache question too? they're in the resource records.... 
                // there should be a table that indicates the question (request made, the time, the flags, the response code, etc)
                foreach(DnsTransaction.ResourceRecord rr in t.getRecords())
                    cache(rr);
            }
            if (obj is DnsTransaction.ResourceRecord)
            {
                DnsTransaction.ResourceRecord record = obj as DnsTransaction.ResourceRecord;
                uint qid = getDnsQueryId(record.question);
                MySqlCommand sql = new MySqlCommand("SELECT id FROM records WHERE query=@query AND ttl > (now()-cached) ORDER BY cached DESC LIMIT 1", db);
                sql.Parameters.AddWithValue("@query", qid);

                sql.Prepare();
                object r = sql.ExecuteScalar();
                if (r == null)
                {
                    // Not in cache, or expired
                    sql.CommandText = "INSERT INTO records (query,ttl,data,source) VALUES (@query,@ttl,@data,@source)";
                    sql.Parameters.AddWithValue("@ttl", record.ttl);
                    sql.Parameters.AddWithValue("@data", record.data);
                    if (source != 0)
                    {
                        sql.Parameters.AddWithValue("@source", source);
                    }
                    else
                    {
                        sql.Parameters.AddWithValue("@source", null);
                    }
                    sql.ExecuteNonQuery();
                }
            }
        }
        public uint getDnsQueryId(DnsTransaction.QuestionRecord query, uint labelId = 0) {
            if (labelId == 0)
                labelId = getDnsLabelId(query.name);
             

            MySqlCommand sql = new MySqlCommand("SELECT (id) FROM queries WHERE label=@label AND type=@type AND class=@class LIMIT 1", db);
            
            sql.Parameters.AddWithValue("@label", labelId.ToString());
            sql.Parameters.AddWithValue("@type", (uint)query._type);
            sql.Parameters.AddWithValue("@class", (uint)query._class);

            sql.Prepare();
            object r = sql.ExecuteScalar();
            if (r != null)
                return (uint)r;

            // Doesn't Exist, lets create
            sql.CommandText = "INSERT INTO queries (label,type,class) VALUES (@label,@type,@class)";
            sql.Prepare();
            sql.ExecuteNonQuery();
            return (uint)sql.LastInsertedId;
        }
        public uint getDnsLabelId(string label)
        {
            uint lid = 0;

            MySqlCommand sql = new MySqlCommand("SELECT (id) FROM labels WHERE label LIKE @label AND parent IS NULL LIMIT 1", db);
            foreach (string substr in label.Split('.').Reverse<string>()) {
                sql.Parameters.AddWithValue("@label", substr);
                if (lid != 0)
                {
                    sql.CommandText = "SELECT (id) FROM labels WHERE label LIKE @label AND parent = @parent LIMIT 1";
                    sql.Parameters.AddWithValue("@parent", lid.ToString());
                }
                sql.Prepare();
                object r = sql.ExecuteScalar();
                if (r != null)
                {
                   lid = (uint)r;
                    sql.Parameters.Clear();
                }
                else
                {
                    // Doesn't exist, create
                    if(lid == 0) {
                        sql.CommandText = "INSERT INTO labels (label) VALUES (@label)";
                    }
                    else
                    {
                        sql.CommandText = "INSERT INTO labels (label,parent) VALUES (@label,@parent)";
                    }
                    sql.Prepare();
                    sql.ExecuteNonQuery();
                    lid = (uint)sql.LastInsertedId;
                    sql.Parameters.Clear();
                }
             
            }
            return lid;
        }
        #endregion
    }
}

