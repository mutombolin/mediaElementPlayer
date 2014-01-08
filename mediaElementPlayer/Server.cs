using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Diagnostics;
using System.Threading;

using System.IO;
using System.Threading.Tasks;

namespace mediaElementPlayer
{ 
    public class Server
    {
        private HttpListener _listener;
        private string _filename = string.Empty;
        private bool _isStop = false;
        private bool _isStarted = false;
        private Thread _requestThread;
        private bool _isStopping = false;
        private int _numberOfRequest;

        private ManualResetEvent resetEvent;

        private MemoryStream _ms;

        public Server()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:7896/");
            _listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            resetEvent = new ManualResetEvent(false);
        }

        private void RequestThread()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
                    context.AsyncWaitHandle.WaitOne();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("RequestThread - Ex = {0}", ex));
                }
            }
        }

        private void ListenerCallback(IAsyncResult ar)
        {
            lock (this)
            {
                if (_isStopping)
                    return;

                resetEvent.Reset();
                _numberOfRequest++;
            }

            var listener = ar.AsyncState as HttpListener;

//            System.Diagnostics.Debug.WriteLine(string.Format("ListenerCallback numberOfRequest = {0}", _numberOfRequest));

            _isStarted = true;

            var context = listener.EndGetContext(ar);

            System.Threading.Tasks.Task.Factory.StartNew((ctx) =>
            {
                _isStop = false;
                WriteFile((HttpListenerContext)ctx);
            }, context, System.Threading.Tasks.TaskCreationOptions.LongRunning);

            lock (this)
            {
                if (--_numberOfRequest == 0)
                    resetEvent.Set();
            }
        }

        public void Stop()
        {
            lock (this)
            {
                _isStopping = true;
            }

            resetEvent.WaitOne(1000);
            _isStopping = false;
            System.Diagnostics.Debug.WriteLine("Listener Stopped");
            _isStarted = false;
        }

        public void Dispose()
        {
            while (_requestThread.IsAlive)
            {
                _listener.Stop();
                Thread.Sleep(10);
            }

            _listener.Close();
            _listener.Abort();
        }

        public void Start()
        {
            _listener.Start();
            _listener.IgnoreWriteExceptions = true;

            _requestThread = new Thread(RequestThread);
            _requestThread.Name = "RequestThread";
            _requestThread.Start();

            _numberOfRequest = 0;        
        }

        public bool IsStarted
        {
            get
            {
                return _isStarted;
            }
        }

        void WriteFile(HttpListenerContext ctx)
        {
            var response = ctx.Response;

            if ((_ms == null) || (!_ms.CanRead))
                return;

            MemoryStream newMs = new MemoryStream(_ms.GetBuffer());
            newMs.Position = 0;

            using (newMs)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Length = {0}", newMs.Length));
                response.ContentLength64 = newMs.Length;
                response.SendChunked = true;
                response.ContentType = System.Net.Mime.MediaTypeNames.Application.Octet;
                response.AddHeader("Content-disposition", "attachment; filename=1.mp4");

                byte[] buffer = new byte[64 * 1024];
                int read;
                int Count = 0;
//                long ticks = 0;
//                long oldTicks = 0;

                newMs.Position = 0;

                using (BinaryWriter bw = new BinaryWriter(response.OutputStream))
                {
                    while ((read = newMs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        try
                        {
                            bw.Write(buffer, 0, read);
                            bw.Flush();
                            Count += read;
//                            ticks = DateTime.Now.Ticks;
//                            System.Diagnostics.Debug.WriteLine(string.Format("Ticks = {0}", ticks - oldTicks));
//                            oldTicks = ticks;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Exception - error = {0}", ex));
                        }

                        if (_isStop)
                        {
                            break;
                        }
                    }
                    try
                    {
                        bw.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Exception ex = {0}", ex));
                    }
                }
                System.Diagnostics.Debug.WriteLine(string.Format("Count = {0}", Count));

                response.StatusCode = (int)HttpStatusCode.OK;
                response.StatusDescription = "OK";
                response.OutputStream.Close();
            }
        }

        public string FileName
        {
            set
            {
                if (value != string.Empty)
                    _filename = value;
            }
            get
            {
                return _filename;
            }
        }

        public MemoryStream memoryStream
        {
            set
            {
                _ms = value;
            }
            get
            {
                return _ms;
            }
        }
    }
}
