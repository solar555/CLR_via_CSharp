using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ch29_1_PrimitiveThreadSync
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AsyncCoordinatorDemo.Go();
            Console.ReadLine();
        }

        private static void OptimizedAway()
        {
            // An expression of constants is computed at compile time then put into a local variable that is never used
            int value = 1 * 100 - 50 * 2;

            for (int x = 0; x < value; x++) Console.WriteLine("Jeff"); // A loop that does nothing
        }
    }

    #region LinkedList Synchronization
    // This class is used by the LinkedList class
    public class Node { internal Node m_next; }

    public sealed class SomeKindOfLock
    {
        public void Acquire() { }
        public void Release() { }
    }

    public sealed class LinkedList
    {
        private SomeKindOfLock m_lock = new SomeKindOfLock();
        private Node m_head;

        public void Add(Node newNode)
        {
            m_lock.Acquire();
            // The two lines below perform very fast reference assignments
            newNode.m_next = m_head;
            m_head = newNode;
            m_lock.Release();
        }
    }
    #endregion

    internal static class StrangeBehavior
    {
        // Compile with "/platform:x86 /o" and run it NOT under the debugger (Ctrl+F5)
        private static bool s_stopWorker = false;

        public static void Go()
        {
            Console.WriteLine("Main: letting worker run for 5 seconds");
            Thread t = new Thread(Worker);
            t.Start();
            Thread.Sleep(5000);
            s_stopWorker = true;
            Console.WriteLine("Main: waiting for worker to stop");
            t.Join();
            Environment.Exit(0);
        }

        private static void Worker(object o)
        {
            int x = 0;
            while (!s_stopWorker) x++;
            Console.WriteLine("Worker: stopped when x={0}", x);
        }
    }

    internal static class ThreadsSharingData
    {
        internal sealed class ThreadsSharingDataV1
        {
            private int m_flag = 0;
            private int m_value = 0;

            // This method is executed by one thread
            public void Thread1()
            {
                // Note: These could execute in reverse order
                m_value = 5;
                m_flag = 1;
            }

            // This method is executed by another thread
            public void Thread2()
            {
                // Note: m_value could be read before m_flag
                if (m_flag == 1) Console.WriteLine(m_value);
            }
        }

        internal sealed class ThreadsSharingDataV2
        {
            private int m_flag = 0;
            private int m_value = 0;

            // This method is executed by one thread
            public void Thread1()
            {
                // Note: 5 must be written to m_value before 1 is written to m_flag
                m_value = 5;
                Volatile.Write(ref m_flag, 1);
            }

            // This method is executed by another thread
            public void Thread2()
            {
                // Note: m_value must be read after m_flag is read
                if (Volatile.Read(ref m_flag) == 1)
                    Console.WriteLine(m_value);
            }
        }

        internal sealed class ThreadsSharingDataV3
        {
            private volatile int m_flag = 0;
            private int m_value = 0;

            // This method is executed by one thread
            public void Thread1()
            {
                // Note: 5 must be written to m_value before 1 is written to m_flag
                m_value = 5;
                m_flag = 1;
            }

            // This method is executed by another thread
            public void Thread2()
            {
                // Note: m_value must be read after m_flag is read
                if (m_flag == 1)
                    Console.WriteLine(m_value);
            }
        }
    }

    /// <summary>
    /// 29.3.2
    /// </summary>
    internal static class AsyncCoordinatorDemo
    {
        public static void Go()
        {
            const int timeout = 50000; // Change to desired timeout
            MultiWebRequests act = new MultiWebRequests(timeout);
            Console.WriteLine("All operations initiated (Timeout={0}). Hit <Enter> to cancel.",
                (timeout == Timeout.Infinite) ? "Infinite" : (timeout.ToString() + "ms"));
            Console.ReadLine();
            act.Cancel();

            Console.WriteLine();
            Console.WriteLine("Hit enter to terminate.");
            Console.ReadLine();
        }

        private sealed class MultiWebRequests
        {
            // This helper class coordinates all the asynchronous operations
            private AsyncCoordinator m_ac = new AsyncCoordinator();

            // Set of Web servers we want  to query & their responses (Exception or Int32)
            private Dictionary<string, object> m_servers = new Dictionary<string, object>
            {
                {"http://Wintellect.com/", null },
                {"http://Microsoft.com", null },
                {"http://1.1.1.1/", null }
            };

            public MultiWebRequests(int timeout = Timeout.Infinite)
            {
                // Asynchronously initiate all the requests all at once
                var httpClient = new HttpClient();
                foreach (var server in m_servers.Keys)
                {
                    m_ac.AboutToBegin(1);
                    httpClient.GetByteArrayAsync(server).ContinueWith(task => ComputeResult(server, task));
                }

                // Tell AsyncCoordinator that all operations have been initiated and to call
                // AllDone when all operations complete, Cancel is called, or the timeout occurs
                m_ac.AllBegun(AllDone, timeout);
            }

            private void ComputeResult(string server, Task<byte[]> task)
            {
                object result;
                if (task.Exception != null)
                    result = task.Exception.InnerException;
                else
                    // Process I/O completion here on thread pool thread(s)
                    // Put your own compute-intensive algorithm here...
                    result = task.Result.Length; // This example just returns the length

                // Save result (exception/sum) and indicate that 1 operation completed
                m_servers[server] = result;
                m_ac.JustEnded();
            }

            // Calling this method indicates that the results don't matter anymore
            public void Cancel() { m_ac.Cancel(); }

            // This method is called after all Web servers respond,
            // Cancel is called, or the timeout occurs
            private void AllDone(CoordinationStatus status)
            {
                switch (status)
                {
                    case CoordinationStatus.Cancel:
                        Console.WriteLine("Operation canceled.");
                        break;

                    case CoordinationStatus.Timeout:
                        Console.WriteLine("Operation timed-out.");
                        break;

                    case CoordinationStatus.AllDone:
                        Console.WriteLine("Operation completed; results below:");
                        foreach (var server in m_servers)
                        {
                            Console.Write("{0} ", server.Key);
                            object result = server.Value;
                            if (result is Exception)
                            {
                                Console.WriteLine("failed due to {0}.", result.GetType().Name);
                            }
                            else
                            {
                                Console.WriteLine("returned {0:N0} bytes.", result);
                            }
                        }
                        break;
                }
            }
        }

        private enum CoordinationStatus
        {
            AllDone,
            Timeout,
            Cancel
        };

        private sealed class AsyncCoordinator
        {
            private int m_opCount = 1; // Decremented when AllBegun calls JustEnded
            private int m_statusReported = 0; // 0=false, 1=true
            private Action<CoordinationStatus> m_callback;
            private Timer m_timer;

            // This method MUST be called BEFORE initiating an operation
            public void AboutToBegin(int opsToAdd = 1)
            {
                Interlocked.Add(ref m_opCount, opsToAdd);
            }

            // This method MUST be called AFTER an operations result has been processed
            public void JustEnded()
            {
                if (Interlocked.Decrement(ref m_opCount) == 0)
                    ReportStatus(CoordinationStatus.AllDone);
            }

            // This method MUST be called AFTER initiating ALL operations
            public void AllBegun(Action<CoordinationStatus> callback, int timeout = Timeout.Infinite)
            {
                m_callback = callback;
                if (timeout != Timeout.Infinite)
                {
                    m_timer = new Timer(TimeExpired, null, timeout, Timeout.Infinite);
                }
                JustEnded();
            }

            private void TimeExpired(object o)
            {
                ReportStatus(CoordinationStatus.Timeout);
            }

            public void Cancel()
            {
                if (m_callback == null)
                    throw new InvalidOperationException("Cancel cannot be called before AllBegun");
                ReportStatus(CoordinationStatus.Cancel);
            }

            private void ReportStatus(CoordinationStatus status)
            {
                if (m_timer != null)
                {
                    // If timer is still in play, kill it
                    Timer timer = Interlocked.Exchange(ref m_timer, null);
                    if (timer != null) timer.Dispose();
                }

                // If status has never been reported, report it; else ignore it
                if (Interlocked.Exchange(ref m_statusReported, 1) == 0)
                    m_callback(status);
            }
        }
    }
}
