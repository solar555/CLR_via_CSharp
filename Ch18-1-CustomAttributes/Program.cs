using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace Ch18_1_CustomAttributes
{

    public sealed class Program
    {
        static void Main(string[] args)
        {

        }
    }

    /// <summary>
    /// Ch18_4_CustomAttributes
    /// </summary>
    [Serializable]
    [DefaultMemberAttribute("Go")]
    [DebuggerDisplayAttribute("Richter", Name = "Jeff", Target = typeof(CustomAttributes))]
    internal sealed class CustomAttributes
    {
        [Conditional("Debug")]
        [Conditional("Release")]
        public void DoSomething() { }

        public CustomAttributes() { }

        [STAThread]
        static void Go(string[] args)
        {
            ShowAttributes(typeof(CustomAttributes));

            var members =
                from m in typeof(CustomAttributes).GetTypeInfo().DeclaredMembers.OfType<MethodBase>()
                where m.IsPublic
                select m;

            foreach (MemberInfo member in members)
            {
                ShowAttributes(member);
            }

            Console.ReadKey();
        }

        private static void ShowAttributes(MemberInfo attributeTarget)
        {
            var attributes = attributeTarget.GetCustomAttributes<Attribute>();

            Console.WriteLine("Attributes applied to {0}: {1}",
                attributeTarget.Name, (attributes.Count() == 0 ? "None" : String.Empty));

            foreach (Attribute attribute in attributes)
            {
                Console.WriteLine(" {0}", attribute.GetType().ToString());

                if (attribute is DefaultMemberAttribute)
                    Console.WriteLine(" MemberName={0}",
                        ((DefaultMemberAttribute)attribute).MemberName);
                if (attribute is ConditionalAttribute)
                    Console.WriteLine(" ConditionString={0}",
                        ((ConditionalAttribute)attribute).ConditionString);
                if (attribute is CLSCompliantAttribute)
                    Console.WriteLine(" IsCompliant={0}",
                        ((CLSCompliantAttribute)attribute).IsCompliant);

                DebuggerDisplayAttribute dda = attribute as DebuggerDisplayAttribute;
                if (dda != null)
                    Console.WriteLine(" Value={0}, Name={1}, Target={2}",
                        dda.Value, dda.Name, dda.Target);
            }
            Console.WriteLine();
        }
    }


    /// <summary>
    /// Ch18_5
    /// </summary>
    internal sealed class MatchingAttributes
    {
        [Flags]
        internal enum Accounts
        {
            Savings = 0x0001,
            Checking = 0x0002,
            Brokerage = 0x0004
        }

        [AttributeUsage(AttributeTargets.Class)]
        internal sealed class AccountsAttribute : Attribute
        {
            private Accounts m_accounts;

            public AccountsAttribute(Accounts accounts)
            {
                m_accounts = accounts;
            }

            public override bool Match(object obj)
            {
                if (obj == null) return false;

                if (this.GetType() != obj.GetType()) return false;

                AccountsAttribute other = (AccountsAttribute)obj;

                if ((other.m_accounts & m_accounts) != m_accounts)
                    return false;

                return true;
            }

            public override bool Equals(object obj)
            {
                if (obj == null) return false;

                if (this.GetType() != obj.GetType()) return false;

                AccountsAttribute other = (AccountsAttribute)obj;

                if (other.m_accounts != m_accounts)
                    return false;

                return true;
            }

            public override int GetHashCode()
            {
                return (int)m_accounts;
            }
        }

        [Accounts(Accounts.Savings)]
        internal sealed class ChildAccount { }

        [Accounts(Accounts.Savings | Accounts.Checking | Accounts.Brokerage)]
        internal sealed class AdultAccount { }

        public sealed class Program
        {
            public static void Go()
            {
                CanWriteCheck(new ChildAccount());
                CanWriteCheck(new AdultAccount());

                CanWriteCheck(new Program());
            }

            private static void CanWriteCheck(object obj)
            {
                Attribute checking = new AccountsAttribute(Accounts.Checking);
                Attribute validAccounts = Attribute.GetCustomAttribute(
                    obj.GetType(), typeof(AccountsAttribute), false);

                if ((validAccounts != null) && checking.Match(validAccounts))
                    Console.WriteLine("{0} types can write checks.", obj.GetType());
                else
                    Console.WriteLine("{0} types can NOT write checks.", obj.GetType());
            }
        }
    }

    /// <summary>
    /// Ch18_7
    /// </summary>
    /// 
    internal sealed class ConditionalAttributeExample
    {
        [Conditional("TEST")]
        [Conditional("VERIFY")]
        internal sealed class CondAttribute : Attribute
        {

        }

        [Cond]
        public sealed class Program
        {
            public static void Go()
            {
                Console.WriteLine("CondAttribute is {0}applied to Program type.",
                    Attribute.IsDefined(typeof(Program),
                    typeof(CondAttribute)) ? "" : "not ");
            }
        }
    }
}


