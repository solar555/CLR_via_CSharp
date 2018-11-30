using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            Singletons.Go();

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

        public sealed class OneManyLock : IDisposable
        {
            #region Lock State Management
#if false

#endif
            private enum OneManyLockStates
            {
                Free = 0x00000000,
                OwnedByWriter = 0x00000001,
                OwnedByReaders = 0x00000002,
                OwnedByReadersAndWriterPending = 0x00000003,
                ReservedForWriter = 0x00000004,
            }

            private const int c_lsStateStartBit = 0;
            private const int c_lsReadersReadingStartBit = 3;
            private const int c_lsReadersWaitingStartBit = 12;
            private const int c_lsWritersWaitingStartBit = 21;

            // Mask = unchecked((Int32) ((1 << numBits) - 1) << startBit);
            private const int c_lsStateMask = unchecked((int)((1 << 3) - 1) << c_lsStateStartBit);
            private const int c_lsReadersReadingMask = unchecked((int)((1 << 9) - 1) << c_lsReadersReadingStartBit);
            private const int c_lsReadersWaitingMask = unchecked((int)((1 << 9) - 1) << c_lsReadersWaitingStartBit);
            private const int c_lsWritersWaitingMask = unchecked((int)((1 << 9) - 1) << c_lsWritersWaitingStartBit);
            private const int c_lsAnyWaitingMask = c_lsReadersWaitingMask | c_lsWritersWaitingMask;

            // FirstBit = unchecked((Int32) 1 << startBit);
            private const int c_ls1ReaderReading = unchecked((int)1 << c_lsReadersReadingStartBit);
            private const int c_ls1ReaderWaiting = unchecked((int)1 << c_lsReadersWaitingStartBit);
            private const int c_ls1WriterWaiting = unchecked((int)1 << c_lsWritersWaitingStartBit);

            private static OneManyLockStates State(int ls) { return (OneManyLockStates)(ls & c_lsStateMask); }
            private static void SetState(ref int ls, OneManyLockStates newState)
            {
                ls = (ls & ~c_lsStateMask) | ((int)newState);
            }

            private static int NumReadersReading(int ls) { return (ls & c_lsReadersReadingMask) >> c_lsReadersReadingStartBit; }
            private static void AddReadersReading(ref int ls, int amount) { ls += (c_ls1ReaderReading * amount); }

            private static int NumReadersWaiting(int ls) { return (ls & c_lsReadersWaitingMask) >> c_lsReadersWaitingStartBit; }
            private static void AddReadersWaiting(ref int ls, int amount) { ls += (c_ls1ReaderWaiting * amount); }

            private static int NumWritersWaiting(int ls) { return (ls & c_lsWritersWaitingMask) >> c_lsWritersWaitingStartBit; }
            private static void AddWritersWaiting(ref int ls, int amount) { ls += (c_ls1WriterWaiting * amount); }

            private static bool AnyWaiters(int ls) { return (ls & c_lsAnyWaitingMask) != 0; }

            private static string DebugState(int ls)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "State={0}, RR={1}, RW={2}, WW={3}", State(ls),
                    NumReadersReading(ls), NumReadersWaiting(ls), NumWritersWaiting(ls));
            }

            /// <summary>
            /// Returns a string representing the state of the object.
            /// </summary>
            /// <returns>The string representing the state of the object.</returns>
            public override string ToString()
            {
                return DebugState(m_LockState);
            }
            #endregion

            #region State Fields
            private int m_LockState = (int)OneManyLockStates.Free;

            // Readers wait on this if a writer owns the lock
            private Semaphore m_ReadersLock = new Semaphore(0, int.MaxValue);

            // Writers wait on this if a reader owns the lock
            private Semaphore m_WritersLock = new Semaphore(0, int.MaxValue);
            #endregion

            #region Construction and Dispose
            /// <summary>
            /// Constructs a OneManyLock object.
            /// </summary>
            public OneManyLock() : base() { }

            public void Dispose()
            {
                m_WritersLock.Close();
                m_WritersLock = null;
                m_ReadersLock.Close();
                m_ReadersLock = null;
            }
            #endregion

            #region Writer members
            private bool m_exclusive;

            /// <summary>
            /// Acquires the lock.
            /// </summary>
            /// <param name="exclusive"></param>
            public void Enter(bool exclusive)
            {
                if (exclusive)
                    while (WaitToWrite(ref m_LockState))
                        m_WritersLock.WaitOne();
                else
                    while (WaitToRead(ref m_LockState))
                        m_ReadersLock.WaitOne();

                m_exclusive = exclusive;
            }

            private static bool WaitToWrite(ref int target)
            {
                int start, current = target;
                bool wait;
                do
                {
                    start = current;
                    int desired = start;
                    wait = false;

                    switch (State(desired))
                    {
                        case OneManyLockStates.Free: // If Free -> OBW, return
                        case OneManyLockStates.ReservedForWriter: // If RFW - OBW, return
                            SetState(ref desired, OneManyLockStates.OwnedByWriter);
                            break;

                        case OneManyLockStates.OwnedByWriter: // If OBW -> WW++, wait & loop around
                            AddWritersWaiting(ref desired, 1);
                            wait = true;
                            break;

                        case OneManyLockStates.OwnedByReaders: // If OBR or OBRAWP -> OBRAWP, WW++, wait, loop around
                        case OneManyLockStates.OwnedByReadersAndWriterPending:
                            SetState(ref desired, OneManyLockStates.OwnedByReadersAndWriterPending);
                            AddWritersWaiting(ref desired, 1);
                            wait = true;
                            break;
                        default:
                            Debug.Assert(false, "Invalid Lock state");
                            break;
                    }
                    current = Interlocked.CompareExchange(ref target, desired, start);
                } while (start != current);
                return wait;
            }

            /// <summary>
            /// Releases the lock.
            /// </summary>
            public void Leave()
            {
                int wakeup;
                if (m_exclusive)
                {
                    Debug.Assert((State(m_LockState) == OneManyLockStates.OwnedByWriter) && (NumReadersReading(m_LockState) == 0));
                    // Pre-condition: Lock's state must be OBW (not Free/OBR/OBRAWP/RFW)
                    // Post-condition: Lock's state must become Free or RFW (the lock is never passed)

                    // Phase 1: Release the lock
                    wakeup = DoneWriting(ref m_LockState);
                }
                else
                {
                    var s = State(m_LockState);
                    Debug.Assert((State(m_LockState) == OneManyLockStates.OwnedByReaders) || (State(m_LockState) == OneManyLockStates.OwnedByReadersAndWriterPending));
                    // Pre-condition:  Lock's state must be OBR/OBRAWP (not Free/OBW/RFW)
                    // Post-condition: Lock's state must become unchanged, Free or RFW (the lock is never passed)

                    // Phase 1: Release the lock
                    wakeup = DoneReading(ref m_LockState);
                }

                // Phase 2: Possibly wake waiters
                if (wakeup == -1)
                    m_WritersLock.Release();
                else if (wakeup > 0)
                    m_ReadersLock.Release(wakeup);
            }

            /// <summary>
            /// Returns -1 to wake a writer, +# to wake # readers, or 0 to wake no one
            /// </summary>
            /// <param name="target"></param>
            /// <returns></returns>
            private static int DoneWriting(ref int target)
            {
                int start, current = target;
                int wakeup = 0;
                do
                {
                    int desired = (start = current);

                    // We do this test first because it is commonly true &
                    // we avoid the other tests improving performance
                    if (!AnyWaiters(desired))
                    {
                        SetState(ref desired, OneManyLockStates.Free);
                        wakeup = 0;
                    }
                    else if (NumWritersWaiting(desired) > 0)
                    {
                        SetState(ref desired, OneManyLockStates.ReservedForWriter);
                        AddWritersWaiting(ref desired, -1);
                        wakeup = -1;
                    }
                    else
                    {
                        wakeup = NumReadersWaiting(desired);
                        Debug.Assert(wakeup > 0);
                        SetState(ref desired, OneManyLockStates.OwnedByReaders);
                        AddReadersWaiting(ref desired, -wakeup);
                        // RW=0, RR=0 (incremented as readers enter)
                    }
                    current = Interlocked.CompareExchange(ref target, desired, start);
                } while (start != current);
                return wakeup;
            }
            #endregion

            #region Reader members
            private static bool WaitToRead(ref int target)
            {
                int start, current = target;
                bool wait;
                do
                {
                    int desired = (start = current);
                    wait = false;

                    switch (State(desired))
                    {
                        case OneManyLockStates.Free: // If Free -> OBR, RR=1, return
                            SetState(ref desired, OneManyLockStates.OwnedByReaders);
                            AddReadersReading(ref desired, 1);
                            break;

                        case OneManyLockStates.OwnedByReaders: // If OBR -> RR++, return
                            AddReadersReading(ref desired, 1);
                            break;

                        case OneManyLockStates.OwnedByWriter: // If OBW/OBRAWP/RFW -> RW++, wait, loop around
                        case OneManyLockStates.OwnedByReadersAndWriterPending:
                        case OneManyLockStates.ReservedForWriter:
                            AddReadersWaiting(ref desired, 1);
                            wait = true;
                            break;

                        default:
                            Debug.Assert(false, "Invalid Lock state");
                            break;
                    }
                    current = Interlocked.CompareExchange(ref target, desired, start);
                } while (start != current);
                return wait;
            }

            private static int DoneReading(ref int target)
            {
                int start, current = target;
                int wakeup;
                do
                {
                    int desired = (start = current);
                    AddReadersReading(ref desired, -1); // RR--
                    if (NumReadersReading(desired) > 0)
                    {
                        // RR>0, no state change & no threads to wake
                        wakeup = 0;
                    }
                    else if (!AnyWaiters(desired))
                    {
                        SetState(ref desired, OneManyLockStates.Free);
                        wakeup = 0;
                    }
                    else
                    {
                        Debug.Assert(NumWritersWaiting(desired) > 0);
                        SetState(ref desired, OneManyLockStates.ReservedForWriter);
                        AddWritersWaiting(ref desired, -1);
                        wakeup = -1; // Wake 1 writer
                    }
                    current = Interlocked.CompareExchange(ref target, desired, start);
                } while (start != current);
                return wakeup;
            }
            #endregion
        }
    }

    internal static class Singletons
    {
        public static class V1
        {
            public sealed class Singleton
            {
                // s_lock is required for thread safety and having this object assumes that creating
                // the singleton object is more expensive than creationg a System.Object object and that 
                // creating the singleton object may not be necessary at all. Otherwise, it is more
                // efficient and easier to just create the singleton object in a class constructor
                private static readonly object s_lock = new object();

                // This field will refer to the one Singleton object
                private static Singleton s_value = null;

                // Private constructor prevents any code outside this class from creating an instance
                private Singleton() { /* .. */}

                // Public, static method that returns the Singleton object (creating it if necessary)
                public static Singleton GetSingleton()
                {
                    // If the Singleton was already created, just return it (this is fast)
                    if (s_value != null) return s_value;

                    Monitor.Enter(s_lock); // Not created, let 1 thread create it
                    if (s_value == null)
                    {
                        // Still not created, create it
                        Singleton temp = new Singleton();

                        // Save the reference in s_value (see discussion for details)
                        Volatile.Write(ref s_value, temp);
                    }
                    Monitor.Exit(s_lock);

                    // Return a reference to the one Singleton object
                    return s_value;
                }
            }
        }

        public static class V2
        {
            public sealed class Singleton
            {
                private static Singleton s_value = new Singleton();

                // Private constructor prevents any code outside this class from creating an instance
                private Singleton()
                {

                }

                // Public, static method that returns the Singleton object (creating it if necessary)
                public static Singleton GetSingleton() { return s_value; }
            }
        }

        public static class V3
        {
            public sealed class Singleton
            {
                private static Singleton s_value = null;

                // Private constructor prevents any code outside this class from creating an instance
                private Singleton() { }

                // Public, static method that returns the Singleton object (creating it if necessary)
                public static Singleton GetSingleton()
                {
                    if (s_value != null) return s_value;

                    // Create a new Singleton and root it if another thread didn't do it first
                    Singleton temp = new Singleton();
                    Interlocked.CompareExchange(ref s_value, temp, null);

                    // If this thread lost, then the second Singleton object gets GC' d
                    return s_value;
                }
            }
        }

        public static void Go()
        {
            Lazy<string> s = new Lazy<string>(() => DateTime.Now.ToLongTimeString(), true);
            Console.WriteLine(s.IsValueCreated);    // false
            Console.WriteLine(s.Value);             // Lambda is invoked now
            Console.WriteLine(s.IsValueCreated);    // true
            Thread.Sleep(10000);
            Console.WriteLine(s.Value);             // Lambda is NOT invoked now; same result

            string name = null;
            LazyInitializer.EnsureInitialized(ref name, () => "Jeff");
            Console.WriteLine(name);    // Jeff

            LazyInitializer.EnsureInitialized(ref name, () => "Richter");
            Console.WriteLine(name);    // Jeff
        }
    }

    internal static class ConditionVariables
    {
        public sealed class ConditionVariablePattern
        {
            private readonly object m_lock = new object();
            private bool m_condition = false;

            public void Thread1()
            {
                Monitor.Enter(m_lock);      // Acquire a mutual-exclusive lock

                // While under the lock, test the complex condition "atomically"
                while (!m_condition)
                {
                    // If condition is not met, wait for another thread to change the condition
                    Monitor.Wait(m_lock);   // Temporarily release lock so other threads can get it
                }

                // The condition was met, process the data...
                Monitor.Exit(m_lock);       // Permanently release lock
            }

            public void Thread2()
            {
                Monitor.Enter(m_lock);      // Acquire a mutual-exclusive lock

                // Process data and modify the condition...
                m_condition = true;

                // Monitor.Pulse(m_lock);   // Wake one waiter AFTER lock is released
                Monitor.PulseAll(m_lock);   // Wakes all waiters AFTER lock is released

                Monitor.Exit(m_lock);       // Release lock
            }
        }

        public sealed class SynchronizedQueue<T>
        {
            private readonly object m_lock = new object();
            private readonly Queue<T> m_queue = new Queue<T>();

            public void Enqueue(T item)
            {
                Monitor.Enter(m_lock);

                m_queue.Enqueue(item);
                Monitor.PulseAll(m_lock);   // Wakeup any/all waiters

                Monitor.Exit(m_lock);
            }

            public T Dequeue()
            {
                Monitor.Enter(m_lock);

                // Loop waiting for condition (queue not empty)
                while (m_queue.Count == 0)
                    Monitor.Wait(m_queue);

                T item = m_queue.Dequeue();
                Monitor.Exit(m_lock);
                return item;
            }
        }
    }

    internal static class AsyncSynchronization
    {
        public static void Go()
        {
            //SemaphoreSlimDemo();
            ConcurrentExclusiveSchedulerDemo();
            OneManyDemo();
        }

        private static void SemaphoreSlimDemo()
        {
            SemaphoreSlim asyncLock = new SemaphoreSlim(1, 1);
            List<Task> tasks = new List<Task>();
            for (int op = 0; op < 5; op++)
            {
                var capturedOp = op;
                tasks.Add(Task.Run(() => AccessResourceViaAsyncSynchronization(asyncLock, capturedOp)));
                Thread.Sleep(200);
            }
            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("All operations done");
            Console.ReadLine();
        }

        private static async Task AccessResourceViaAsyncSynchronization(SemaphoreSlim asyncLock, int operation)
        {
            // Execute whatever code you want here..
            Console.WriteLine("ThreadID={0}, OpID={1}, await for {2} access",
                Environment.CurrentManagedThreadId, operation, "exclusive");
            await asyncLock.WaitAsync();        // Request exclusive access to a resource via its lock
            // When we get here, we know that no other thread his accessing the resource
            // Access the resource (exclusively)...
            Console.WriteLine("ThreadID={0}, OpID={1}, got access at {2}",
                Environment.CurrentManagedThreadId, operation, DateTime.Now.ToLongTimeString());
            Thread.Sleep(5000);

            // When done accessing resource, relinquish lock so other code can access the resource
            asyncLock.Release();

            // Execute whatever code you want here...
        }

        private static async Task AccessResourceViaAsyncSynchronization(SemaphoreSlim asyncLock)
        {
            // Execute whatever code you want here...

            await asyncLock.WaitAsync();    // Request exclusive access to a resource via its lock
            // When we get here, we know that no other thread his accessing the resource
            // Access the resource (exclusively)...

            // When done accessing resource, relinquish lock so other code can access the resource
            asyncLock.Release();

            // Execute whatever code you want here...
        }

        private static async Task AccessResourceViaAsyncSynchronization(AsyncOneManyLock asyncLock)
        {
            // Execute whatever code you want here...

            // Pass OneManyMode.Exclusive or OneManyMode.Shared depending on the concurrent access you need
            await asyncLock.WaitAsync(OneManyMode.Shared); // Request shared access to a resource via its lock
            // When we get here, no threads are writing to the resource; other threads may be reading
            // Read from the resource...

            // When done accessing resource, relinquish lock so other code can access the resource
            asyncLock.Release();

            // Execute whatever code you want here...
        }

        private static void ConcurrentExclusiveSchedulerDemo()
        {
            var cesp = new ConcurrentExclusiveSchedulerPair();
            var tfExclusive = new TaskFactory(cesp.ExclusiveScheduler);
            var tfConcurrent = new TaskFactory(cesp.ConcurrentScheduler);

            List<Task> tasks = new List<Task>();
            for (int operation = 0; operation < 5; operation++)
            {
                var capturedOp = operation;
                var exclusive = operation < 2;
                Task t = (exclusive ? tfExclusive : tfConcurrent).StartNew(() =>
                {
                    Console.WriteLine("ThreadID={0}, OpID={1}, {2} access",
                        Environment.CurrentManagedThreadId, capturedOp, exclusive ? "exclusive" : "concurrent");
                    Thread.Sleep(5000);
                });

                tasks.Add(t);
                Thread.Sleep(200);
            }
            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("All operations done");
            Console.ReadLine();
        }

        private static void OneManyDemo()
        {
            var asyncLock = new AsyncOneManyLock();
            List<Task> tasks = new List<Task>();
            for (int x = 0; x < 5; x++)
            {
                var y = x;

                tasks.Add(Task.Run(async () =>
                {
                    var mode = (y < 3) ? OneManyMode.Shared : OneManyMode.Exclusive;
                    Console.WriteLine("ThreadID={0}, OpID={1}, await for {2} access",
                        Environment.CurrentManagedThreadId, y, mode);
                    var t = asyncLock.WaitAsync(mode);
                    await t;
                    Console.WriteLine("ThreadID={0}, OpID={1}, got access at {2}",
                        Environment.CurrentManagedThreadId, y, DateTime.Now.ToLongTimeString());
                    Thread.Sleep(5000);
                    asyncLock.Release();
                }));
                Thread.Sleep(200);
            }
            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("All operations done");
            Console.ReadLine();
        }

        /// <summary>
        /// Indicates if the OneManyLock should be acquired for exclusive or shared access.
        /// </summary>
        public enum OneManyMode
        {
            /// <summary>
            /// Indicates that exclusive access is required.
            /// </summary>
            Exclusive,

            /// <summary>
            /// Indicates that shared access is required.
            /// </summary>
            Shared
        }

        /////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This class implements a reader/writer lock that never blocks any threads.
        /// To use, await the result of AccessAsync and, after manipulating shared state,
        /// call Release.
        /// </summary>
        public sealed class AsyncOneManyLock
        {
            #region Lock code
            private SpinLock m_lock = new SpinLock(true);   // Don't use readonly with a SpinLock
            private void Lock() { bool taken = false; m_lock.Enter(ref taken); }
            private void Unlock() { m_lock.Exit(); }
            #endregion

            #region Lock state and helper methods
            private int m_state = 0;
            private bool IsFree { get { return m_state == 0; } }
            private bool IsOwnedByWriter { get { return m_state == -1; } }
            private bool IsOwnedByReaders { get { return m_state > 0; } }
            private int AddReaders(int count) { return m_state += count; }
            private int SubtractReader() { return --m_state; }
            private void MakeWriter() { m_state = -1; }
            private void MakeFree() { m_state = 0; }
            #endregion

            // For the no-contention case to improve performance and reduce memory consumption
            private readonly Task m_noContentionAccessGranter;

            // Each waiting writers wakes up via their own TaskCompletionSource queued here
            private readonly Queue<TaskCompletionSource<object>> m_qWaitingWriters =
                new Queue<TaskCompletionSource<object>>();

            // All waiting readers wake up by signaling a single TaskCompletionSource
            private TaskCompletionSource<object> m_waitingReadersSignal =
                new TaskCompletionSource<object>();
            private int m_numWaitingReaders = 0;

            /// <summary>
            /// Constructs an AsyncOneManyLock object.
            /// </summary>
            public AsyncOneManyLock()
            {
                m_noContentionAccessGranter = Task.FromResult<object>(null);
            }

            /// <summary>
            /// Asynchronously requests access to the state protected by this AsyncOneManyLock
            /// </summary>
            /// <param name="mode">Specifies whether you want exclusive (write) access or shared (read) access.</param>
            /// <returns>A Task to await.</returns>
            public Task WaitAsync(OneManyMode mode)
            {
                Task accressGranter = m_noContentionAccessGranter; // Assume no contention

                Lock();
                switch (mode)
                {
                    case OneManyMode.Exclusive:
                        if (IsFree)
                            MakeWriter();   // No contention
                        else
                        {
                            // Contention: Queue new writer task & return it so writer waits
                            var tcs = new TaskCompletionSource<object>();
                            m_qWaitingWriters.Enqueue(tcs);
                            accressGranter = tcs.Task;
                        }
                        break;

                    case OneManyMode.Shared:
                        if (IsFree || (IsOwnedByReaders && m_qWaitingWriters.Count == 0))
                            AddReaders(1);  // No contention
                        else
                        {
                            // Contention: Increment waiting readers & return reader task so reader waits
                            m_numWaitingReaders++;
                            accressGranter = m_waitingReadersSignal.Task.ContinueWith(t => t.Result);
                        }
                        break;
                }

                Unlock();

                return accressGranter;
            }

            /// <summary>
            /// Releases the AsyncOneManyLock allowing other code to acquire it
            /// </summary>
            public void Release()
            {
                TaskCompletionSource<object> accessGranter = null;  // Assume no code is released

                Lock();
                if (IsOwnedByWriter) MakeFree();    // The Writer left
                else SubtractReader();              // A reader left

                if (IsFree)
                {
                    // If free, wake 1 waiting writer or all waiting readers
                    if (m_qWaitingWriters.Count > 0)
                    {
                        MakeWriter();
                        accessGranter = m_qWaitingWriters.Dequeue();
                    }
                    else if (m_numWaitingReaders > 0)
                    {
                        AddReaders(m_numWaitingReaders);
                        m_numWaitingReaders = 0;
                        accessGranter = m_waitingReadersSignal;

                        // Create a new TCS for future readers that need to wait
                        m_waitingReadersSignal = new TaskCompletionSource<object>();
                    }
                }
                Unlock();

                // Wake the writer/reader outside the lock to reduce
                // chance of contention improving performance
                if (accessGranter != null) accessGranter.SetResult(null);
            }
        }
    }

    internal static class BlockingCollectionDemo
    {
        public static void Go()
        {
            var bl = new BlockingCollection<int>(new ConcurrentQueue<int>());

            // A thread pool thread will do the consuming
            ThreadPool.QueueUserWorkItem(ConsumeItems, bl);

            // Add 5 items to the collection
            for (int item = 0; item < 5; item++)
            {
                Console.WriteLine("Producing: " + item);
                bl.Add(item);
            }

            // Tell the consuming thread(s) that no more items will be added to the collection
            bl.CompleteAdding();

            Console.ReadLine();  // For testing purposes
        }

        private static void ConsumeItems(object o)
        {
            var bl = (BlockingCollection<int>)o;

            // Block until an item shows up, then process it
            foreach (var item in bl.GetConsumingEnumerable())
            {
                Console.WriteLine("Consuming: " + item);
            }

            // The collection is empty and no more items are going into it
            Console.WriteLine("All items have been consumed");
        }
    }
}
