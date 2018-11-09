using System;
//using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

/// 1. Don't create 'Windows Program' by 'Visual Studio',
/// 2. Write code using a text editor, and generate the program using 'csc Program.cs',
/// 3. Execute Program.exe.
namespace Ch17_Delegates
{
    public sealed class Program
    {
        static void Main(string[] args)
        {
            GetInvocationList.Run();

            Console.ReadKey();
        }
    }

    /// <summary>
    /// Ch17_1
    /// </summary>
    internal sealed class DelegateIntro
    {
        internal delegate void FeedBack(int value);

        public static void Run()
        {
            StaticDelegateDemo();
            InstanceDelegateDemo();
            ChainDelegateDemo1(new DelegateIntro());
            ChainDelegateDemo2(new DelegateIntro());

            Console.ReadKey();
        }

        private static void StaticDelegateDemo()
        {
            Console.WriteLine("----- Static Delegate Demo -----");
            Counter(1, 3, null);
            Counter(1, 3, new FeedBack(FeedbackToConsole));
            Counter(1, 3, new FeedBack(FeedbackToMsgBox));
            Console.WriteLine();
        }

        private static void InstanceDelegateDemo()
        {
            Console.WriteLine("----- Instance Delegate Demo -----");
            DelegateIntro p = new DelegateIntro();
            Counter(1, 3, new FeedBack(p.FeedbackToFile));
            Console.WriteLine();
        }

        private static void ChainDelegateDemo1(DelegateIntro p)
        {
            Console.WriteLine("----- Chain Delegate Demo 1 -----");
            FeedBack fb1 = new FeedBack(FeedbackToConsole);
            FeedBack fb2 = new FeedBack(FeedbackToMsgBox);
            FeedBack fb3 = new FeedBack(p.FeedbackToFile);

            FeedBack fbChain = null;
            fbChain = (FeedBack)Delegate.Combine(fbChain, fb1);
            fbChain = (FeedBack)Delegate.Combine(fbChain, fb2);
            fbChain = (FeedBack)Delegate.Combine(fbChain, fb3);
            Counter(1, 2, fbChain);

            Console.WriteLine();
            fbChain = (FeedBack)Delegate.Remove(fbChain, new FeedBack(FeedbackToMsgBox));
            Counter(1, 2, fbChain);
        }

        private static void ChainDelegateDemo2(DelegateIntro p)
        {
            Console.WriteLine("----- Chain Delegate Demo 2 -----");
            FeedBack fb1 = new FeedBack(FeedbackToConsole);
            FeedBack fb2 = new FeedBack(FeedbackToMsgBox);
            FeedBack fb3 = new FeedBack(p.FeedbackToFile);

            FeedBack fbChain = null;
            fbChain += fb1;
            fbChain += fb2;
            fbChain += fb3;
            Counter(1, 2, fbChain);

            Console.WriteLine();
            fbChain -= new FeedBack(FeedbackToMsgBox);
            Counter(1, 2, fbChain);
        }

        private static void FeedbackToConsole(int value)
        {
            Console.WriteLine("Item=" + value);
        }

        private static void FeedbackToMsgBox(int value)
        {
            // Uncomment the following code if you wanna run it.
            //System.Windows.Forms.MessageBox.Show("Item=" + value);
        }

        private void FeedbackToFile(int value)
        {
            using (StreamWriter sw = new StreamWriter("Status", true))
            {
                sw.WriteLine("Item=" + value);
            }
        }

        private static void Counter(int from, int to, FeedBack fb)
        {
            for (int val = from; val <= to; val++)
            {
                fb?.Invoke(val);
            }
        }
    }

    /// <summary>
    /// Ch17_5_2
    /// </summary>
    internal sealed class GetInvocationList
    {
        internal sealed class Light
        {
            public String SwitchPosition()
            {
                return "The light is off";
            }
        }

        internal sealed class Fan
        {
            public string Speed()
            {
                throw new InvalidOperationException("The fan broke due to overheating");
            }
        }

        internal sealed class Speaker
        {
            public string Volume()
            {
                return "The volume is loud";
            }
        }

        private delegate string GetStatus();

