using CryPixivClient.Objects;
using CryPixivClient.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
    public partial class WorkDetails : Window
    {
        event EventHandler<ImageSource> ImageDownloaded;

        static bool wasMaximized = false;
        static SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public PixivWork LoadedWork { get; private set; }
        Dictionary<int, ImageSource> DownloadedImages = new Dictionary<int, ImageSource>();

        const int WorkCacheLimit = 6;
        static List<Tuple<long, Dictionary<int, ImageSource>>> PreviousDownloads = new List<Tuple<long, Dictionary<int, ImageSource>>>();
        int currentPage = 1;

        public WorkDetails(PixivWork work)
        {
            InitializeComponent();
            SetWindow();
            if (wasMaximized) WindowState = WindowState.Maximized;

            LoadedWork = work;
            DataContext = this;

            this.Closing += WorkDetails_Closing;
            this.StateChanged += (a, b) => wasMaximized = WindowState == WindowState.Maximized;

            LoadWork(work);
        }

        bool openedCache = false;
        void LoadWork(PixivWork newWork)
        {
            timestamp = DateTime.Now;
            LoadedWork = newWork;
            mainImage.Source = null;

            currentPage = 1;
            DownloadedImages.Clear();
            DataContext = null; DataContext = this;

            // load cached results if available
            var prevd = PreviousDownloads.Find(x => x.Item1 == newWork.Id.Value);
            if (prevd != null)
            {
                DownloadedImages = new Dictionary<int, ImageSource>(prevd.Item2);                    
                openedCache = true;

                // add at the beginning
                PreviousDownloads.Remove(prevd);
                PreviousDownloads.Add(prevd);
            }
            else openedCache = false;

            comboTags.ItemsSource = LoadedWork.Tags;
            txtClipboard.Text = "Click on tag to copy to clipboard";
            txtScore.Text = $"Score: {LoadedWork.Stats?.Score ?? LoadedWork.TotalBookmarks}";
            txtArtist.Text = LoadedWork.User.Name;
            txtResolution.Text = $"{LoadedWork.Width}x{LoadedWork.Height}";
            txtTitle.Text = LoadedWork.Title;
            Title = $"Work Details - ({LoadedWork.Id}) {LoadedWork.Title}";
            SetPageStatus();

            // start downloading images
            DownloadImages();

            if (DownloadedImages.Count >= 1) SetImage(1);
            if (LoadedWork.ImageThumbnail != null) mainImage.Source = LoadedWork.ImageThumbnail;

            // once first image is downloaded, show it
            ImageDownloaded += (a, b) =>
            {
                if (DownloadedImages.Count == 1) SetImage(1);
            };
        }

        void SetPageStatus()
        {
            txtPage.Text = $"{currentPage}/{LoadedWork.PageCount}" + ((DownloadedImages.Count < LoadedWork.PageCount) ? $" ({DownloadedImages.Count})" : "");
            if (DownloadedImages.ContainsKey(currentPage))
            {
                var img = (BitmapImage)DownloadedImages[currentPage];
                
                txtResolution.Text = $"{img.PixelWidth}x{img.PixelHeight}";
            }
        }      

        async Task DownloadImages()
        {
            if (DownloadedImages.Count >= LoadedWork.PageCount) return;

            await semaphore.WaitAsync();
            SetProgressBar(true);
            try
            {
                var lworkid = LoadedWork.Id;
                for (int i = DownloadedImages.Count; i < LoadedWork.PageCount; i++)
                {
                    if (isClosing || lworkid != LoadedWork.Id) break;
                    var img = await Task.Run(() => LoadedWork.GetImage(LoadedWork.GetImageUri(LoadedWork.OriginalImageUrl, i)));
                    if (isClosing || lworkid != LoadedWork.Id) break;

                    DownloadedImages.Add(i + 1, img);
                    CacheDownloads();
                    SetPageStatus();
                    ImageDownloaded?.Invoke(this, img);
                }
            }
            finally
            {
                isClosing = false;
                SetProgressBar(false);
                semaphore.Release();
            }
        }

        public void SetImage(int page)
        {
            currentPage = page;
            SetPageStatus();
            mainImage.Source = DownloadedImages[page];
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage + 1 > DownloadedImages.Count) return;
            SetImage(currentPage + 1);
        }

        private void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage - 1 <= 0) return;
            SetImage(currentPage - 1);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.A:
                    btnPrev_Click(this, null);
                    break;
                case Key.Right:
                case Key.D:
                    btnNext_Click(this, null);
                    break;
                case Key.Q:
                case Key.Down:
                    PrevPost();
                    break;
                case Key.E:
                case Key.Up:
                    NextPost();
                    break;
                case Key.Enter:
                    ToggleState();
                    break;
                case Key.Escape:
                    Close();
                    break;
            }
        }

        void NextPost()
        {
            // open next one
            var result = MainWindow.MainModel.OpenNextWork(LoadedWork);
            if (result == null) return;
            LoadWork(result);

            //if (result) Close();
        }
        void PrevPost()
        {
            // open prev one
            var result = MainWindow.MainModel.OpenPrevWork(LoadedWork);
            if (result == null) return;
            LoadWork(result);
        }

        void SetProgressBar(bool show) => progressBar.Visibility = show ? Visibility.Visible : Visibility.Hidden;

        DateTime timestamp;
        void ToggleState() => this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        void PreviewImageClick(object sender, MouseButtonEventArgs e)
        {
            var stampNow = DateTime.Now;
            if (stampNow.Subtract(timestamp).TotalSeconds < 0.3)
            {
                ToggleState();
                timestamp = stampNow.AddSeconds(-30);
            }
            else timestamp = stampNow;
        }
        
        void SetWindow()
        {
            if (Settings.Default.DetailWindowHeight == 0) return;

            Height = Settings.Default.DetailWindowHeight;
            Width = Settings.Default.DetailWindowWidth;
            Left = Settings.Default.DetailWindowLeft;
            Top = Settings.Default.DetailWindowTop;
        }

        bool isOpening = false;
        bool isClosing = false;
        void WorkDetails_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isOpening == false) SavePos();
            PreviousDownloads.Clear();

            isClosing = true;
        }

        void SavePos()
        {
            Settings.Default.DetailWindowHeight = Height;
            Settings.Default.DetailWindowWidth = Width;
            Settings.Default.DetailWindowLeft = Left;
            Settings.Default.DetailWindowTop = Top;
            Settings.Default.Save();
        }

        void CacheDownloads()
        {
            if (PreviousDownloads.Count > WorkCacheLimit) PreviousDownloads.RemoveAt(0);

            if (openedCache) return;

            PreviousDownloads.Add(new Tuple<long, Dictionary<int, ImageSource>>(LoadedWork.Id.Value, new Dictionary<int, ImageSource>(DownloadedImages)));
        }

        private void btnInternet_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainModel.OpenInBrowser(LoadedWork);
        }

        private void btnBookmark_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainModel.BookmarkWork(LoadedWork);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainModel.DownloadSelectedWorks(LoadedWork, true);
        }

        private void txtArtist_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // search artist
        }

        private void comboTags_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboTags.SelectedIndex == -1) return;

            var text = comboTags.SelectedItem as string;
            Clipboard.SetText(text);
            txtClipboard.Text = "Tag copied to clipboard!";
        }
    }
}
