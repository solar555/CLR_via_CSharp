using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
            FalseSharing.Go();

            Console.WriteLine("Main is done.");
            Console.ReadLine();
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

    internal static class CancellationDemo
    {
        public static void Go()
        {

            Linking();
        }

        private static void CancellingAWorkItem()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            // Pass the CancellationToken and the number-to-count-to into the operation
            ThreadPool.QueueUserWorkItem(o => Count(cts.Token, 1000));

            Console.WriteLine("Press <Enter> to cancel the operation.");
            Console.ReadLine();
            cts.Cancel(); // If Count returned already, Cancel has no effect on it
            // Cancel returns immediately, and the method continues running here ...

            Console.ReadLine(); // For testing purposes
        }

        private static void Count(CancellationToken token, int countTo)
        {
            for (int count = 0; count < countTo; count++)
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("Count is cancelled");
                    break; // Exit the loop to stop the operation
                }

                Console.WriteLine(count);
                Thread.Sleep(200);
            }
            Console.WriteLine("Count is done");
        }

        private static void Register()
        {
            var cts = new CancellationTokenSource();
            cts.Token.Register(() => Console.WriteLine("Canceled 1"));
            cts.Token.Register(() => Console.WriteLine("Canceled 2"));

            // To test, let's just cancel it now and have the 2 callbacks execute
            cts.Cancel();
        }

        private static void Linking()
        {
            // Create a CancellationTokenSource
            var cts1 = new CancellationTokenSource();
            cts1.Token.Register(() => Console.WriteLine("cts1 canceled"));

            // Create another CancellationTokenSource
            var cts2 = new CancellationTokenSource();
            cts2.Token.Register(() => Console.WriteLine("cts2 canceled"));

            // Create a new CancellationTokenSource that is canceled when cts1 or cts2 is canceled
            /* Basically, Constructs a new CTS and registers callbacks with all he passed-in tokens. Each callback calls Cancel(false) on the new CTS */
            var ctsLinked = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);
            ctsLinked.Token.Register(() => Console.WriteLine("linkedCts canceled"));

            // Cancel one of the CancellationTokenSource objects (I chose cts2)
            cts2.Cancel();

            // Display which CancellationTokenSource objects are canceled
            Console.WriteLine("cts1 canceled={0}, cts2 canceled={1}, ctsLinked canceled={2}",
                cts1.IsCancellationRequested, cts2.IsCancellationRequested, ctsLinked.IsCancellationRequested);
        }
    }

    internal static class TaskDemo
    {
        public static void Go()
        {
            TaskFactory();
        }

        private static void UsingTaskInsteadOfQueueUserWorkItem()
        {
            ThreadPool.QueueUserWorkItem(null, 5);
        }

        private static void ComputeBoundOp(object state) { }

        private static void WaitForResult()
        {
            // Create and start a Task
            Task<int> t = new Task<int>(n => Sum((int)n), 10);

            // You can start the task sometime later
            t.Start();

            // Optionally, you can explicitly wait for the task to complete
            //t.Wait(); // FYI: Overloads exist accepting a timeout/CancellationToken
            Console.WriteLine("other task");

            // Get the result (the Result property internally calls Wait)
            Console.WriteLine("The sum is: " + t.Result);
        }

        private static int Sum(int n)
        {
            int sum = 0;
            int time = 0;
            for (; n > 0; n--) checked
                {
                    Console.WriteLine(time + "s");
                    Thread.Sleep(1000);
                    time++;
                    sum += n;
                }
            return sum;
        }

        private static int Sum(CancellationToken ct, int n)
        {
            int sum = 0;
            for (; n > 0; n--)
            {
                // The following line throws OperationCanceledException when Cancel
                // is called on the CancellationTokenSource referred to by the token
                ct.ThrowIfCancellationRequested();

                // Thread.Sleep(0); // Simulate taking a long time
                checked { sum += n; }
            }
            return sum;
        }

        private static void Cancel()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            Task<int> t = Task.Run(() => Sum(cts.Token, 10000), cts.Token);

            // Sometime later, cancel the CancellationTokenSource to cancel the Task
            cts.Cancel();

            try
            {
                // If the task got cancelled, Result will throw an AggregateException
                Console.WriteLine("The sum is: " + t.Result); // An Int32 value
            }
            catch (AggregateException ae)
            {
                // Consider any OperationCanceledException objects as handled.
                // Any other exceptions cause a new AggregateException containing
                // only the unhandled exceptions to be thrown
                ae.Handle(e => e is OperationCanceledException);

                // If all the exceptions were handled, the following executes
                Console.WriteLine("Sum was canceled");
            }
        }

        private static void ContinueWith()
        {
            // Create and start a Task, continue with another task
            Task<int> t = Task.Run(() => Sum(10000));

            // ContinueWith returns a Task but you usually don't care
            Task cwt = t.ContinueWith(task => Console.WriteLine("The sum is: " + task.Result));
            cwt.Wait(); // For the testing only
        }

        /// <summary>
        /// Ch27.5.3
        /// </summary>
        private static void MultipleContinueWith()
        {
            // Create and start a Task, continue with multiple other tasks
            Task<int> t = Task.Run(() => Sum(3));

            // Each ContinueWith returns a Task but you usually don't care
            t.ContinueWith(task => Console.WriteLine("The sum is: " + task.Result),
                TaskContinuationOptions.OnlyOnRanToCompletion);
            t.ContinueWith(task => Console.WriteLine("Sum threw: " + task.Exception),
                TaskContinuationOptions.OnlyOnFaulted);
            t.ContinueWith(task => Console.WriteLine("Sum was canceled"),
                TaskContinuationOptions.OnlyOnCanceled);

            try
            {
                t.Wait(); // For the testing only
            }
            catch (AggregateException)
            {

            }
        }

        private static void ParentChild()
        {
            Task<int[]> parent = new Task<int[]>(() =>
            {
                var results = new int[3]; // Create an array for the results

                // This tasks creates and starts 3 child tasks
                new Task(() => results[0] = Sum(1), TaskCreationOptions.AttachedToParent).Start();
                new Task(() => results[1] = Sum(2), TaskCreationOptions.AttachedToParent).Start();
                new Task(() => results[2] = Sum(3), TaskCreationOptions.AttachedToParent).Start();

                // Returns a reference to the array (even though the elements may not be initialized yet)
                return results;
            });

            // When the parent and its children have run to completion, display the results
            var cwt = parent.ContinueWith(parentTask => Array.ForEach(parentTask.Result, Console.WriteLine));

            // Start the parent Task so it can start its children
            parent.Start();

            cwt.Wait(); // For testing purposes
        }

        /// <summary>
        /// 27.5.6 page 627
        /// </summary>
        private static void TaskFactory()
        {
            Task parent = new Task(() =>
            {
                var cts = new CancellationTokenSource();
                var tf = new TaskFactory<int>(cts.Token, TaskCreationOptions.AttachedToParent, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

                // This tasks creates and starts 3 child tasks
                var childTasks = new[]
                {
                    tf.StartNew(() => Sum(cts.Token, 10000)),
                    tf.StartNew(() => Sum(cts.Token, 20000)),
                    tf.StartNew(() => Sum(cts.Token, Int32.MaxValue)) // Too big, throws OverflowException
                };

                // If any of the child tasks throw, cancel the rest of them
                for (Int32 task = 0; task < childTasks.Length; task++)
                    childTasks[task].ContinueWith(t => cts.Cancel(), TaskContinuationOptions.OnlyOnFaulted);

                // When all children are done, get the maximum value returned from the non-faulting/canceled tasks
                // Then pass the maximum value to another task which displays the maximum result
                tf.ContinueWhenAll(
                    childTasks,
                    completedTasks => completedTasks.Where(t => t.Status == TaskStatus.RanToCompletion).Max(t => t.Result),
                    CancellationToken.None)
                    .ContinueWith(t => Console.WriteLine("The maximum is: " + t.Result),
                    TaskContinuationOptions.ExecuteSynchronously).Wait(); // Wait is for testing only
            });

            // When the children are done, show any unhandled exceptions too
            parent.ContinueWith(p =>
            {
                // I put all this text in a StringBuilder and call Console.WriteLine just once because this task
                // could execute concurrently with the task above & I don't want the tasks' output interspersed
                StringBuilder sb = new StringBuilder("The following exception(s) occured:" + Environment.NewLine);
                foreach (var e in p.Exception.Flatten().InnerExceptions)
                    sb.AppendLine("   " + e.GetType().ToString());
                Console.WriteLine(sb.ToString());
            }, TaskContinuationOptions.OnlyOnFaulted);

            // Start the parent Task so it can start its children
            parent.Start();

            try
            {
                parent.Wait(); // For testing purposes
            }
            catch (AggregateException)
            {

            }
        }

        private sealed class MyForm : System.Windows.Forms.Form
        {
            private readonly TaskScheduler m_syncContextTaskScheduler;
            public MyForm()
            {
                // Get a reference to a synchronization context task scheduler
                m_syncContextTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

                Text = "Synchronization Context Task Scheduler Demo";
                Visible = true;
                Width = 400;
                Height = 100;
            }

            private CancellationTokenSource m_cts;

            protected override void OnMouseClick(System.Windows.Forms.MouseEventArgs e)
            {
                if (m_cts != null)
                {// An operation is in flight, cancel it
                    m_cts.Cancel();
                    m_cts = null;
                }
                else
                {// An operation is not in flight, start it
                    Text = "Operation running";
                    m_cts = new CancellationTokenSource();

                    // This task uses the default task scheduler and executes on a thread pool thread
                    Task<int> t = Task.Run(() => Sum(m_cts.Token, 20000), m_cts.Token);

                    // These tasks use the synchronization context task scheduler and execute on the GUI thread
                    t.ContinueWith(task => Text = "Result: " + task.Result,
                        CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion,
                        m_syncContextTaskScheduler);

                    t.ContinueWith(task => Text = "Operation canceled",
                        CancellationToken.None, TaskContinuationOptions.OnlyOnCanceled,
                        m_syncContextTaskScheduler);

                    t.ContinueWith(task => Text = "Operation faulted",
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted,
                        m_syncContextTaskScheduler);
                }
                base.OnMouseClick(e);
            }
        }
    }

    internal static class ParalleDemo
    {
        public static void Go()
        {

        }

        private static void SimpleUsage()
        {
            // One thread performs all this work sequentially
            for (int i = 0; i < 1000; i++) DoWork(i);

            // The thread pool's threads process the work in parallel
            Parallel.For(0, 1000, i => DoWork(i));

            var collection = new int[0];
            // One thread performs all this work sequentially
            foreach (var item in collection) DoWork(item);

            // The thread pool's threads process the work in parallel
            Parallel.ForEach(collection, item => DoWork(item));

            // One thread executes all the methods sequentially
            Method1();
            Method2();
            Method3();

            // The thread pool's threads execute the methods in parallel
            Parallel.Invoke(
                () => Method1(),
                () => Method2(),
                () => Method3());
        }

        private static Int64 DirectoryBytes(string path, string searchPattern, SearchOption searchOption)
        {
            var files = Directory.EnumerateFiles(path, searchPattern, searchOption);
            Int64 masterTotal = 0;

            ParallelLoopResult result = Parallel.ForEach<string, Int64>(files,
                () =>
                {
                    // localInit: Invoked once per task at start
                    // Initialize that this task has seen 0 bytes
                    return 0; // Set's taskLocalTotal to 0
                },
                (file, parallelLoopState, index, taskLocalTotal) =>
                {
                    // body: Invoked once per work item
                    // Get this file's size and add it to this task's running total
                    Int64 fileLength = 0;
                    FileStream fs = null;
                    try
                    {
                        fs = File.OpenRead(file);
                        fileLength = fs.Length;
                    }
                    catch (IOException)
                    {
                        /* Ignore any files we can't access */
                    }
                    finally { if (fs != null) fs.Dispose(); }
                    return taskLocalTotal + fileLength;
                },

                taskLocalTotal =>
                {
                    // LocalFinally: Invoked once per task at end
                    // Atomically add this task's total to the "master" total
                    Interlocked.Add(ref masterTotal, taskLocalTotal);
                });
            return masterTotal;
        }

        private static void DoWork(int i) { }

        private static void Method1() { }

        private static void Method2() { }

        private static void Method3() { }
    }

    internal static class ParallelLinq
    {
        public static void Go()
        {
            ObsoleteMethods(typeof(object).Assembly);
        }

        private static void ObsoleteMethods(Assembly assembly)
        {
            var query =
                from type in assembly.GetExportedTypes().AsParallel()
                from method in type.GetMethods(BindingFlags.Public |
                BindingFlags.Instance | BindingFlags.Static)
                let obsoleteAttrType = typeof(ObsoleteAttribute)
                where Attribute.IsDefined(method, obsoleteAttrType)
                orderby type.FullName
                let obsoleteAttrObj = (ObsoleteAttribute)
                    Attribute.GetCustomAttribute(method, obsoleteAttrType)
                select string.Format("Type={0}\nMethod={1}\nMessage={2}\n",
                    type.FullName, method.ToString(), obsoleteAttrObj.Message);

            // Display the results
            foreach (var result in query)
                Console.WriteLine(result);
            // Alternate (not as fast): query.ForAll(Console.WriteLine);
        }
    }

    internal static class TimerDemo
    {
        private static Timer s_timer;

        public static void Go()
        {
            Console.WriteLine("Checking status every 2 seconds");

            // Create the Timer ensuring that it never fires. This ensures that
            // s_timer refers to it BEFORE Status is invoked by a thread pool thread
            s_timer = new Timer(Status, null, Timeout.Infinite, Timeout.Infinite);

            // Now that s_timer is assigned to, we can let the timer fire knowing
            // that calling Change in Status will not throw a NullReferenceException
            s_timer.Change(0, Timeout.Infinite);

            Console.WriteLine(); // Prevent the process from terminating
        }

        // This method's signature must match the TimerCallback delegate
        private static void Status(object state)
        {
            // This method is executed by a thread pool thread
            Console.WriteLine("In Status at {0}", DateTime.Now);
            Thread.Sleep(1000); // Simulates other work (1 second)

            // Just before returning, have the Timer fire again in 2 seconds
            s_timer.Change(2000, Timeout.Infinite);

            // When this method returns, the thread goes back
            // to the pool and waits for another work item
        }
    }

    internal static class DelayDemo
    {
        public static void Go()
        {
            Console.WriteLine("Checking status every 2 seconds");
            Status();

            Console.WriteLine("Go is done.");
            Console.ReadLine(); // Prevent the process from terminating
        }

        // This method can take whatever parameters you desire
        private static async void Status()
        {
            while (true)
            {
                Console.WriteLine("Checking status at {0}", DateTime.Now);
                // Put code to check status here...
                Console.WriteLine("before await");

                // At end of loop, delay 2 seconds without blocking a thread
                await Task.Delay(2000); // await allows thread to return
                // After 2 seconds, some thread will continue after await to loop around
                Console.WriteLine("after await");

                break;
            }
        }
    }

    internal static class FalseSharing
    {
#if true
        private class Data
        {
            // These two fields are right next to each other in
            // memory; most-likely in the same cache line
            public int field1;
            public int field2;
        }
#else
        [StructLayout(LayoutKind.Explicit)]
        private class Data{
            // These two fields are right next to each other in
            // memory; most-likely in the same cache line
            [FieldOffset(0)]
            public Int32 field1;
            [FieldOffset(64)]
            public Int32 field2;
        }
#endif

        private const Int32 iterations = 100;
        private static int s_operations = 2;
        private static Stopwatch s_stopwatch;

        public static void Go()
        {
            Data data = new Data();
            s_stopwatch = Stopwatch.StartNew();
            ThreadPool.QueueUserWorkItem(o => AccessData(data, 0));
            ThreadPool.QueueUserWorkItem(o => AccessData(data, 1));
        }

        private static void AccessData(Data data, int field)
        {
            for (int x = 0; x < iterations; x++)
                if (field == 0) data.field1++; else data.field2++;

            if (Interlocked.Decrement(ref s_operations) == 0)
                Console.WriteLine("Access time: {0}", s_stopwatch.Elapsed);
        }
    }
}
