using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ch20_1_ExceptionHandling
{
    internal class Program
    {
        static void Main(string[] args)
        {
        }
    }

    internal static class Mechanics
    {
        public static void SomeMethod()
        {
            try
            {

            }
            catch (InvalidOperationException)
            {

            }
            catch (IOException)
            {

            }
            catch (Exception)
            {
                throw;
            }
            catch
            {
                throw;
            }
            finally
            {

            }
        }

        private static void ReadData(string pathname)
        {
            FileStream fs = null;
            try
            {
                fs = new FileStream(pathname, FileMode.Open);
            }
            catch (IOException)
            {

            }
            finally
            {
                if (fs != null) fs.Close();
            }
        }
    }

    internal static class GenericException
    {
        [Serializable]
        public sealed class Exception<TExceptionArgs> : Exception, ISerializable where TExceptionArgs : ExceptionArgs
        {
            private const string c_args = "Args";

            private readonly TExceptionArgs m_args;

            public TExceptionArgs Args { get { return m_args; } }

            public Exception(string message = null, Exception innerException = null)
                : this(null, message, innerException)
            {
            }

            public Exception(TExceptionArgs args, string message = null, Exception innerException = null) : base(message, innerException)
            { m_args = args; }

            [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
            private Exception(SerializationInfo info, StreamingContext context) : base(info, context)
            {
                m_args = (TExceptionArgs)info.GetValue(c_args, typeof(TExceptionArgs));
            }

            [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue(c_args, m_args);
                base.GetObjectData(info, context);
            }

            public override string Message
            {
                get
                {
                    string baseMsg = base.Message;
                    return (m_args == null) ? baseMsg : baseMsg + " (" + m_args.Message + ")";
                }
            }

            public override bool Equals(object obj)
            {
                Exception<TExceptionArgs> other = obj as Exception<TExceptionArgs>;
                if (other == null) return false;
                return object.Equals(m_args, other.m_args) && base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        [Serializable]
        public abstract class ExceptionArgs
        {
            public virtual string Message { get { return string.Empty; } }
        }

        [Serializable]
        public sealed class DiskFullExceptionArgs: ExceptionArgs
        {
            private readonly string m_diskpath;

            public DiskFullExceptionArgs(string diskpath)
            {
                m_diskpath = diskpath;
            }

            public string DiskPath { get { return m_diskpath; } }

            public override string Message
            {
                get
                {
                    return (m_diskpath == null) ? base.Message : "DiskPath=" + m_diskpath;
                }
            }
        }

        public sealed class Program
        {
            public static void TestException()
            {
                try
                {
                    throw new Exception<DiskFullExceptionArgs>(
                        new DiskFullExceptionArgs(@"C:\"), "The disk is full");
                }
                catch (Exception<DiskFullExceptionArgs> e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }

    internal static class OneStatementDemo
    {

        private static object s_myLockObject = new object();
        private static void MonitorWithStateCorruption()
        {
            Monitor.Enter(s_myLockObject);

            try
            {

            }
            finally
            {
                Monitor.Exit(s_myLockObject);
            }
        }

        private static void MonitorWithoutStateCorruption()
        {
            bool lockTaken = false;
            try
            {
                Monitor.Enter(s_myLockObject, ref lockTaken);
            }
            finally
            {
                if (lockTaken) Monitor.Exit(s_myLockObject);
            }
        }
    }

    internal sealed class SomeType
    {
        private void SomeMethod()
        {
            using (FileStream fs = new FileStream(@"C:\Data.bin", FileMode.Open))
            {
                Console.WriteLine(100 / fs.ReadByte());
            }
        }

        public void SerializeObjectGraph(FileStream fs, IFormatter formatter, object rootObj)
        {
            Int64 beforeSerialization = fs.Position;

            try
            {
                formatter.Serialize(fs, rootObj);
            }
            catch
            {
                fs.Position = beforeSerialization;

                fs.SetLength(fs.Position);

                throw;
            }
        }
    }

    internal sealed class PhoneBook
    {
        private string m_pathname;

        private static void Reflection(object o)
        {
            try
            {
                var mi = o.GetType().GetMethod("DoSomething");
                mi.Invoke(o, null);
            }
            catch (System.Reflection.TargetInvocationException e)
            {
                throw e.InnerException;
                throw;
            }
        }
    }
}
