using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ch30_1_HybridThreadSync
{
    public class Program
    {
        public static void Main(string[] args)
        {
            HybridLocks.Go();

            Console.WriteLine("Main is done.");
            Console.ReadLine();
        }
    }

    internal static class HybridLocks
    {
        public static void Go()
        {
            int x = 0;
            const int iterations = 10000000; // 10 million

            Stopwatch sw = Stopwatch.StartNew();
            for (Int32 i = 0; i < iterations; i++)
            {
                x++;
            }
            Console.WriteLine("Incrementing x: {0:N0}", sw.ElapsedMilliseconds);

            // How long does it take to increment x 10 million times 
            // adding the overhead of calling a method that does nothing?
            sw.Restart();
            for (Int32 i = 0; i < iterations; i++)
            {
                M(); x++; M();
            }
            Console.WriteLine("Incrementing x in M: {0:N0}", sw.ElapsedMilliseconds);

            // How long does it take to increment x 10 million times 
            // adding the overhead of calling an uncontended SpinLock?
            SpinLock sl = new SpinLock(false);
            sw.Restart();
            for (Int32 i = 0; i < iterations; i++)
            {
                Boolean taken = false;
                sl.Enter(ref taken); x++; sl.Exit(false);
            }
            Console.WriteLine("Incrementing x in SpinLock: {0:N0}", sw.ElapsedMilliseconds);

            // How long does it take to increment x 10 million times
            var shl = new SimpleHybridLock();
            shl.Enter();
            x++;
            shl.Leave();

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                shl.Enter();
                x++;
                shl.Leave();
            }
            Console.WriteLine("Incrementing x in SimpleHybridLock: {0:N0}", sw.ElapsedMilliseconds);

            using (var ahl = new AnotherHybridLock())
            {
                ahl.Enter();
                x++;
                ahl.Leave();
                sw.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    ahl.Enter();
                    x++;
                    ahl.Leave();
                }
                Console.WriteLine("Incrementing x in AnotherHybridLock: {0:N0}", sw.ElapsedMilliseconds);
            }

            using (SimpleWaitLock swl = new SimpleWaitLock())
            {
                sw.Restart();
                for (Int32 i = 0; i < iterations; i++)
                {
                    swl.Enter(); x++; swl.Leave();
                }
                Console.WriteLine("Incrementing x in SimpleWaitLock: {0:N0}", sw.ElapsedMilliseconds);
            }

            //using (var oml = new OneManyLock())
            //{

            //}

            //using (var rwls = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion))
            //{
            //    rwls
            //}
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void M() { }

        private sealed class SimpleWaitLock : IDisposable
        {
            private readonly AutoResetEvent m_available;
            public SimpleWaitLock()
            {
                m_available = new AutoResetEvent(true); // Initially free
            }

            public void Enter()
            {
                // Block in kernel until resource available
                m_available.WaitOne();
            }

            public void Leave()
            {
                // Let another thread access the resource
                m_available.Set();
            }

            public void Dispose() { m_available.Dispose(); }
        }

        public sealed class SimpleHybridLock : IDisposable
        {
            // The Int32 is used by the primitive user-mode constructs (Interlocked methods)
            private int m_waiters = 0;

            // The AutoResetEvent is the primitive kernel-mode construct
            private readonly AutoResetEvent m_waiterLock = new AutoResetEvent(false);

            public void Enter()
            {
                // Indicate that this thread wants the lock
                if (Interlocked.Increment(ref m_waiters) == 1)
                    return; // Lock was free, no contention, just return

                // There is contention, block this thread
                m_waiterLock.WaitOne(); // Bad performance hit here
                // When WaitOne returns, this thread now has the lock
            }

            public void Leave()
            {
                // This thread is releasing the lock
                if (Interlocked.Decrement(ref m_waiters) == 0)
                    return; // No other threads are blocked, just return

                // Other threads are blocked, wake 1 of them
                m_waiterLock.Set(); // Bad performance hit here
            }

            public void Dispose() { m_waiterLock.Dispose(); }
        }

        public sealed class AnotherHybridLock : IDisposable
        {
            // The Int32 is used by the primitive user-mode constructs (Interlocked methods)
            private Int32 m_waiters = 0;

            // The AutoResetEvent is the primitive kernel-mode construct
            private AutoResetEvent m_waiterLock = new AutoResetEvent(false);

            // This field controls spinning in an effort to improve performance
            private int m_spincount = 4000; // Arbitrarily chosen count

            // These fields indicate which thread owns the lock and how many times it owns it
            private int m_owningThreadId = 0, m_recursion = 0;

            public void Enter()
            {
                // If the calling thread already owns this lock, increment the recursion count and return
                int threadId = Thread.CurrentThread.ManagedThreadId;
                if (threadId == m_owningThreadId) { m_recursion++; return; }

                // The calling thread doesn't own the lock, try to get it
                SpinWait spinwait = new SpinWait();
                for (int spinCount = 0; spinCount < m_spincount; spinCount++)
                {
                    // If the lock was free, this thread got it; set some state and return
                    if (Interlocked.CompareExchange(ref m_waiters, 1, 0) == 0) goto GotLock;

                    // Black magic: give others threads a chance to run
                    // in hopes that the lock will be released
                    spinwait.SpinOnce();
                }

                // Spinning is over and the lock was still not obtained, try one more time
                if (Interlocked.Increment(ref m_waiters) > 1)
                {
                    // Other threads are blocked and this thread must block too
                    m_waiterLock.WaitOne(); // Wait for the lock; performance hit
                    // When this thread wakes, it owns the lock; set some state and return
                }

                GotLock:
                // When a thread gets the lock, we record its ID and
                // indicate that the thread owns the lock once
                m_owningThreadId = threadId;
                m_recursion = 1;
            }

            public void Leave()
            {
                // If the calling thread doesn't own the lock, there is a bug
                int threadId = Thread.CurrentThread.ManagedThreadId;
                if (threadId != m_owningThreadId)
                    throw new SynchronizationLockException("Lock not owned by calling thread");

                // Decrement the recursion count. If this thread still owns the lock, just return
                if (--m_recursion > 0) return;

                m_owningThreadId = 0; // No thread owns the lock now

                // If no other threads are blocked, just return
                if (Interlocked.Decrement(ref m_waiters) == 0)
                    return;

                // Other threads are blocked, wake 1 of them
                m_waiterLock.Set(); // Bad performance hit here
            }

            public void Dispose() { m_waiterLock.Dispose(); }
        }

        /// <summary>
        /// 30.3.2 Monitor class and sync block
        /// </summary>
        private sealed class Transactions : IDisposable
        {
            private readonly ReaderWriterLockSlim m_lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            private DateTime m_timeOfLastTrans;

            public void PerformTransaction()
            {
                m_lock.EnterWriteLock();

                // This code has exclusive access to the data...
                m_timeOfLastTrans = DateTime.Now;
                m_lock.ExitWriteLock();
            }

            public DateTime LastTransaction
            {
                get
                {
                    m_lock.EnterReadLock();

                    // This code has shared access to the data...
                    DateTime temp = m_timeOfLastTrans;
                    m_lock.ExitReadLock();
                    return temp;
                }
            }
            public void Dispose()
            {
                m_lock.Dispose();
            }
        }
    }
}
