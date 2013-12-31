using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Diagnostics;
using System.Threading;

using System.IO;

namespace mediaElementPlayer
{ 
    public class Server
    {
        private HttpListener _listener;

        public Server()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:7896/");
            _listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
        }

        public void Start()
        {
            if (!HttpListener.IsSupported)
                System.Diagnostics.Debug.WriteLine("Not supported");

            _listener.Start();
            _listener.IgnoreWriteExceptions = true;

            System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        HttpListenerContext context = _listener.GetContext();
                        System.Threading.Tasks.Task.Factory.StartNew((ctx) =>
                            {
                                WriteFile((HttpListenerContext)ctx);
                            },context, System.Threading.Tasks.TaskCreationOptions.LongRunning);
                    }
                }, System.Threading.Tasks.TaskCreationOptions.LongRunning);            
        }

        void WriteFile(HttpListenerContext ctx)
        {
            var response = ctx.Response;
            using (FileStream fs = File.OpenRead(@"D:\Videos\miss A Bad Girl, Good Girl.mp4"))
            {
                response.ContentLength64 = fs.Length;
                response.SendChunked = false;
                response.ContentType = System.Net.Mime.MediaTypeNames.Application.Octet;
                response.AddHeader("Content-disposition", "attachment; filename=o115.mp3");

                byte[] buffer = new byte[64 * 1024];
                int read;
                using (BinaryWriter bw = new BinaryWriter(response.OutputStream))
                {
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        try
                        {
                            bw.Write(buffer, 0, read);
                            bw.Flush();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Exception - error = {0}", ex));
                        }
                    }

                    bw.Close();
                }

                response.StatusCode = (int)HttpStatusCode.OK;
                response.StatusDescription = "OK";
                response.OutputStream.Close();
            }
        }
    }
}
