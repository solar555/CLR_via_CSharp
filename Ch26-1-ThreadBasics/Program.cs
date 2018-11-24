using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ch26_1_ThreadBasics
{
    public static class ThreadBasics
    {
        public static void Main(string[] args)
        {
            BackgroundAndForgroundDemo.Go();
            Console.ReadLine();
        }
    }

    internal static class FirstThread
    {
        public static void Go()
        {
            Console.WriteLine("Main thread: starting a dedicated thread " +
                "to do an asynchronous operation");
            Thread dedicatedThread = new Thread(ComputeBoundOp);
            dedicatedThread.Start(5);

            Console.WriteLine("Main thread: Doing other work here...");
            Thread.Sleep(10000); // Simulating other work (10 seconds)

            dedicatedThread.Join(); // Wait for thread to terminate
            Console.ReadLine();
        }

        /// <summary>
        /// This method's signature must match the ParametizedThreadStart delegate
        /// </summary>
        /// <param name="state"></param>
        private static void ComputeBoundOp(object state)
        {
            // This method is executed by another thread
            Console.WriteLine("In ComputeBoundOp: state={0}", state);
            Thread.Sleep(1000); // Simulates other work (1 second)

            // When this method returns, the dedicated thread dies
        }
    }

    internal static class BackgroundDemo
    {
        public static void Go(bool background)
        {
            // Create a new thread (defaults to Foreground)
            Thread t = new Thread(new ThreadStart(ThreadMethod));

            // Make the thread a background thread if desired
            if (background) t.IsBackground = true;

            t.Start(); // Start the thread
            return; // NOTE: the application won't actually die for about 5 seconds
        }

        private static void ThreadMethod()
        {
            Thread.Sleep(5000); // Simulate 5 seconds of work
            Console.WriteLine("ThreadMethod is exiting");
        }
    }

    internal static class BackgroundAndForgroundDemo
    {
        public static void Go()
        {
            // 创建新线程（默认为前台线程）
            Thread t = new Thread(Worker);

            // 使线程成为后台线程
            t.IsBackground = true;

            t.Start(); // 启动
            // 如果t是前台线程，则应用程序大约10s后才终止
            // 如果是后台线程，应用程序立即终止
            Console.WriteLine("Returning from Main");
        }

        private static void Worker()
        {
            // 模拟10秒工作
            Thread.Sleep(10000);
            //for (int i = 0; i < 10; i++)
            //{
            //    Thread.Sleep(1000);
            //    Console.WriteLine(i + "s");
            //}

            // 只有在由一个前台线程执行时才会显示
            Console.WriteLine("Returning from Worker");
        }
    }
}