        public static void Run()
        {
            GetStatus getStatus = null;

            getStatus += new GetStatus(new Light().SwitchPosition);
            getStatus += new GetStatus(new Fan().Speed);
            getStatus += new GetStatus(new Speaker().Volume);

            Console.WriteLine(GetComponentStatusReport(getStatus));
        }

        private static string GetComponentStatusReport(GetStatus status)
        {
            if (status == null) return null;

            StringBuilder report = new StringBuilder();

            Delegate[] arrayOfDelegates = status.GetInvocationList();

            foreach (GetStatus getStatus in arrayOfDelegates)
            {
                try
                {
                    report.AppendFormat("{0}{1}{1}", getStatus(),
                        Environment.NewLine);
                }
                catch (InvalidOperationException e)
                {
                    object component = getStatus.Target;
                    report.AppendFormat(
                        "Failed to get status from {1}{2}{0} Error:{3}{0}{0}",
                        Environment.NewLine,
                        ((component == null) ? "" : component.GetType() + "."),
                        getStatus.Method.Name,
                        e.Message);
                }
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// Ch17_7
    /// </summary>
    internal sealed class AnonymousMethods
    {
        internal sealed class AClass
        {
            public static void UsingLocalVariablesInTheCallbackCode(int numToDo)
            {
                int[] squares = new int[numToDo];
                AutoResetEvent done = new AutoResetEvent(false);

                for (int n = 0; n < squares.Length; n++)
                {
                    ThreadPool.QueueUserWorkItem(
                        obj => {
                            int num = (int)obj;
                            squares[num] = num * num;

                            if (Interlocked.Decrement(ref numToDo) == 0)
                                done.Set();
                        }, n);
                }

                done.WaitOne();

                for (int n = 0; n < squares.Length; n++)
                {
                    Console.WriteLine("Index {0}, squares={1}", n, squares[n]);
                }
            }
        }
    }

    /// <summary>
    /// Ch17_8
    /// </summary>
    internal sealed class DelegateReflection
    {
        internal delegate object TwoInt32s(int n1, int n2);
        internal delegate object OneString(string s1);

        public static void Run(string[] args)
        {
            if (args.Length < 2)
            {
                string usage =
                    @"Usage:" +
                    "{0} delType methodName [Arg1] [Arg2]" +
                    "{0}   where delType must be TwoInt32s or OneString" +
                    "{0}   if delType is TwoInt32s, methodName must be Add or Subtract" +
                    "{0}   if delType is OneString, methodName must be NumChars or Reverse" +
                    "{0}" +
                    "{0}Examples:" +
                    "{0}   {1} TwoInt32s Add 123 321" +
                    "{0}   {1} TwoInt32s Subtract 123 321" +
                    "{0}   {1} OneString NumChars \"Hello there\"" +
                    "{0}   {1} OneString Reverse \"Hello there\"";
                Console.WriteLine(usage, Environment.NewLine);
                return;
            }

            Type delType = Type.GetType(args[0]);
            if(delType == null)
            {
                Console.WriteLine("Invalid delType argument: " + args[0]);
                return;
            }

            Delegate d;
            try
            {
                MethodInfo mi =
                    typeof(DelegateReflection).GetTypeInfo().GetDeclaredMethod(args[1]);

                d = mi.CreateDelegate(delType);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Invalid methodName argument: " + args[1]);
                return;
            }

            object[] callbackArgs = new object[args.Length - 2];

            if(d.GetType() == typeof(TwoInt32s))
            {
                try
                {
                    for (int a = 2; a < args.Length; a++)
                        callbackArgs[a - 2] = int.Parse(args[a]);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Parameters must be integers.");
                    return;
                }
            }

            if (d.GetType() == typeof(OneString))
            {
                Array.Copy(args, 2, callbackArgs, 0, callbackArgs.Length);
            }

            try
            {
                object result = d.DynamicInvoke(callbackArgs);
                Console.WriteLine("Result = " + result);
            }
            catch (TargetParameterCountException)
            {
                Console.WriteLine("Incorrect number of parameters specified.");
            }
        }

        private static object Add(int n1, int n2)
        {
            return n1 + n2;
        }

        private static object Subtract(int n1, int n2)
        {
            return n1 - n2;
        }

        private static object NumChars(string s1)
        {
            return s1.Length;
        }

        private static object Reverse(string s1)
        {
            return new string(s1.Reverse().ToArray());
        }
    }
}
