using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ch28_1_IOOps
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
        }

        private static async Task<string> AwaitWebClient(Uri uri)
        {
            // The System.Net.WebClient class supports the Event-based Asynchronous Pattern
            var wc = new System.Net.WebClient();

            // Create the TaskCompletionSource and its underlying Task object
            var tcs = new TaskCompletionSource<string>();

            // When a string completes downloading, the WebClient object raises the
            // DownloadStringCompleted event which completes the TaskCompletionSource
            wc.DownloadStringCompleted += (s, e) =>
            {
                if (e.Cancelled) tcs.SetCanceled();
                else if (e.Error != null) tcs.SetException(e.Error);
                else tcs.SetResult(e.Result);
            };

            // Start the asynchronous operation
            wc.DownloadStringAsync(uri);

            // Now, we can the TaskCompletionSource's Task and process the result as usual
            string result = await tcs.Task;
            // Process the resulting string (if desired)...

            return result;
        }
    }

    internal static class PipeDemo
    {
        public static async Task Go2()
        {
            var tasks = new Task[]
            {
                Task.Delay(10000),
                Task.Delay(1000),
                Task.Delay(5000),
                Task.Delay(3000),
            };
            //foreach (var t in WhenE)
            {

            }
        }

        public static async Task Go()
        {
            // Start the server which returns immediately since
            // it asynchronously waits for client requests
            
        }

        public static IEnumerable<Task<Task<TResult>>> WhenEach<TResult>(params Task<TResult>[] tasks)
        {
            // Create a new TaskCompletionSource for each task
            var taskCompletions = new TaskCompletionSource<Task<TResult>>[tasks.Length];

            int next = -1; // Identifies the next TaskCompletionSource to complete
            Action<Task<TResult>> taskCompletionCallback = t => taskCompletions[Interlocked.Increment(ref next)].SetResult(t);

            // Create all the TaskCompletionSource objects and tell each task to
            // complete the next one as each task completes
            for (int n = 0; n < tasks.Length; n++)
            {
                taskCompletions[n] = new TaskCompletionSource<Task<TResult>>();
                tasks[n].ContinueWith(taskCompletionCallback, TaskContinuationOptions.ExecuteSynchronously);
            }
            // Return each of the TaskCompletionSource's Tasks in turn.
            // The Result property represents the original task that completed.
            for (int n = 0; n < tasks.Length; n++) yield return taskCompletions[n].Task;
        }

        public static IEnumerable<Task<Task>> WhenEach(params Task[] tasks)
        {
            // Create a new TaskCompletionSource for each task
            var taskCompletions = new TaskCompletionSource<Task>[tasks.Length];

            int next = -1; // Identifies the next TaskCompletionSource to complete
            // As each task completes, this callback completes the next TaskCompletionSource
            Action<Task> taskCompletionCallback = t => taskCompletions[Interlocked.Increment(ref next)].SetResult(t);

            // Create all the TaskCompletionSource objects and tell each task to
            // complete the next one as each task completes
            for (int n = 0; n < tasks.Length; n++)
            {
                taskCompletions[n] = new TaskCompletionSource<Task>();
                tasks[n].ContinueWith(taskCompletionCallback, TaskContinuationOptions.ExecuteSynchronously);
            }
            // Return each of the TaskCompletionSource's Tasks in turn.
            // The Result property represents the original task that completed.
            for (int n = 0; n < tasks.Length; n++) yield return taskCompletions[n].Task;
        }

        private static async void StartServer()
        {
            while (true)
            {
                var pipe = new NamedPipeServerStream("PipeName", PipeDirection.InOut, -1,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);

                // Asynchronously accept a client connection
                // NOTE: NamedPipServerStream uses the old Asynchronous Programming Model (APM)
                // I convert the old APM to the new Task model via TaskFactory's FromAsync method
                await Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null);

                // Start servicing the client which returns immediately since it is asynchronous
                ServiceClientRequestAsync(pipe);
            }
        }

        // This field records the timestamp of the most recent client's request
        private static DateTime s_lastClientRequest = DateTime.MinValue;

        // The SemaphoreSlim protects enforces thread-safe access to s_lastClientRequest
        private static readonly SemaphoreSlim s_lock = new SemaphoreSlim(1);

        private static async void ServiceClientRequestAsync(NamedPipeServerStream pipe)
        {
            using (pipe)
            {
                // Asynchronously read a request from the client
                Byte[] data = new byte[1000];
                int bytesRead = await pipe.ReadAsync(data, 0, data.Length);

                // Get the timestamp of this client's request
                DateTime now = DateTime.Now;

                // We want to save the timestamp of the most-recent client request.
                // Since many clients can run concurrently, this has to be thread-safe.
                await s_lock.WaitAsync(); // Asynchronously request exclusive access

                // When we get here, we know no other thread is touching s_lastClientRequest
                if (s_lastClientRequest < now) s_lastClientRequest = now;
                s_lock.Release();  // Relinquish access so other clients can update

                // My sample server just changes all the characters to uppercase.
                // You can replace this code with any compute-bound operation.
                data = Encoding.UTF8.GetBytes(
                    Encoding.UTF8.GetString(data, 0, bytesRead).ToUpper().ToCharArray());

                // Asynchronously send the response back to the client
                await pipe.WriteAsync(data, 0, data.Length);
                // Close the pipe to the client
            }
        }

        private static async Task<string> IssueClientRequestAsync(string serverName, string message)
        {
            using (var pipe = new NamedPipeClientStream(serverName, "PipeName", PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough))
            {
                pipe.Connect(); // Must Connect before setting ReadMode
                pipe.ReadMode = PipeTransmissionMode.Message;

                // Asynchronously send data to the server
                Byte[] request = Encoding.UTF8.GetBytes(message);
                await pipe.WriteAsync(request, 0, request.Length);

                // Asynchronously read the server's response
                byte[] response = new byte[1000];
                int bytesRead = await pipe.ReadAsync(response, 0, response.Length);
                return Encoding.UTF8.GetString(response, 0, bytesRead);
                // Close the pipe
            }
        }
    }
}
