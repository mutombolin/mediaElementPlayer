﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PortableDeviceApiLib;
using PortableDeviceTypesLib;
using System.IO;

using System.Net;
using System.Diagnostics;
using System.Threading;

using _tagpropertykey = PortableDeviceApiLib._tagpropertykey;
using IPortableDeviceKeyCollection = PortableDeviceApiLib.IPortableDeviceKeyCollection;
using IPortableDeviceValues = PortableDeviceApiLib.IPortableDeviceValues;
using System.Runtime.InteropServices;


namespace PortableDevice
{
    public class PortableDevice
    {
        private bool _isConnected;
        private readonly PortableDeviceClass _device;

        public PortableDevice(string deviceId)
        {
            this._device = new PortableDeviceClass();
            this.DeviceId = deviceId;
        }

        public string DeviceId { get; set; }

        public string FriendlyName
        {
            get
            {
                if (!this._isConnected)
                {
                    throw new InvalidOperationException("Not connected to device.");
                }

                // Retrieve the properties of the device
                IPortableDeviceContent content;
                IPortableDeviceProperties properties;
                this._device.Content(out content);
                content.Properties(out properties);

                // Retrieve the values for the properties
                IPortableDeviceValues propertyValues;
                properties.GetValues("DEVICE", null, out propertyValues);

                // Identify the property to retrieve
                var property = new _tagpropertykey();
                property.fmtid = new Guid(0x26D4979A, 0xE643, 0x4626, 0x9E, 0x2B,
                                          0x73, 0x6D, 0xC0, 0xC9, 0x2F, 0xDC);
                property.pid = 12;

                // Retrieve the friendly name
                string propertyValue;
                propertyValues.GetStringValue(ref property, out propertyValue);

                return propertyValue;
            }
        }

//      internal PortableDeviceClass PortableDeviceClass
        public PortableDeviceClass PortableDeviceClass
        {
            get
            {
                return this._device;
            }
        }

        public void Connect()
        {
            if (this._isConnected) { return; }

            var clientInfo = (IPortableDeviceValues)new PortableDeviceValuesClass();
            this._device.Open(this.DeviceId, clientInfo);
            this._isConnected = true;
        }

        public void Disconnect()
        {
            if (!this._isConnected) { return; }
            this._device.Close();
            this._isConnected = false;
        }

        public PortableDeviceFolder GetContents()
        {
            var root = new PortableDeviceFolder("DEVICE", "DEVICE");

            IPortableDeviceContent content;
            this._device.Content(out content);

            EnumerateContents(ref content, root);

            return root;
        }

        private static void EnumerateContents(ref IPortableDeviceContent content,
            PortableDeviceFolder parent)
        {
            // Get the properties of the object
            IPortableDeviceProperties properties;
            content.Properties(out properties);

            // Enumerate the items contained by the current object
            IEnumPortableDeviceObjectIDs objectIds;
            content.EnumObjects(0, parent.Id, null, out objectIds);

            uint fetched = 0;
            do
            {
                string objectId;

                objectIds.Next(1, out objectId, ref fetched);
                if (fetched > 0)
                {
                    var currentObject = WrapObject(properties, objectId);

                    parent.Files.Add(currentObject);

                    if (currentObject is PortableDeviceFolder)
                    {
                        EnumerateContents(ref content, (PortableDeviceFolder)currentObject);
                    }
                }
            } while (fetched > 0);
        }

        public MemoryStream GetMemoryStream(PortableDeviceFile file)
        {
            MemoryStream ms = new MemoryStream();

            IPortableDeviceContent content;
            this._device.Content(out content);

            IPortableDeviceResources resources;
            content.Transfer(out resources);

            PortableDeviceApiLib.IStream wpdStream;
            uint optimalTransferSize = 0;

            var property = new _tagpropertykey();
            property.fmtid = new Guid(0xE81E79BE, 0x34F0, 0x41BF, 0xB5, 0x3F, 0xF1, 0xA0, 0x6A, 0xE8, 0x78, 0x42);
            property.pid = 0;

            resources.GetStream(file.Id, ref property, 0, ref optimalTransferSize, out wpdStream);

            System.Runtime.InteropServices.ComTypes.IStream sourceStream = (System.Runtime.InteropServices.ComTypes.IStream)wpdStream;

            var filename = Path.GetFileName(file.Id);
            Console.WriteLine(string.Format("file.Id = {0} fileanme = {1}", file.Id, filename));
            
            unsafe
            {
                var buffer = new byte[64 * 1024];
                int bytesRead;
                do
                {
                    sourceStream.Read(buffer, buffer.Length, new IntPtr(&bytesRead));
                    ms.Write(buffer, 0, buffer.Length);
                } while (bytesRead > 0);
                ms.Position = 0;
                sourceStream.Commit(0);
                sourceStream = null;
            }

            return ms;
        }

