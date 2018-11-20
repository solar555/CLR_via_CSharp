using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ch23_1_AssemblyLoadingAndReflection
{
    interface IAddIn
    {
        bool DoSomething(int a);
    }
    public class Program
    {
        static void Main(string[] args)
        {
            ConvertMethodInfoToRuntimeMethodHandle.Go();

            Console.ReadKey();
        }
    }

    /// <summary>
    /// 23.1
    /// </summary>
    internal static class DynamicLoadFromResource
    {
        static void Go()
        {
            Assembly a = Assembly.LoadFrom(@"C:\Users\Administrator\Desktop\MyTest\CLR_via_CSharp\Ch22-1-AppDomains\bin\Debug\Ch22-1-AppDomains.exe");
            Console.WriteLine("a.ToString():\n{0}", a.ToString());
            Console.WriteLine("Load Done!");
            Console.ReadKey();
        }

        private static Assembly ResolveEventHandler(object sender, ResolveEventArgs args)
        {
            string dllName = new AssemblyName(args.Name).Name + ".dll";

            var assem = Assembly.GetExecutingAssembly();
            string resourceName = assem
                .GetManifestResourceNames()
                .FirstOrDefault(rn => rn.EndsWith(dllName));

            // Not found, maybe another handler will find it
            if (resourceName == null) return null;

            using (var stream = assem.GetManifestResourceStream(resourceName))
            {
                Byte[] assemblyData = new byte[stream.Length];
                stream.Read(assemblyData, 0, assemblyData.Length);
                return Assembly.Load(assemblyData);
            }
        }
    }

    /// <summary>
    /// 23.3.1
    /// </summary>
    internal static class DiscoverTypes
    {
        public static void Go()
        {
            string dataAssembly = "System.Data, version=4.0.0.0, " +
                "culture=neutral, PublicKeyToken=677a5c561934e089";
            LoadAssemAndShowPublicTypes(dataAssembly);
        }

        private static void LoadAssemAndShowPublicTypes(string assemId)
        {
            // Explicitly load an assembly in to this AppDomain
            Assembly a = Assembly.Load(assemId);

            // Execute this loop once for each Type
            // publicly-exported from the loaded assembly
            foreach (Type t in a.ExportedTypes)
            {
                // Display the full name of the type
                Console.WriteLine(t.FullName);
            }
        }
    }

    internal static class GetTypeByOperator_typeof
    {
        private static void SomeMethod(object o)
        {
            // GetType在运行时返回对象的类型（晚期绑定）
            // typeof返回指定类的类型（早期绑定）
            if (o.GetType() == typeof(FileInfo)) { }
            if (o.GetType() == typeof(DirectoryInfo)) { }
        }
    }

    internal static class ExceptionTree
    {
        public static void Go()
        {
            // Explicitly load the assemblies that we want to reflect over
            LoadAssemblies();

            // Filter & sort all the types
            var allTypes =
                (from a in new[] { typeof(object).Assembly }// AppDomain.CurrentDomain.GetAssemblies()
                 from t in a.ExportedTypes
                 where typeof(Exception).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo())
                 orderby t.Name
                 select t).ToArray();

            // Build the inheritance hierarchy tree and show it
            Console.WriteLine(WalkInheritanceHierarchy(new StringBuilder(), 0, typeof(Exception), allTypes));
        }

        private static StringBuilder WalkInheritanceHierarchy(
            StringBuilder sb,
            Int32 indent,
            Type baseType,
            IEnumerable<Type> allTypes)
        {
            string spaces = new string(' ', indent * 3);
            sb.AppendLine(spaces + baseType.FullName);
            foreach (var t in allTypes)
            {
                if (t.GetTypeInfo().BaseType != baseType) continue;
                WalkInheritanceHierarchy(sb, indent + 1, t, allTypes);
            }
            return sb;
        }

        private static void LoadAssemblies()
        {
            string[] assemblies =
            {
                "System,                    PublicKeyToken={0}",
                "System.Core,PublicKeyToken={0}",
                "System.Data,PublicKeyToken={0}",
                "System.Design,PublicKeyToken={1}",
                "System.DirectoryServices,PublicKeyToken={1}",
                "System.Drawing,PublicKeyToken={1}",
                "System.Drawing.Design,PublicKeyToken={1}",
                "System.Management,PublicKeyToken={1}",
                "System.Messaging,PublicKeyToken={1}",
                "System.Runtime.Remoting,PublicKeyToken={0}",
                "System.Runtime.Serialization,PublicKeyToken={0}",
                "System.Security,PublicKeyToken={1}",
                "System.ServiceModel,PublicKeyToken={0}",
                "System.ServiceProcess,PublicKeyToken={1}",
                "System.Web,PublicKeyToken={1}",
                "System.Web.RegularExpressions,PublicKeyToken={1}",
                "System.Web.Services,PublicKeyToken={1}",
                "System.Xml,PublicKeyToken={0}",
                "System.Xml.Linq,PublicKeyToken={0}",
                "Microsoft.CSharp,PublicKeyToken={1}",
            };

            const string EcmaPublicKeyToken = "b77a5c561934e089";
            const string MSPublicKeyToken = "b03f5f7f11d50a3a";

            // Get the version of the assembly containing System.Object
            // We'll assume the same version for all the other assemblies
            Version version = typeof(System.Object).Assembly.GetName().Version;

            // Explicitly load the assemblies that we want to reflect over
            foreach (string a in assemblies)
            {
                string AssemblyIdentity =
                    string.Format(a, EcmaPublicKeyToken, MSPublicKeyToken) +
                    ", Culture=neutral, Version=" + version;
                Assembly.Load(AssemblyIdentity);
            }
        }
    }

    internal static class ConstructingGenericType
    {
        private sealed class Dictionary<TKey, TValue> { }

        public static void Go()
        {
            // Get a reference to the generic type's type object
            Type openType = typeof(Dictionary<,>);

            // Close the generic type by using TKey=String, TValue=Int32
            Type closedType = openType.MakeGenericType(typeof(string), typeof(Int32));

            // Construct an instance of the closed type
            object o = Activator.CreateInstance(closedType);

            // Prove it worked
            Console.WriteLine(o.GetType());
        }
    }

    internal class ComposeClass
    {
        public interface IBookService
        {
            void GetBookName();
        }

        [Export(typeof(IBookService))]
        public class ComputerBookService : IBookService
        {
            public void GetBookName()
            {
                Console.WriteLine("《Hello Silverlight》");
            }
        }

        public class Program
        {
            [Import]
            public IBookService Service { get; set; }

            private void Compose()
            {
                var catalog = new AssemblyCatalog(Assembly.GetExecutingAssembly());
                var container = new CompositionContainer(catalog);
                container.ComposeParts(this, new ComputerBookService());
            }

            public static void Go()
            {
                Program p = new Program();
                p.Compose();

                p.Service.GetBookName();
            }
        }
    }

    internal static class MemberDiscover
    {
        public static void Go()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly a in assemblies)
            {
                Show(0, "Assembly: {0}", a);

                foreach (Type t in a.ExportedTypes)
                {
                    Show(1, "Type: {0}", t);

                    foreach (MemberInfo mi in t.GetTypeInfo().DeclaredMembers)
                    {
                        string typeName = string.Empty;
                        if (mi is Type) typeName = "(Nested) Type";
                        if (mi is FieldInfo) typeName = "FieldInfo";
                        if (mi is MethodInfo) typeName = "MethodInfo";
                        if (mi is ConstructorInfo) typeName = "ConstructoInfo";
                        if (mi is PropertyInfo) typeName = "PropertyInfo";
                        if (mi is EventInfo) typeName = "EventInfo";

                        Show(2, "{0}: {1}", typeName, mi);
                    }
                }
            }

            Console.ReadKey();
        }

        private static void Show(int indent, string format, params object[] args)
        {
            Console.WriteLine(new string(' ', 3 * indent) + format, args);
        }
    }

    /// <summary>
    /// 23.5.2
    /// </summary>
    internal static class ReflectionExtensions
    {
        // Helper extension method to simplify syntax to create a delegate
        public static TDelegate CreateDelegate<TDelegate>(
            this MethodInfo mi,
            object target = null)
        {
            return (TDelegate)(object)mi.CreateDelegate(typeof(TDelegate), target);
        }
    }

    /// <summary>
    /// 23.5.2
    /// </summary>
    internal static class Invoker
    {
        private sealed class SomeType
        {
            private int m_someField;
            public SomeType(ref int x)
            {
                x *= 2;
            }

            public override string ToString()
            {
                return m_someField.ToString();
            }
            public int SomeProp
            {
                get { return m_someField; }
                set
                {
                    if (value < 1) throw new ArgumentOutOfRangeException("value", "value must be > 0");
                    m_someField = value;
                }
            }
            public event EventHandler SomeEvent;
            private void NoCompilerWarnings()
            {
                SomeEvent.ToString();
            }
        }

        public static void Go()
        {
            Type t = typeof(SomeType);
            BindToMemberThenInvokeTheMember(t);
            Console.WriteLine();
            BindToMemberCreateDelegateToMemberThenInvokeTheMember(t);
            Console.WriteLine();
            UseDynamicToBindAndInvokeTheMember(t);
            Console.WriteLine();
        }

        private static void BindToMemberThenInvokeTheMember(Type t)
        {
            Console.WriteLine("BindToMemberThenInvokeTheMember");

            // Construct an instance
            Type ctorArgument = Type.GetType("System.Int32&");
            ConstructorInfo ctor = t.GetTypeInfo().DeclaredConstructors.First(c => c.GetParameters()[0].ParameterType == ctorArgument);
            object[] args = new object[] { 12 };
            Console.WriteLine("x before constructor called: " + args[0]);
            object obj = ctor.Invoke(args);
            Console.WriteLine("Type: " + obj.GetType().ToString());
            Console.WriteLine("x after constructor returns: " + args[0]);

            FieldInfo fi = obj.GetType().GetTypeInfo().GetDeclaredField("m_someField");
            fi.SetValue(obj, 33);
            Console.WriteLine("someField: " + fi.GetValue(obj));

            MethodInfo mi = obj.GetType().GetTypeInfo().GetDeclaredMethod("ToString");
            string s = (string)mi.Invoke(obj, null);
            Console.WriteLine("ToString: " + s);

            PropertyInfo pi = obj.GetType().GetTypeInfo().GetDeclaredProperty("SomeProp");
            try
            {
                pi.SetValue(obj, 0, null);
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException.GetType() != typeof(ArgumentOutOfRangeException)) throw;
                Console.WriteLine("Property set catch.");
            }
            pi.SetValue(obj, 2, null);
            Console.WriteLine("SomeProp: " + pi.GetValue(obj, null));

            EventInfo ei = obj.GetType().GetTypeInfo().GetDeclaredEvent("SomeEvent");
            EventHandler eh = new EventHandler(EventCallback);
            ei.AddEventHandler(obj, eh);
            ei.RemoveEventHandler(obj, eh);
        }

        private static void BindToMemberCreateDelegateToMemberThenInvokeTheMember(Type t)
        {
            Console.WriteLine("BindToMemberCreateDelegateToMemberThenInvokeTheMember");

            object[] args = new object[] { 12 };
            Console.WriteLine("x before constructor called: " + args[0]);
            object obj = Activator.CreateInstance(t, args);
            Console.WriteLine("Type: " + obj.GetType().ToString());
            Console.WriteLine("x after constructor returns: " + args[0]);

            // NOTE: You can't create a delegate to a field

            // Call a method
            MethodInfo mi = obj.GetType().GetTypeInfo().GetDeclaredMethod("ToString");
            var toString = mi.CreateDelegate<Func<String>>(obj);
            string s = toString();
            Console.WriteLine("ToString: " + s);

            // Read and write a property
            PropertyInfo pi = obj.GetType().GetTypeInfo().GetDeclaredProperty("SomeProp");
            var setSomeProp = pi.SetMethod.CreateDelegate<Action<int>>(obj);
            try
            {
                setSomeProp(0);
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("Property set catch.");
            }
            setSomeProp(2);
            var getSomeProp = pi.GetMethod.CreateDelegate<Func<int>>(obj);
            Console.WriteLine("SomeProp: " + getSomeProp());

            // Add and remove a delegate from the event
            EventInfo ei = obj.GetType().GetTypeInfo().GetDeclaredEvent("SomeEvent");
            var addSomeEvent = ei.AddMethod.CreateDelegate<Action<EventHandler>>(obj);
            addSomeEvent(EventCallback);
            var removeSomeEvent = ei.RemoveMethod.CreateDelegate<Action<EventHandler>>(obj);
            removeSomeEvent(EventCallback);
        }

        private static void UseDynamicToBindAndInvokeTheMember(Type t)
        {
            Console.WriteLine("UseDynamicToBindAndInvokeTheMember");

            // Construct an instance (You can't use dynamic to call a constructor)
            object[] args = new object[] { 12 };
            Console.WriteLine("x before constructor called: " + args[0]);
            dynamic obj = Activator.CreateInstance(t, args);
            Console.WriteLine("Type: " + obj.GetType().ToString());
            Console.WriteLine("x after constructor returns: " + args[0]);

            // Read and write to a field
            try
            {
                obj.m_someField = 5;
                int v = (int)obj.m_someField;
                Console.WriteLine("someField: " + v);
            }
            catch (RuntimeBinderException e)
            {
                // We get here because the field is private
                Console.WriteLine("Faild to access field: " + e.Message);
            }

            // Call a method
            string s = (string)obj.ToString();
            Console.WriteLine("ToString: " + s);

            // Read and write a property
            try
            {
                obj.SomeProp = 0;
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("Property set catch.");
            }
            obj.SomeProp = 2;
            int val = (int)obj.SomeProp;
            Console.WriteLine("SomeProp: " + val);

            // Add and remove a delegate from the event
            obj.SomeEvent += new EventHandler(EventCallback);
            obj.SomeEvent -= new EventHandler(EventCallback);
        }

        // Callback method added to the event
        private static void EventCallback(Object sender, EventArgs e) { }
    }

    internal sealed class ConvertMethodInfoToRuntimeMethodHandle
    {
        private const BindingFlags c_bf = 
            BindingFlags.FlattenHierarchy |
            BindingFlags.Instance | 
            BindingFlags.Static |
            BindingFlags.Public | 
            BindingFlags.NonPublic;

        public static void Go()
        {
            Show("Before doing anything");

            List<MethodBase> methodInfos = new List<MethodBase>();
            foreach (Type t in typeof(object).Assembly.GetExportedTypes())
            {
                if (t.IsGenericTypeDefinition) continue;

                MethodBase[] mb = t.GetMethods(c_bf);
                methodInfos.AddRange(mb);
            }

            Console.WriteLine("# of methods={0:N0}", methodInfos.Count);
            Show("After building cache of MethodInfo objects");

            List<RuntimeMethodHandle> methodHandles =
                methodInfos.ConvertAll<RuntimeMethodHandle>(mb => mb.MethodHandle);

            Show("Holding MethodInfo and RuntimeMethodHandle cache");
            GC.KeepAlive(methodInfos);

            methodInfos = null;
            Show("After freeing MethodInfo objects");

            methodInfos = methodHandles.ConvertAll<MethodBase>(
                rmh => MethodBase.GetMethodFromHandle(rmh));
            Show("Size of heap after re-creating MethodInfo objects");
            GC.KeepAlive(methodHandles);
            GC.KeepAlive(methodInfos);

            methodHandles = null;
            methodInfos = null;
            Show("After freeing MethodInfos and RuntimeMethodHandles");
        }

        private static void Show(string s)
        {
            Console.WriteLine("Heap size={0,12:N0} - {1}", 
                GC.GetTotalMemory(true), s);
        }
    }
}
