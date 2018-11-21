using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ch26_1_ThreadBasics
{
    public static class ThreadBasics
    {
        public static void Main(string[] args)
        {
        }
    }

    internal static class FirstThread
    {
        public static void Go()
        {
            Console.WriteLine("Main thread: starting a dedicated thread " +
                "to do an asynchronous operation");

        }

        /// <summary>
        /// This 
        /// </summary>
        /// <param name="state"></param>
        private static void ComputeBoundOp(object state)
        {
            
        }
    }
}
