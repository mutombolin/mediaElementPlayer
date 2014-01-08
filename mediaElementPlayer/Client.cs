using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;
using System.Diagnostics;
using System.Net;

namespace mediaElementPlayer
{
    public class Client
    {
        public Client()
        { 
            
        }

        public void ConnectToServer()
        {
            Thread.Sleep(1000);
            var startNow = Stopwatch.StartNew();
            var calls = 100;
            var result = System.Threading.Tasks.Parallel.For(0, calls, CallServer);
            while (!result.IsCompleted)
            {
                Thread.Sleep(100);
            }
            startNow.Stop();

            System.Diagnostics.Debug.WriteLine(string.Format("Client finished {0}x1sec calls in {1} sec", calls, startNow.Elapsed.Seconds));
        }

        private void CallServer(int i)
        {
            var webRequest = WebRequest.Create("http://localhost:7896/");
            webRequest.Headers["thread"] = i.ToString();
            using (var webResponse = webRequest.GetResponse())
            { 
                System.Diagnostics.Debug.WriteLine(string.Format("Client: {0}", webRequest.Headers["thread"]));
            }
        }

        // This is a test line.
    }
}
