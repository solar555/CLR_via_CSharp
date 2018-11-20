using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

public static class RuntieSerialization
{
    static void Main(string[] args)
    {
        QuickStart.Go();

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

