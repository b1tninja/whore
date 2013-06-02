using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

using Starksoft.Net.Proxy;

namespace whore
{
    abstract class Task
    {
        public delegate void TaskResultHandler(object taskResult);
        protected TaskResultHandler callback = null;

        public Task()
        {
        }

        public virtual void execute(TaskResultHandler callback)
        {
            this.callback = callback;
            System.Console.WriteLine("Executing task {0}.", this.GetHashCode());
        }
    }
}