        public void DownloadFile(PortableDeviceFile file, string saveToPath)
        {
            IPortableDeviceContent content;
            this._device.Content(out content);

            IPortableDeviceResources resources;
            content.Transfer(out resources);

            PortableDeviceApiLib.IStream wpdStream;
            uint optimalTransferSize = 0;

            var property = new _tagpropertykey();
            property.fmtid = new Guid(0xE81E79BE, 0x34F0, 0x41BF, 0xB5, 0x3F, 0xF1, 0xA0, 0x6A, 0xE8, 0x78, 0x42);
            property.pid = 0;

            resources.GetStream(file.Id, ref property, 0, ref optimalTransferSize, out wpdStream);

            System.Runtime.InteropServices.ComTypes.IStream sourceStream = (System.Runtime.InteropServices.ComTypes.IStream)wpdStream;

            var filename = Path.GetFileName(file.Id);
            Console.WriteLine(string.Format("file.Id = {0}", file.Id));
            FileStream targetStream = new FileStream(Path.Combine(saveToPath, filename), FileMode.Create, FileAccess.Write);

            unsafe
            {
                var buffer = new byte[1024];
                int bytesRead;
                do
                {
                    sourceStream.Read(buffer, 1024, new IntPtr(&bytesRead));
                    targetStream.Write(buffer, 0, 1024);
                } while (bytesRead > 0);
                targetStream.Close();
                sourceStream.Commit(0);
                sourceStream = null;
            }
        }

        public void DeleteFile(PortableDeviceFile file)
        {
            IPortableDeviceContent content;
            this._device.Content(out content);

            var variant = new PortableDeviceApiLib.tag_inner_PROPVARIANT();
            StringToPropVariant(file.Id, out variant);

            PortableDeviceApiLib.IPortableDevicePropVariantCollection objectIds =
                new PortableDeviceTypesLib.PortableDevicePropVariantCollection()
                as PortableDeviceApiLib.IPortableDevicePropVariantCollection;
            objectIds.Add(variant);

            content.Delete(0, objectIds, null);
        }

        public void TransferContentToDevice(string fileName, string parentObjectId)
        {
            IPortableDeviceContent content;
            this._device.Content(out content);

            IPortableDeviceValues values =
                GetRequiredPropertiesForContentType(fileName, parentObjectId);

            PortableDeviceApiLib.IStream tempStream;
            uint optimalTransferSizeBytes = 0;
            content.CreateObjectWithPropertiesAndData(
                values,
                out tempStream,
                ref optimalTransferSizeBytes,
                null);

            System.Runtime.InteropServices.ComTypes.IStream targetStream =
                (System.Runtime.InteropServices.ComTypes.IStream)tempStream;
            try
            {
                using (var sourceStream =
                    new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[optimalTransferSizeBytes];
                    int bytesRead;
                    do
                    {
                        bytesRead = sourceStream.Read(
                            buffer, 0, (int)optimalTransferSizeBytes);
                        IntPtr pcbWritten = IntPtr.Zero;
                        targetStream.Write(
                            buffer, (int)optimalTransferSizeBytes, pcbWritten);
                    } while (bytesRead > 0);
                }
                targetStream.Commit(0);
            }
            finally
            {
                Marshal.ReleaseComObject(tempStream);
            }
        }

