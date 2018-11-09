using System;
using System.Windows.Forms;
using System.IO;

/// 1. Don't create 'Windows Program' by 'Visual Studio',
/// 2. Write code using a text editor, and generate the program using 'csc Program.cs',
/// 3. Execute Program.exe.
namespace Ch17_1_Delegates
{
    internal delegate void FeedBack(int value);

    class Program
    {
        static void Main(string[] args)
        {
            StaticDelegateDemo();
            InstanceDelegateDemo();
            ChainDelegateDemo1(new Program());
            ChainDelegateDemo2(new Program());

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
            Program p = new Program();
            Counter(1, 3, new FeedBack(p.FeedbackToFile));
            Console.WriteLine();
        }

        private static void ChainDelegateDemo1(Program p)
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

        private static void ChainDelegateDemo2(Program p)
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
            MessageBox.Show("Item=" + value);
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
}
