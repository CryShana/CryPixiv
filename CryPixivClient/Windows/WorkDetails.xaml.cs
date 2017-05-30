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

            comboTags.ItemsSource = GetTranslatedTags(LoadedWork.Tags);
            txtClipboard.Text = "Click on tag to copy it!";
            txtScore.Text = $"Score: {LoadedWork.Stats?.Score ?? LoadedWork.TotalBookmarks}";
            txtArtist.Text = LoadedWork.User.Name;
            txtResolution.Text = $"{LoadedWork.Width}x{LoadedWork.Height}";
            txtTitle.Text = LoadedWork.Title;
            Title = $"Work Details - ({LoadedWork.Id}) {LoadedWork.Title}";
            SetPageStatus();

            // start downloading images
            DownloadImages();

            if (LoadedWork.img != null) mainImage.Source = LoadedWork.ImageThumbnail;
            if (DownloadedImages.Count >= 1) SetImage(1);

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
            try
            {
                mainImage.Source = DownloadedImages[page];
            }
            catch(Exception ex)
            {
                MessageBox.Show("Debug error: " + ex.Message);
            }
        }

        void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage + 1 > DownloadedImages.Count)
            {
                if (currentPage + 1 > LoadedWork.PageCount.Value)
                {
                    var result = MainWindow.MainModel.OpenNextWork(LoadedWork);
                    if (result == null) return;
                    LoadWork(result);
                }

                return;
            }
            SetImage(currentPage + 1);
        }

        void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage - 1 <= 0)
            {
                var result = MainWindow.MainModel.OpenPrevWork(LoadedWork);
                if (result == null) return;
                LoadWork(result);
                return;
            }
            SetImage(currentPage - 1);
        }

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
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
            if (e.LeftButton == MouseButtonState.Released) return;

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

        void btnInternet_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainModel.OpenInBrowser(LoadedWork);
        }

        void btnBookmark_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainModel.BookmarkWork(LoadedWork);
        }

        void DownloadSelected(object sender, RoutedEventArgs e)
        {
            MainWindow.MainModel.DownloadSelectedWorks(LoadedWork, true);
        }

        void txtArtist_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // search artist
            MainWindow.ShowUserWork(LoadedWork.User.Id ?? -1, LoadedWork.User.Name);
        }

        void comboTags_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboTags.SelectedIndex == -1) return;

            var text = comboTags.SelectedItem as Translation;
            Clipboard.SetText(text.Original);
            txtClipboard.Text = "Tag copied to clipboard!";
        }

        async void CopyImage(object sender, RoutedEventArgs e)
        {
            if (DownloadedImages.ContainsKey(currentPage) == false) return;
            
            var copyTd = new Thread(CopyImageToClipboard);
            copyTd.SetApartmentState(ApartmentState.STA);
            copyTd.Start();
        }

        void CopyImageToClipboard()
        {
            var src = DownloadedImages[currentPage] as BitmapSource;
            src.Freeze();
            Clipboard.SetImage(src);            
        }

        void CopyLink(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText($"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={LoadedWork.Id}");
        }

        List<Translation> GetTranslatedTags(List<string> tags)
        {
            var newTags = new List<Translation>();

            foreach (var t in tags) newTags.Add(new Translation(t));
            
            return newTags;
        }
    }

    public class Translation : INotifyPropertyChanged
    {
        public string Original { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        public Translation(string original) => this.Original = original;
        


        string translated = null;
        public string Translated
        {
            get
            {
                if (string.IsNullOrEmpty(translated)) translated = GetTranslation();
                return translated;
            }
            set
            {
                translated = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Translated"));
            }
        }

        string GetTranslation()
        {
            Translated = Translator.Translate(Original);
            return Translated;
        }
    }
}
