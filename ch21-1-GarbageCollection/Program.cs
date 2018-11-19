using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ch21_1_GarbageCollection
{
    class Program
    {
        static void Main(string[] args)
        {
            ConditionalWeakTableDemo.Go();

            Console.WriteLine("well done！");
            Console.ReadLine();
        }
    }

    internal static class DebuggingRoots
    {
        public static void Go()
        {
            var t = new Timer(TimerCallback, null, 0, 2000);

            Console.ReadLine();
        }

        private static void TimerCallback(object o)
        {
            Console.WriteLine("In TimerCallback: " + DateTime.Now);
            GC.Collect();
        }
    }

    internal static class GCNotifications
    {
        private static Action<int> s_gcDone = null;

        public static event Action<int> GCDone
        {
            add
            {
                if (s_gcDone == null)
                {
                    new GenObject(0);
                    new GenObject(2);
                }
                s_gcDone += value;
            }
            remove { s_gcDone -= value; }
        }

        private sealed class GenObject
        {
            private int m_generation;
            public GenObject(int generation)
            {
                m_generation = generation;
            }

            ~GenObject()
            {
                if (GC.GetGeneration(this) >= m_generation)
                {
                    Action<int> temp = Volatile.Read(ref s_gcDone);
                    if (temp != null) temp(m_generation);
                }

                if ((s_gcDone != null)
                    && !AppDomain.CurrentDomain.IsFinalizingForUnload()
                    && !Environment.HasShutdownStarted)
                {
                    if (m_generation == 0) new GenObject(0);
                    else GC.ReRegisterForFinalize(this);
                }
                else { /* 放过对象，让其被回收 */}
            }
        }
    }

    internal static class ConstructorThrowsAndFinalize
    {
        internal sealed class TempFileV1
        {
            private string m_filename = null;
            private FileStream m_fs;

            public TempFileV1()
            {

            }

            ~TempFileV1()
            {

            }
        }
    }

    internal static class DisposePattern
    {
        public static void Go()
        {
            FileStream fs = new FileStream("DataFile.dat", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            sw.Write("Hi there");

            /// Microsoft规定：
            /// StreamWriter必须显示调用Dispose()，否则数据肯定会丢失。
            /// Microsoft希望开发人员注意到这个数据一直丢失的问题，并插入对Dispose的调用来修正代码。
            /// ——《CLR via C#(第四版)》
            sw.Dispose();
        }
    }

    internal static class MemoryPressureAndHandleCollector
    {
        public static void Go()
        {
            MemoryPressureDemo(0);                  // 0    causes infrequent GCs
            MemoryPressureDemo(10 * 1024 * 1024);   // 10MB causes frequent GCs

            HandleCollectorDemo();
        }

        private static void MemoryPressureDemo(int size)
        {
            Console.WriteLine();
            Console.WriteLine("MemoryPressureDemo, size={0}", size);
            for (int count = 0; count < 15; count++)
            {
                new BigNativeResource(size);
            }
        }

        private sealed class BigNativeResource
        {
            private int m_size;

            public BigNativeResource(int size)
            {
                m_size = size;
                if(m_size > 0)
                {
                    GC.AddMemoryPressure(m_size);
                }
                Console.WriteLine("BigNativeResource create.");
            }

            ~BigNativeResource()
            {
                if(m_size > 0)
                {
                    GC.RemoveMemoryPressure(m_size);
                }
                Console.WriteLine("BigNativeResource destroy.");
            }
        }

        private static void HandleCollectorDemo()
        {
            Console.WriteLine();
            Console.WriteLine("HandleCollectorDemo");
            for (int count = 0; count < 10; count++)
            {
                new LimitedResource();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private sealed class LimitedResource
        {
            private static HandleCollector s_hc = new HandleCollector("LimitedResource", 2);

            public LimitedResource()
            {
                s_hc.Add();
                Console.WriteLine("LimitedResource create.  Count={0}", s_hc.Count);
            }

            ~LimitedResource()
            {
                s_hc.Remove();
                Console.WriteLine("LimitedResource destroy. Count={0}", s_hc.Count);
            }
        }
    }

    internal static class FixedStatement
    {
        unsafe public static void Go()
        {
            for (int x = 0; x < 10000; x++)
            {
                new Object();
            }

            IntPtr originalMemoryAddress;
            Byte[] bytes = new byte[1000];

            fixed (Byte* pbytes = bytes) { originalMemoryAddress = (IntPtr)pbytes; }

            GC.Collect();

            fixed(byte* pbytes = bytes)
            {
                Console.WriteLine("The Byte[] did{0} move during the GC",
                    (originalMemoryAddress == (IntPtr)pbytes)? " not" : null);
            }
        }
    }

    internal static class ConditionalWeakTableDemo
    {
        public static void Go()
        {
            object o = new object().GCWatch("My Object created at " + DateTime.Now);
            GC.Collect();   // Can not see GC notification now
            GC.KeepAlive(o);// Ensure object referenced by o is alive
            o = null;       // Object referenced by o could die

            GC.Collect();   // Can see GC notification now
            Console.ReadLine();
        }
    }

    internal static class GCWatcher
    {
        private readonly static ConditionalWeakTable<object, NotifyWhenGCd<string>> s_cwt =
            new ConditionalWeakTable<object, NotifyWhenGCd<string>>();

        private sealed class NotifyWhenGCd<T>
        {
            private readonly T m_value;

            internal NotifyWhenGCd(T value) { m_value = value; }

            public override string ToString()
            {
                return m_value.ToString();
            }
            ~NotifyWhenGCd()
            {
                Console.WriteLine("GC'd: " + m_value);
            }
        }

        public static T GCWatch<T>(this T @object, string tag) where T : class
        {
            s_cwt.Add(@object, new NotifyWhenGCd<string>(tag));
            return @object;
        }
    }
}
