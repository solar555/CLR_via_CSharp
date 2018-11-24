using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ch27_1_ComputeOps
{
    public class Program
    {
        static void Main(string[] args)
        {
            ExecutionContexts.Go();
        }
    }

    internal static class ThreadPoolDemo
    {
        public static void Go()
        {
            Console.WriteLine("Main thread: queuing an asynchronous operation");
            ThreadPool.QueueUserWorkItem(ComputeBoundOp, 5);
            Console.WriteLine("Main thread: Doing other work here ...");
            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine(i + "s");
                Thread.Sleep(1000); // Simulating other work (10s)
            }
            Console.WriteLine("Other work is done.");
            Console.ReadLine();
        }

        private static void ComputeBoundOp(object state)
        {
            // This method is executed by a thread pool thread
            Console.WriteLine("In ComputeBoundOp: state={0}", state);
            Thread.Sleep(1000); // Simulate other work (1s)
            Console.WriteLine("ComputeBoundOp is done.");

            // When this method returns, the thread goes back
            // to the pool and waits for another task
        }
    }

    internal static class ExecutionContexts
    {
        public static void Go()
        {
            // Put some data into the Main thread's logical call context
            CallContext.LogicalSetData("Name", "Jeffrey");

            // Initiate some work to be done by a thread pool thread
            // The thread pool thread can access the logical call context data
            ThreadPool.QueueUserWorkItem(
                state => Console.WriteLine("Name={0}", CallContext.LogicalGetData("Name")));

            // Suppress the flowing of the Main thread's execution context
            ExecutionContext.SuppressFlow();

            // Initiate some work to be done by a thread pool thread
            // The thread pool thread can NOT access the logical call context data
            ThreadPool.QueueUserWorkItem(
                state => Console.WriteLine("Name={0}", CallContext.LogicalGetData("Name")));

            // Restore the flowing of the Main thread's execution context in case
            // it employs more thread pool threads in the future
            ExecutionContext.RestoreFlow();

            ThreadPool.QueueUserWorkItem(
                state => Console.WriteLine("Name={0}", CallContext.LogicalGetData("Name")));

            Console.ReadLine();
        }
    }
}