        private IPortableDeviceValues GetRequiredPropertiesForContentType(
            string fileName,
            string parentObjectId)
        {
            IPortableDeviceValues values =
                new PortableDeviceTypesLib.PortableDeviceValues() as IPortableDeviceValues;

            var WPD_OBJECT_PARENT_ID = new _tagpropertykey();
            WPD_OBJECT_PARENT_ID.fmtid =
                new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC,
                         0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_PARENT_ID.pid = 3;
            values.SetStringValue(ref WPD_OBJECT_PARENT_ID, parentObjectId);

            FileInfo fileInfo = new FileInfo(fileName);
            var WPD_OBJECT_SIZE = new _tagpropertykey();
            WPD_OBJECT_SIZE.fmtid =
                new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC,
                         0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_SIZE.pid = 11;
            values.SetUnsignedLargeIntegerValue(WPD_OBJECT_SIZE, (ulong)fileInfo.Length);

            var WPD_OBJECT_ORIGINAL_FILE_NAME = new _tagpropertykey();
            WPD_OBJECT_ORIGINAL_FILE_NAME.fmtid =
                new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC,
                         0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_ORIGINAL_FILE_NAME.pid = 12;
            values.SetStringValue(WPD_OBJECT_ORIGINAL_FILE_NAME, Path.GetFileName(fileName));

            var WPD_OBJECT_NAME = new _tagpropertykey();
            WPD_OBJECT_NAME.fmtid =
                new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC,
                         0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_NAME.pid = 4;
            values.SetStringValue(WPD_OBJECT_NAME, Path.GetFileName(fileName));

            return values;
        }

        private static void StringToPropVariant(
            string value,
            out PortableDeviceApiLib.tag_inner_PROPVARIANT propvarValue)
        {
            PortableDeviceApiLib.IPortableDeviceValues pValues =
                (PortableDeviceApiLib.IPortableDeviceValues)
                    new PortableDeviceTypesLib.PortableDeviceValuesClass();

            var WPD_OBJECT_ID = new _tagpropertykey();
            WPD_OBJECT_ID.fmtid =
                new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA,
                         0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_ID.pid = 2;

            pValues.SetStringValue(ref WPD_OBJECT_ID, value);

            pValues.GetValue(ref WPD_OBJECT_ID, out propvarValue);
        }

        private static PortableDeviceObject WrapObject(IPortableDeviceProperties properties,
            string objectId)
        {
            IPortableDeviceKeyCollection keys;
            properties.GetSupportedProperties(objectId, out keys);

            IPortableDeviceValues values;
            properties.GetValues(objectId, keys, out values);

            // Get the name of the object
            string name;
            var property = new _tagpropertykey();
            property.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC,
                                      0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            property.pid = 4;
            values.GetStringValue(property, out name);

            // Get the type of the object
            Guid contentType;
            property = new _tagpropertykey();
            property.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC,
                                      0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            property.pid = 7;
            values.GetGuidValue(property, out contentType);

            var folderType = new Guid(0x27E2E392, 0xA111, 0x48E0, 0xAB, 0x0C,
                                      0xE1, 0x77, 0x05, 0xA0, 0x5F, 0x85);
            var functionalType = new Guid(0x99ED0160, 0x17FF, 0x4C44, 0x9D, 0x98,
                                          0x1D, 0x7A, 0x6F, 0x94, 0x19, 0x21);

            if (contentType == folderType || contentType == functionalType)
            {
                return new PortableDeviceFolder(objectId, name);
            }

            property.pid = 12;
            values.GetStringValue(property, out name);

            return new PortableDeviceFile(objectId, name);
        }
    }

    public class Server
    {
        private HttpListener _listener;
        private bool _isStop = false;
        private bool _isStarted = false;
        private Thread _requestThread;
        private bool _isStopping = false;
        private int _numberOfRequest;

        private ManualResetEvent resetEvent;

        private PortableDevice _device;
        private PortableDeviceFile _file;

        System.Runtime.InteropServices.ComTypes.IStream _sourceStream;

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

            System.Diagnostics.Debug.WriteLine(string.Format("ListenerCallback numberOfRequest = {0}", _numberOfRequest));

            if (_isStarted)
                _isStop = true;
            else
                _isStop = false;

            while (_isStop)
                Thread.Sleep(100);
                        
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
            _isStop = true;
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

        private static IntPtr ReadBuffer;

