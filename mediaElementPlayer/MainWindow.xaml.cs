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

        public MainWindow()
        {
            InitializeComponent();

            Loaded += new RoutedEventHandler(MainWindow_Loaded);

            mediaElement1.MediaEnded += new RoutedEventHandler(mediaElement1_MediaEnded);
            mediaElement1.MediaFailed += new EventHandler<ExceptionRoutedEventArgs>(mediaElement1_MediaFailed);
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Thread startServer = new Thread(new ThreadStart(StartServer));
            startServer.Name = "StartServer";
            startServer.Start();

            MemoryStream ms = new MemoryStream();

//            mediaElement1.Source = new Uri("D:\\Videos\\miss A Bad Girl, Good Girl.mp4");
//            mediaElement1.Play();

            Thread.Sleep(1000);

            mediaElement1.Source = new Uri(@"http://localhost:7896/", UriKind.Absolute);
            mediaElement1.Play();
        }

        #region MediaElement Events
        void mediaElement1_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
        }
        #endregion

        private void StartServer()
        {
            Server server = new Server();
            server.Start();
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
    }
}
