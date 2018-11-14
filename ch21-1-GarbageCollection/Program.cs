using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ch21_1_GarbageCollection
{
    class Program
    {
        static void Main(string[] args)
        {

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
                if(s_gcDone == null)
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
                if(GC.GetGeneration(this) >= m_generation)
                {
                    Action<int> temp = Volatile.Read(ref s_gcDone);
                    if (temp != null) temp(m_generation);
                }

                if((s_gcDone != null)
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
}
