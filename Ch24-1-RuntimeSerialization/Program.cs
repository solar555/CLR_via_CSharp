using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Formatters.Soap;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Ch24_1_RuntimeSerialization
{

    public static class RuntieSerialization
    {
        static void Main(string[] args)
        {
            ISerializableVersioning.Go();

            Console.ReadLine();
        }
    }

    internal static class QuickStart
    {
        public static void Go()
        {
            var objectGraph = new List<string> { "Jeff", "Kristin", "Aidan", "Grant" };
            Stream stream = SerializeToMemory(objectGraph);

            stream.Position = 0;
            objectGraph = null;

            objectGraph = (List<string>)DeserializeFromMemory(stream);
            foreach (var s in objectGraph) Console.WriteLine(s);

            var clone = DeepClone(objectGraph);
            MultipleGraphs();
            OptInSerialization();
        }

        private static MemoryStream SerializeToMemory(object objectGraph)
        {
            MemoryStream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, objectGraph);
            return stream;
        }

        private static object DeserializeFromMemory(Stream stream)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            return formatter.Deserialize(stream);
        }

        private static Object DeepClone(object original)
        {
            // Construct a temporary memory stream
            using (MemoryStream stream = new MemoryStream())
            {
                // Construct a serialization formatter that daes all the hard work
                BinaryFormatter formatter = new BinaryFormatter();

                // This line is explained in this chapter's "Streaming Contexts" section
                formatter.Context = new StreamingContext(StreamingContextStates.Clone);

                // Serialize the object graph into the memory stream
                formatter.Serialize(stream, original);

                // Seek back to the start of the memory stream before deserializing
                stream.Position = 0;

                // Deserialize the graph into a new set of objects and
                // return the root of the graph (deep copy) to the caller
                return formatter.Deserialize(stream);
            }
        }

        [Serializable]
        private sealed class Customer { /* ... */}
        [Serializable]
        private sealed class Order { /* ... */}

        private static List<Customer> s_customers = new List<Customer>();
        private static List<Order> s_pendingOrders = new List<Order>();
        private static List<Order> s_processedOrders = new List<Order>();

        private static void MultipleGraphs()
        {
            using (var stream = new MemoryStream())
            {
                SaveApplicationState(stream);
                stream.Position = 0;
                RestoreApplicationState(stream);
            }
        }

        private static void SaveApplicationState(Stream stream)
        {
            // Construct a serialization formatter that does all the hard work
            BinaryFormatter formatter = new BinaryFormatter();

            // Serialize our application's entire state
            formatter.Serialize(stream, s_customers);
            formatter.Serialize(stream, s_pendingOrders);
            formatter.Serialize(stream, s_processedOrders);
        }

        private static void RestoreApplicationState(Stream stream)
        {
            // Construct a serialization formatter that does all the hard work
            BinaryFormatter formatter = new BinaryFormatter();

            // Deserialize our application's entire state (same order as serialized)
            s_customers = (List<Customer>)formatter.Deserialize(stream);
            s_pendingOrders = (List<Order>)formatter.Deserialize(stream);
            s_processedOrders = (List<Order>)formatter.Deserialize(stream);
        }

        // Not marked [Serializable]
        private struct Point { public int x, y; }

        private static void OptInSerialization()
        {
            Point pt = new Point { x = 1, y = 2 };
            using (var stream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(stream, pt);// throws SerializationException
            }
        }
    }

    /// <summary>
    /// 24.3
    /// </summary>
    internal static class UsingNonSerializedFields
    {
        [Serializable]
        internal class Circle /*: IDeserializationCallback */
        {
            private Double m_radius;

            [NonSerialized]
            private Double m_area;

            public Circle(Double radius)
            {
                m_radius = radius;
                m_area = Math.PI * m_radius * m_radius;
            }

            [OnDeserialized]
            private void OnDeserialized(StreamingContext context)
            {
                m_area = Math.PI * m_radius * m_radius;
            }

            [OnDeserializing]
            private void OnDeserializing(StreamingContext context)
            {
                m_area = Math.PI * m_radius * m_radius;
            }
            // void IDeserializationCallback.OnDeserialization(Object sender) { m_area = Math.PI * m_radius * m_radius; }
        }

        [Serializable]
        private class Outer
        {
            public Inner inner;
            [OnSerializing]
            private void OnSerializing(StreamingContext context) { }
            [OnSerialized]
            private void OnSerialized(StreamingContext context) { }
            [OnDeserializing]
            private void OnDeserializing(StreamingContext context) { }
            [OnDeserialized]
            private void OnDeserialized(StreamingContext context) { }
        }

        [Serializable]
        private class Inner
        {
            [OnSerializing]
            private void OnSerializing(StreamingContext context) { }
            [OnSerialized]
            private void OnSerialized(StreamingContext context) { }
            [OnDeserializing]
            private void OnDeserializing(StreamingContext context) { }
            [OnDeserialized]
            private void OnDeserialized(StreamingContext context) { }
        }

        public static void Go()
        {
            using (var stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                var outer = new Outer { inner = new Inner() };
                // Circle[] circles = new[] { new Circle(10), new Circle(20) };
                formatter.Serialize(stream, outer);
                stream.Position = 0;
                outer = (Outer)formatter.Deserialize(stream);
            }
        }
    }

    internal static class OptionalField
    {
        [Serializable]
        public class Foo
        {
            /*[OptionalField] */
            public string name = "jeff";
        }

        public static void Go()
        {
            const string filename = @"temp.dat";
            var formatter = new SoapFormatter();

            // Serialize
            using (var stream = File.Create(filename))
            {
                formatter.Serialize(stream, new Foo());
            }

            // Deserialize
            using (var stream = File.Open(filename, FileMode.Open))
            {
                Foo f = (Foo)formatter.Deserialize(stream);
            }
            File.Delete(filename);
        }
    }

    internal static class ISerializableVersioning
    {
        public static void Go()
        {
            using (var stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, new Derived());
                stream.Position = 0;
                Derived d = (Derived)formatter.Deserialize(stream);
                Console.WriteLine(d);
            }
        }

        [Serializable]
        private class Base
        {
            protected string m_name = "Jeff";
            protected string Name { get { return m_name; } }
            public Base() { /* Make the type instantiable*/}
        }

        [Serializable]
        private class Derived : Base, ISerializable
        {
            new private string m_name = "Richter";
            public Derived() { /* Make the type instantiable*/}

            // If this constructor didn't exist, we'd get a SerializationException
            // This constructor should be protected if this class were not sealed
            [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
            private Derived(SerializationInfo info, StreamingContext context)
            {
                // Get the set of serializable members for our class and base classes
                Type baseType = this.GetType().BaseType;
                MemberInfo[] mi = FormatterServices.GetSerializableMembers(baseType, context);

                // Deserialize the base class's fields from the info object
                for (int i = 0; i < mi.Length; i++)
                {
                    // Get the field and set it to the deserialized value
                    FieldInfo fi = (FieldInfo)mi[i];
                    fi.SetValue(this, info.GetValue(baseType.FullName + "+" + fi.Name, fi.FieldType));
                }

                // Deserialize the values that were serialized for this class
                m_name = info.GetString("Name");
            }

            [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
            public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                // Serialize the desired values for this class
                info.AddValue("Name", m_name);

                // Get the set of serializable members for our class and base classes
                Type baseType = this.GetType().BaseType;
                MemberInfo[] mi = FormatterServices.GetSerializableMembers(baseType, context);

                // Serialize the base class's fields to the info object
                for (int i = 0; i < mi.Length; i++)
                {
                    // Prefix the field name with the fullname of the base type
                    info.AddValue(baseType.FullName + "+" + mi[i].Name, ((FieldInfo)mi[i]).GetValue(this));
                }
            }

            public override string ToString()
            {
                return string.Format("Base Name={0}, Derived Name={1}", base.Name, m_name);
            }
        }
    }
}