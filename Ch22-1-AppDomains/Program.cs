using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ch22_1_AppDomains
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            AppDomainResourceMonitoring();

            Console.ReadKey();
        }

        private static void Marshalling()
        {
            AppDomain adCallingThreadDomain = Thread.GetDomain();

            string callingDomainName = adCallingThreadDomain.FriendlyName;
            Console.WriteLine("Default AppDomain's friendly name={0}", callingDomainName);

            string exeAssembly = Assembly.GetEntryAssembly().FullName;
            Console.WriteLine("Main assembly={0}", exeAssembly);

            AppDomain ad2 = null;

            // *** DEMO 1:使用Marshal-by-Reference进行跨APPDomain通信 ***
            Console.WriteLine("{0}Demo #1", Environment.NewLine);

            // 新建一个AppDomain(从当前AppDomain继承安全性和配置)
            ad2 = AppDomain.CreateDomain("AD #2", null, null);
            MarshalByRefType mbrt = null;

            // 将我们的程序集加载到新AppDomain中，构造一个对象，把它
            // 封送回我们的AppDomain（实际得到对一个代理的引用）
            mbrt = (MarshalByRefType)
                ad2.CreateInstanceAndUnwrap(exeAssembly, "MarshalByRefType");

            // CLR 在类型上撒谎了
            Console.WriteLine("Type={0}", mbrt.GetType());

            // 证明得到的是对一个代理对象的引用
            Console.WriteLine("Is proxy={0}", RemotingServices.IsTransparentProxy(mbrt));

            // 看起来像是在MarshalByRefType上调用一个方法，实则不然
            // 我们是在代理类型上调用一个方法，代理使线程切换到拥有对象的
            // 那个AppDomain，并在真实的对象上调用这个方法
            mbrt.SomeMethod();

            // 卸载新的AppDomain
            AppDomain.Unload(ad2);

            // mbrt引用一个有效的代理对象；代理对象引用一个无效的AppDomain
            try
            {
                // 在代理类型上调用一个方法，AppDomain无效，造成抛出异常
                mbrt.SomeMethod();
                Console.WriteLine("Successful call.");
            }
            catch(AppDomainUnloadedException)
            {
                Console.WriteLine("Failed call.");
            }

            // *** DEMO 2:使用Marshal-by-Value进行跨AppDomain通信 ***
            Console.WriteLine("{0}Demo #2", Environment.NewLine);

            // 新建一个AppDomain（从当前AppDomain继承安全性和配置）
            ad2 = AppDomain.CreateDomain("AD #2", null, null);

            // 将我们的程序集加载到新AppDomain中，构造一个对象，把它
            // 封送回我们的AppDomain（实际得到对一个代理的引用）
            mbrt = (MarshalByRefType)
                ad2.CreateInstanceAndUnwrap(exeAssembly, "MarshalByRefType");

            // 对象的方法返回所返回对象的副本；
            // 对象按值（而非按引用）封送
            MarshalByValType mbvt = mbrt.MethodWithReturn();

            // 证明得到的不是对一个代理对象的引用
            Console.WriteLine("Is proxy={0}", RemotingServices.IsTransparentProxy(mbvt));

            // 看起来是在MarshalByValType上调用一个方法，实际也是如此
            Console.WriteLine("Returned object created " + mbvt.ToString());

            // 卸载新的AppDomain
            AppDomain.Unload(ad2);
            // mbvt引用有效的对象：卸载AppDomain没有影响

            try
            {
                // 我们是在对象上调用一个方法：不会抛出异常
                Console.WriteLine("Returned object created " + mbvt.ToString());
                Console.WriteLine("Successful call.");
            }
            catch (AppDomainUnloadedException)
            {
                Console.WriteLine("Failed call.");
            }

            // DEMO 3: 使用不可封送的类型进行跨AppDomain通信 ***
            Console.WriteLine("{0}Demo #3", Environment.NewLine);

            // 新建一个AppDomain（从当前AppDomain继承安全性和配置）
            ad2 = AppDomain.CreateDomain("AD #2", null, null);
            // 将我们的程序集加载到新AppDomain中，构造一个对象，把它
            // 封送回我们的AppDomain（实际得到对一个代理的引用）
            mbrt = (MarshalByRefType)
                ad2.CreateInstanceAndUnwrap(exeAssembly, "MarshalByRefType");

            // 对象的方法返回一个不可封送的对象：抛出异常
            NonMarshalableType nmt = mbrt.MethodArgAndReturn(callingDomainName);
            // 这里永远执行不到...
        }

        /// <summary>
        /// 该类的实例可跨越AppDomain的边界“按引用封送”
        /// </summary>
        public sealed class MarshalByRefType : MarshalByRefObject
        {
            public MarshalByRefType()
            {
                Console.WriteLine("{0} ctor running in {1}",
                    this.GetType().ToString(), Thread.GetDomain().FriendlyName);
            }

            public void SomeMethod()
            {
                Console.WriteLine("Executing in " + Thread.GetDomain().FriendlyName);
            }

            public MarshalByValType MethodWithReturn()
            {
                Console.WriteLine("Executing in" + Thread.GetDomain().FriendlyName);
                MarshalByValType t = new MarshalByValType();
                return t;
            }

            public NonMarshalableType MethodArgAndReturn(string callingDomainName)
            {
                // 注意：callingDomainName 是可序列化的
                Console.WriteLine("Calling from '{0}' to '{1}'.",
                    callingDomainName, Thread.GetDomain().FriendlyName);
                NonMarshalableType t = new NonMarshalableType();
                return t;
            }

        }

        /// <summary>
        /// 该类的实例可跨越AppDomain的边界“按值封送”
        /// </summary>
        [Serializable]
        public sealed class MarshalByValType : Object
        {
            // 注意：DateTime是可序列化的
            private DateTime m_creationTime = DateTime.Now;

            public MarshalByValType()
            {
                Console.WriteLine("{0} ctor running in {1}, Created on {2:D}",
                    this.GetType().ToString(),
                    Thread.GetDomain().FriendlyName,
                    m_creationTime);
            }

            public override string ToString()
            {
                return m_creationTime.ToLongDateString();
            }
        }

        /// <summary>
        /// 该类的实例不能跨越AppDomain边界进行封送
        /// [Serializable]
        /// </summary>
        public sealed class NonMarshalableType : Object
        {
            public NonMarshalableType()
            {
                Console.WriteLine("Executing in " + Thread.GetDomain().FriendlyName);
            }
        }

        private sealed class MBRO : MarshalByRefObject { public int x; }
        private sealed class NonMBRO:Object { public int x; }

        private static void FieldAccessTiming()
        {
            const int count = 100000000;
            NonMBRO nonMbro = new NonMBRO();
            MBRO mbro = new MBRO();

            Stopwatch sw = Stopwatch.StartNew();
            for (int c = 0; c < count; c++)
                nonMbro.x++;
            
            Console.WriteLine("{0}", sw.Elapsed);

            sw = Stopwatch.StartNew();
            for (int c = 0; c < count; c++)
                mbro.x++;
            
            Console.WriteLine("{0}", sw.Elapsed);
        }

        private sealed class AppDomainMonitorDelta : IDisposable
        {
            private AppDomain m_appDomain;
            private TimeSpan m_thisADCpu;
            private Int64 m_thisADMemoryInUse;
            private Int64 m_thisADMemoryAllocated;

            static AppDomainMonitorDelta()
            {
                AppDomain.MonitoringIsEnabled = true;
            }

            public AppDomainMonitorDelta(AppDomain ad)
            {
                m_appDomain = ad ?? AppDomain.CurrentDomain;
                m_thisADCpu = m_appDomain.MonitoringTotalProcessorTime;
                m_thisADMemoryInUse = m_appDomain.MonitoringSurvivedMemorySize;
                m_thisADMemoryAllocated = m_appDomain.MonitoringTotalAllocatedMemorySize;
            }

            public void Dispose()
            {
                GC.Collect();
                Console.WriteLine("FriendlyName={0}, CPU={1}ms", m_appDomain.FriendlyName,
                    (m_appDomain.MonitoringTotalProcessorTime - m_thisADCpu).TotalMilliseconds);
                Console.WriteLine(" Allocated {0:N0} bytes of which {1:N0} survived GCs",
                    m_appDomain.MonitoringTotalAllocatedMemorySize - m_thisADMemoryAllocated,
                    m_appDomain.MonitoringSurvivedMemorySize - m_thisADMemoryInUse);
            }
        }

        private static void AppDomainResourceMonitoring()
        {
            using(new AppDomainMonitorDelta(null))
            {
                var list = new List<Object>();
                for (int x = 0; x < 1000; x++)
                {
                    list.Add(new Byte[10000]);
                }

                for (int x = 0; x < 2000; x++)
                {
                    new Byte[10000].GetType();
                }

                Int64 stop = Environment.TickCount + 5000;
                while (Environment.TickCount < stop) ;
            }
        }
    }
}
