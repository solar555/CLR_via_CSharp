using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Ch25_1_InteroperatingWithWinRTComponents
{
    public class Program
    {
        static void Main(string[] args)
        {
        }
    }
}

// The namesp
namespace Wintellect.WinRTComponents
{
    // [Flags] // Must not be present if enum is int; required if enum is uint
    public enum WinRTEnum :int
    {
        // Enums must be backed by int or uint
        None,
        NotNone
    }

    // Structures can only contain core data types, String, & other structures
    // No constructors or methods are allowed
    public struct WinRTStruct
    {
        public int ANumber;
        public string AString;
        public WinRTEnum AEnum; // Really just a 32-bit integer
    }

    // Delegates must have WinRT-compatible types in the signature (no BeginInvoke/EndInvoke)
    public delegate string WinRTDelegate(int x);

    // Interfaces can have methods, properties, & events but cannot be generic.
    public interface IWinRTInterface
    {
        // Nullable<T> marshals as IReference<T>
        int? InterfaceProperty { get; set; }
    }

    // Members without a [Version(#)] attribute default to the class's
    // version (1) and are part of the same underlying COM interface
    // produced by WinMDExp.exe.
    //[Version(1)]
    // Class must be derived from Object, sealed, not generic,
    // implement only WinRT interfaces, & public members must be WinRT types
    public sealed class WinRTClass : IWinRTInterface
    {
        // Public fields are not allowed

        #region Class can expose static methods, properties, and events
        public static string StaticMethod(string s) { return "Returning " + s; }
        public static WinRTStruct StaticProperty { get; set; }

        // In JavaScript 'out' parameters are returned as objects with each
        // parameter becoming a property along with the return value
        public static string OutParameters(out WinRTStruct x, out int year)
        {
            x = new WinRTStruct { AEnum = WinRTEnum.NotNone, ANumber = 333, AString = "Jeff" };
            year = DateTimeOffset.Now.Year;
            return "Grant";
        }
        #endregion

        // Constructor can take arguments but not out/ref arguments
        public WinRTClass(int? number) { InterfaceProperty = number; }

        public int? InterfaceProperty { get; set; }

        // Only ToString is allowed to be overridden
        public override string ToString()
        {
            return string.Format("InterfaceProperty={0}",
                InterfaceProperty.HasValue ? InterfaceProperty.Value.ToString() : "(not set)");
        }

        public void ThrowingMethod()
        {
            throw new InvalidOperationException("My exception message");

            // To throw a specific HRESULT, use COMException instead
            //const Int32 COR_E_INVALIDOPERATION = unchecked((Int32)0x80131509);
            //throw new COMException("Invalid Operation", COR_E_INVALIDOPERATION);
        }

        #region Arrays are passed, returned OR filled; never a combination
        public int PassArray([ReadOnlyArray] /* [In] implied */ int[] data)
        {
            // NOTE:Modified array contents MAY not be marshaled out; do not modify the array
            return data.Sum();
        }

        public Int32 FillArray([WriteOnlyArray] /* [Out] implied */ int[] data)
        {
            // NOTE: Original array contents MAY not be marshaled in;
            // write to the array before reading from it 
            for (int n = 0; n < data.Length; n++) data[n] = n;
            return data.Length;
        }

        public int[] ReturnArray()
        {
            // Array is marshaled out upon return
            return new int[] { 1, 2, 3 };
        }
        #endregion

        // Collections are passed by reference
        public void PassAndModifyCollection(IDictionary<string, object> collection)
        {
            collection["Key2"] = "Value2"; // Modifies collection in place via interop
        }

        #region Method overloading
        // Overloads with same # of parameters are considered identical to JavaScript
        public void SomeMethod(int x) { }

        //[Windows.Foundation.Metadata.DefaultOverload]
        public void SomeMethod(string s) { }
        #endregion

        #region Automatically implemented event
        public event WinRTDelegate AutoEvent;

        public string RaiseAutoEvent(int number)
        {
            WinRTDelegate d = AutoEvent;
            return (d == null) ? "No callbacks registered" : d(number);
        }
        #endregion

        #region Manually implemented event
        // Private field that keeps track of the event's registered delegates
        private EventRegistrationTokenTable<WinRTDelegate> m_manualEvent = null;

        // Manual implementation of the event's add and remove methods
        public event WinRTDelegate ManualEvent
        {
            add
            {
                // Get the existing table, or creates a new one if the table is not yet initialized
                return EventRegistrationTokenTable<WinRTDelegate>
                    .GetOrCreateEventRegistrationTokenTable(ref m_manualEvent).AddEventHandler(value);
            }
            remove
            {
                EventRegistrationTokenTable<WinRTDelegate>
                    .GetOrCreateEventRegistrationTokenTable(ref m_manualEvent).RemoveEventHandler(value);
            }
        }

        public string RaiseManualEvent(int number)
        {
            WinRTDelegate d = EventRegistrationTokenTable<WinRTDelegate>
                .GetOrCreateEventRegistrationTokenTable(ref m_manualEvent).InvocationList;
            return (d == null) ? "No callbacks registered" : d(number);
        }
        #endregion

        #region Asynchronous methods
        // Async methods MUST return IAsync[Action|Operation](WithProgress)
        // NOTE: Other languages see the DataTimeOffset as Windows.Foundation.DateTime
        public IAsyncOperationWithProgress<DateTimeOffset, int> DoSomethingAsync()
        {
            // Use the System.Runtime.InteropServices.WindowsRuntime.AsyncInfo's Run methods to 
            // invoke a private method written entirely in managed code
            return AsyncInfo.Run<DateTimeOffset, int>(DoSimethingAsyncInternal);
        }

        // Implement the async operation via a private method using normal .NET technologies
        private async Task<DateTimeOffset> DoSomethingAsyncInternal(
            CancellationToken ct, IProgress<int> progress)
        {
            for (int x = 0; x < 10; x++)
            {
                // This code supports cancellation and progress reporting
                ct.ThrowIfCancellationRequested();
                if (progress != null) progress.Report(x * 10);
                await Task.Delay(1000); // Simulate doing something asynchronously
            }
            return DateTimeOffset.Now; // Ultimate return value
        }

        public IAsyncOperation<DateTimeOffset> DoSomethingAsync2()
        {
            // If you don't need cancellation & progress, use
            // System.WindowsRuntimeSystemExtensions' AsAsync[Action|Operation] Task
            // extension methods (these call AsyncInfo.Run internally)
            return DoSomethingAsyncInternal(default(CancellationToken), null).AsAsyncOperation();
        }
        #endregion

        // After you ship a version, mark new members with a (Version(#)] attribute
        // so that WinMdExp.exe puts the new members in a different underlying COM
        // interface. This is required since COM interfaces are supposed to be immutable.
        //[Version(2)]
        public void NewMethodAddedInV2() { }
    }
}
