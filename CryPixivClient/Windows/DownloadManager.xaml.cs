using CryPixivClient.Objects;
using CryPixivClient.Properties;
using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CryPixivClient.Windows
{
    public partial class DownloadManager : Window, INotifyPropertyChanged
    {
        public string UniqueIdentifier { get; }
        public List<PixivWork> ToDownload { get; }

        Downloader downloader;
        DesignModel designModel;
        Progress<Downloader.DownloaderProgress> progress;
        MyObservableCollection<DownloadObject> downloadObjects;

        public event PropertyChangedEventHandler PropertyChanged;


        public MyObservableCollection<DownloadObject> DownloadObjects
        {
            get => downloadObjects;
            set
            {
                downloadObjects = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DownloadObjects"));
            }
        }

        public DownloadManager(List<PixivWork> toDownload, string destination)
        {
            InitializeComponent();
            if (toDownload == null || toDownload.Count == 0) { Close(); return; }
            designModel = (DesignModel)FindResource("designMld");
            DataContext = this;
            SetWindow();

            this.Closing += DownloadManager_Closing;
            this.SizeChanged += DownloadManager_SizeChanged;

            // setup
            ToDownload = toDownload;
            UniqueIdentifier = GenerateUniqueIdentifier(ToDownload);

            // prepare observable collection
            downloadObjects = new MyObservableCollection<DownloadObject>();
            foreach (var i in ToDownload) downloadObjects.Add(new DownloadObject(i));

            // startup downloader
            progress = new Progress<Downloader.DownloaderProgress>(ProgressChanged);
            downloader = new Downloader(toDownload, destination, progress);
            downloader.Finished += Downloader_Finished;
            downloader.ErrorEncountered += Downloader_ErrorEncountered;
            downloader.Start();
        }

        void Downloader_Finished(object sender, EventArgs e)
        {
            if (downloader.Percentage == 100.0)
            {
                btnPause.IsEnabled = false;
            }
        }
        void Downloader_ErrorEncountered(object sender, Tuple<long, int, string> e)
        {
            foreach(var d in downloadObjects) 
                if (d.Work.Id.Value == e.Item1)
                {
                    d.IsError = true;
                    d.ErrorMessage = e.Item3;
                    break;
                }
        }

        void ProgressChanged(Downloader.DownloaderProgress progress)
        {
            foreach (var d in downloadObjects)
                if (d.Work.Id.Value == progress.AssociatedWork.Id.Value)
                {
                    d.CompletedPages = progress.PageNumber;
                    double valuePerPage = (100.0 / d.Work.PageCount.Value);
                    d.Percentage = valuePerPage * d.CompletedPages + valuePerPage * (progress.Progress / 100.0);
                    break;
                }
        }

        public static string GenerateUniqueIdentifier(IEnumerable<PixivWork> collection)
        {
            string allIds = "";
            foreach (var i in collection) allIds += i.Id.Value + "-";

            var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(allIds)));
        }

        void SetWindow()
        {
            if (Settings.Default.DownloaderWindowHeight == 0) return;
            Height = Settings.Default.DownloaderWindowHeight;
            Width = Settings.Default.DownloaderWindowWidth;
            Left = Settings.Default.DownloaderWindowLeft;
            Top = Settings.Default.DownloaderWindowTop;
        }
        void DownloadManager_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.downloader.Stop();

            Settings.Default.DownloaderWindowHeight = Height;
            Settings.Default.DownloaderWindowWidth = Width;
            Settings.Default.DownloaderWindowLeft = Left;
            Settings.Default.DownloaderWindowTop = Top;
            Settings.Default.Save();
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (this.downloader.IsStarted)
            {
                this.downloader.Pause();
                btnPause.Content = "Continue";
            }
            else
            {
                this.downloader.Start();
                btnPause.Content = "Pause";
            }
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(this.downloader.Destination);
        }

        private void DownloadManager_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            designModel.GridWidth = this.Width - 30;
        }
    }
    public class DesignModel : INotifyPropertyChanged
    {
        double gridw = 0;
        public double GridWidth
        {
            get => (gridw < 100) ? 100 : gridw;
            set
            {
                gridw = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("GridWidth"));
            }
        }

        MyObservableCollection<DownloadObject> downloadObjects;
        public event PropertyChangedEventHandler PropertyChanged;
        public MyObservableCollection<DownloadObject> DownloadObjects
        {
            get => downloadObjects;
            set
            {
                downloadObjects = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DownloadObjects"));
            }
        }
    }
}