        static int Read(System.Runtime.InteropServices.ComTypes.IStream strm, byte[] buffer)
        {
            if (ReadBuffer == IntPtr.Zero)
                ReadBuffer = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(int)));
            strm.Read(buffer, buffer.Length, ReadBuffer);
            return Marshal.ReadInt32(ReadBuffer);
        }

        void WriteFile(HttpListenerContext ctx)
        {
            var response = ctx.Response;
/*
            if ((_ms == null) || (!_ms.CanRead))
                return;

            MemoryStream newMs = new MemoryStream(_ms.GetBuffer());
            newMs.Position = 0;
*/
            if ((_device == null) || (_file == null))
                return;

            while (_sourceStream != null)
            {
                Thread.Sleep(100);
            }

            System.Diagnostics.Debug.WriteLine("WriteFile");

            IPortableDeviceContent content;
            this._device.PortableDeviceClass.Content(out content);

            IPortableDeviceResources resources;
            content.Transfer(out resources);

            PortableDeviceApiLib.IStream wpdStream;
            uint optimalTransferSize = 0;

            var property = new _tagpropertykey();
            property.fmtid = new Guid(0xE81E79BE, 0x34F0, 0x41BF, 0xB5, 0x3F, 0xF1, 0xA0, 0x6A, 0xE8, 0x78, 0x42);
            property.pid = 0;

 //           resources.GetStream(_file.Id, ref property, 0, ref optimalTransferSize, out wpdStream);

//            System.Runtime.InteropServices.ComTypes.IStream sourceStream = (System.Runtime.InteropServices.ComTypes.IStream)wpdStream;

            var filename = Path.GetFileName(_file.Id);
            Console.WriteLine(string.Format("file.Id = {0} fileanme = {1} extension = {2}", _file.Id, Path.GetFullPath(_file.Id), Path.GetExtension(_file.Id)));
/*
            unsafe
            {
                var buffer = new byte[64 * 1024];
                int bytesRead;
                do
                {
                    sourceStream.Read(buffer, buffer.Length, new IntPtr(&bytesRead));
                    ms.Write(buffer, 0, buffer.Length);
                } while (bytesRead > 0);
                ms.Position = 0;
                sourceStream.Commit(0);
                sourceStream = null;
            }
*/

//            using (newMs)
            try
            {
                resources.GetStream(_file.Id, ref property, 0, ref optimalTransferSize, out wpdStream);
                _sourceStream = (System.Runtime.InteropServices.ComTypes.IStream)wpdStream;
//                System.Runtime.InteropServices.ComTypes.IStream sourceStream = (System.Runtime.InteropServices.ComTypes.IStream)wpdStream;
//                System.Diagnostics.Debug.WriteLine(string.Format("Length = {0}", newMs.Length));
//                response.ContentLength64 = newMs.Length;
//                System.Diagnostics.Debug.WriteLine(string.Format("length = {0}", optimalTransferSize));
                response.ContentLength64 = 0;
                response.SendChunked = true;
                response.ContentType = System.Net.Mime.MediaTypeNames.Application.Octet;
                response.AddHeader("Content-disposition", "attachment; filename=1.mp4");

                byte[] buffer = new byte[64 * 1024];
                int read = 0;
                int Count = 0;
                //                long ticks = 0;
                //                long oldTicks = 0;

//                newMs.Position = 0;

                using (BinaryWriter bw = new BinaryWriter(response.OutputStream))
                {
                    do
                    {
                        read = Read(_sourceStream, buffer);

                        try
                        {
                            bw.Write(buffer, 0, read);
                            bw.Flush();
                            Count += read;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Exception - ex = {0}", ex));
                        }

                        if (_isStop)
                            break;
                    } while (read > 0);
/*                    do
                    {
                        unsafe
                        {
                            _sourceStream.Read(buffer, buffer.Length, new IntPtr(&read));

                            try
                            {
                                bw.Write(buffer, 0, read);
                                bw.Flush();
                                Count += read;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(string.Format("Exception - error = {0}", ex));
                            }

                            if (_isStop)
                                break;
                        }

                    } while (read > 0);
*/
                    _sourceStream.Commit(0);
                    _sourceStream = null;
/*
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
*/
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("WriteFile Ex = {0}", ex));
            }

            _isStop = false;
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

        public PortableDevice Device
        {
            set
            {
                _device = value;
            }
            get
            {
                return _device;
            }
        }

        public PortableDeviceFile FileObject
        {
            set
            {
                _file = value;
            }
            get
            {
                return _file;
            }
        }
    }
}
