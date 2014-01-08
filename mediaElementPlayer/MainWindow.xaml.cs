using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;
using System.IO.Packaging;
using System.IO.Pipes;
//using System.ServiceModel;
using System.Threading;

namespace mediaElementPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _pipeName = "nameMediaElementPlayer";
//        private string _filename = @"D:\Videos\miss A Bad Girl, Good Girl.mp4";
        private string _filename = @"D:\Temp\o115.mp3";
        private System.Timers.Timer _bufferingTimer;
        private List<string> _playList;
        private Server _server;
        private int _curIndex;

        private List<PortableDevice.PortableDeviceObject> _portableList;
        private event EventHandler LoadedCompleted;
        private PortableDevice.PortableDevice _device;

        public MainWindow()
        {
            InitializeComponent();

            _curIndex = 0;

            _playList = new List<string>();
//            _playList.Add(@"D:\Videos\FTISLAND - Severely.mp4");
            _playList.Add(@"D:\Videos\miss A Bad Girl, Good Girl.mp4");
//            _playList.Add(@"D:\Videos\T-ARA(티아라) _ Sexy Love (Dance Ver. MV).mp4");
            _playList.Add(@"D:\Temp\o1928.mp3");
            _playList.Add(@"D:\Temp\o115.mp3");

            

            Loaded += new RoutedEventHandler(MainWindow_Loaded);
            Closed += new EventHandler(MainWindow_Closed);

            _bufferingTimer = new System.Timers.Timer();
            _bufferingTimer.Interval = 300;
            _bufferingTimer.Elapsed += new System.Timers.ElapsedEventHandler(_bufferingTimer_Elapsed);
            mediaElement1.MediaEnded += new RoutedEventHandler(mediaElement1_MediaEnded);
            mediaElement1.MediaFailed += new EventHandler<ExceptionRoutedEventArgs>(mediaElement1_MediaFailed);

            mediaElement1.BufferingStarted += new RoutedEventHandler(mediaElement1_BufferingStarted);
            mediaElement1.BufferingEnded += new RoutedEventHandler(mediaElement1_BufferingEnded);

            LoadedCompleted += new EventHandler(MainWindow_LoadedCompleted);
        }

        void MainWindow_Closed(object sender, EventArgs e)
        {
            _server.Stop();
            _server.Dispose();
        }

        void mediaElement1_BufferingEnded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Buffering Ended");
        }

        void mediaElement1_BufferingStarted(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Buffering Started");
        }

        void _bufferingTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new System.Timers.ElapsedEventHandler(SAFE_bufferingTimer_Elapsed), sender, e);
        }

        void SAFE_bufferingTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Buffering {0}", mediaElement1.BufferingProgress));
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _server = new Server();
            Start();
//            mediaElement1.Source = new Uri(@"http://localhost:7896/", UriKind.Absolute);
//            mediaElement1.Play();
//            _bufferingTimer.Start();


            Thread loadItems = new Thread(new ThreadStart(LoadPortableDevice));
            loadItems.Name = "LoadItems";
            loadItems.Start();
        }

        #region MediaElement Events
        void mediaElement1_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
        }
        #endregion

        private void Start()
        {
            Thread startServer = new Thread(new ThreadStart(StartServer));
            startServer.Name = "StartServer";
            startServer.Start();        
        }

        private void StartServer()
        {
            _server.Start();
        }

        private void UpdateContent()
        {
            mediaElement1.Stop();
            mediaElement1.Source = null;

            if (_server.IsStarted)
                _server.Stop();

            if (_curIndex >= _portableList.Count)
                _curIndex = 0;

            if (_curIndex < 0)
                _curIndex = _portableList.Count - 1;

            var item = _portableList[_curIndex] as PortableDevice.PortableDeviceFile;

            MemoryStream ms = _device.GetMemoryStream(item);

            ms.Position = 0;
            _server.memoryStream = ms;
/*
            string filename = _playList[_curIndex];

            if (filename != string.Empty)
                _server.FileName = filename;
*/
//            mediaElement1.Source = null;
            mediaElement1.Source = new Uri(@"http://localhost:7896/", UriKind.Absolute);
            mediaElement1.Play();

            listBox1.SelectedIndex = _curIndex;
            listBox1.ScrollIntoView(listBox1.SelectedItem);
        }

        private void IsPlaying(bool value)
        {
            btnStop.IsEnabled = value;
            btnMoveBackward.IsEnabled = value;
            btnMoveForward.IsEnabled = value;
            btnPlay.IsEnabled = value;
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            mediaElement1.Play();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaElement1.Stop();
            mediaElement1.Source = null;
            _server.Stop();
        }

        private void btnMoveBackward_Click(object sender, RoutedEventArgs e)
        {
            mediaElement1.Position = mediaElement1.Position - TimeSpan.FromSeconds(5);
        }

        private void btnMoveForward_Click(object sender, RoutedEventArgs e)
        {
            mediaElement1.Position = mediaElement1.Position + TimeSpan.FromSeconds(5);
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaElement1.Pause();
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            _curIndex++;
            UpdateContent();
        }

        private void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            _curIndex--;
            UpdateContent();
        }

        private void listBox1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _curIndex = listBox1.SelectedIndex;
            UpdateContent();
        }

        public void DisplayObject(PortableDevice.PortableDeviceObject portableDeviceObject)
        {
            if (portableDeviceObject is PortableDevice.PortableDeviceFolder)
                DisplayFolderContents((PortableDevice.PortableDeviceFolder)portableDeviceObject);
        }

        public void DisplayFolderContents(PortableDevice.PortableDeviceFolder folder)
        {
            foreach (var item in folder.Files)
            {
                if (item is PortableDevice.PortableDeviceFolder)
                    DisplayFolderContents((PortableDevice.PortableDeviceFolder)item);
                else if (item is PortableDevice.PortableDeviceFile)
                {
                    if (item.Name.ToLower().Contains("mp3"))
                        _portableList.Add(item as PortableDevice.PortableDeviceObject);
                    if (item.Name.ToLower().Contains("mp4"))
                        _portableList.Add(item as PortableDevice.PortableDeviceObject);                       
                }
            }
        }

        private void LoadPortableDevice()
        {
            var devices = new PortableDevice.PortableDeviceCollection();
            devices.Refresh();

            if (devices.Count <= 0)
                return;

            _device = devices.First();
            _device.Connect();

            _portableList = new List<PortableDevice.PortableDeviceObject>();

            var folder = _device.GetContents();

            foreach (var item in folder.Files)
            {
                DisplayObject(item);
            }

            LoadedCompleted(this, new EventArgs());
        }


        void MainWindow_LoadedCompleted(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("LoadedCompleted");

            Dispatcher.BeginInvoke(new EventHandler(SAFE_MainWindow_LoadedCompleted), sender, e);
        }

        void SAFE_MainWindow_LoadedCompleted(object sender, EventArgs e)
        {
            foreach (var item in _portableList)
            {
                listBox1.Items.Add(item.Name.ToString());
            }
        }
    }
}
