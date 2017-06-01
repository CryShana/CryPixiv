using CryPixivClient.Objects;
using CryPixivClient.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace CryPixivClient.Windows
{
    public partial class WorkDetails : Window
    {
        event EventHandler<ImageSource> ImageDownloaded;

        ImageSource img = null;
        public ImageSource ProfileImage
        {
            get
            {
                if (img == null)
                {
                    try
                    {
                        var url = LoadedWork?.User?.ProfileImageUrls?.MainImage;
                        if (url == null) return null;
                        img = PixivWork.GetImage(url);
                    }
                    catch
                    {
                        // failed to load
                        img = null;
                    }
                }

                return img;
            }
        }
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
        void LoadWork(PixivWork newWork, bool doAnimation = false)
        {
            timestamp = DateTime.Now;
            LoadedWork = newWork;
            mainImage.Source = null;

            currentPage = 1;
            DownloadedImages.Clear();
            img = null;
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

            // set thumbnail
            if (LoadedWork.img != null)
            {
                if (doAnimation) AnimateImageShift(() => mainImage.Source = LoadedWork.ImageThumbnail);
                else mainImage.Source = LoadedWork.ImageThumbnail;

            }
            if (DownloadedImages.Count >= 1)
            {
                if (doAnimation) AnimateImageShift(() => SetImage(1));
                else SetImage(1);
            }

            // start downloading images
            DownloadImages();

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

            var lworkid = LoadedWork.Id;
            await semaphore.WaitAsync();           
          
            try
            {
                if (lworkid != LoadedWork.Id) throw new Exception("Work changed!");
                SetProgressBar(true);

                for (int i = DownloadedImages.Count; i < LoadedWork.PageCount; i++)
                {
                    if (isClosing || lworkid != LoadedWork.Id) break;
                    var img = await Task.Run(() => PixivWork.GetImage(LoadedWork.GetImageUri(LoadedWork.OriginalImageUrl, i)));
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
            if (DownloadedImages.Count == 0 & page == 1) return;
            currentPage = page;
            try
            {
                mainImage.Source = DownloadedImages[page];
                SetPageStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Debug error: " + ex.Message);
            }
        }

        void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage + 1 > LoadedWork.PageCount.Value || currentPage + 1 > DownloadedImages.Count)
            {
                NextPost();
                return;
            }
            SetImage(currentPage + 1);
        }

        void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage - 1 <= 0)
            {
                PrevPost();
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
            LoadWork(result, true);
        }
        void PrevPost()
        {
            // open prev one
            var result = MainWindow.MainModel.OpenPrevWork(LoadedWork);
            if (result == null) return;
            LoadWork(result, true);
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
            MainWindow.currentWindow.Focus();
            MainWindow.ShowUserWork(LoadedWork.User);
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
            if (DownloadedImages.ContainsKey(currentPage) == false)
            {
                MessageBox.Show("Image is not yet fully loaded!", "Not loaded yet!", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var copyTd = new Thread(CopyImageToClipboard);
            copyTd.SetApartmentState(ApartmentState.STA);
            copyTd.Start();
        }

        public static Queue<string> CreatedTemporaryFiles = new Queue<string>();
        void CopyImageToClipboard()
        {
            var src = DownloadedImages[currentPage] as BitmapImage;

            string filename = $"{LoadedWork.Id.Value}_p{currentPage}.png";
            string path = Path.Combine(Path.GetTempPath(), filename);
            CreatedTemporaryFiles.Enqueue(path);

            //write the image to a temporary location (todo: purge it later)
            using (var fileStream = new FileStream(path, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(src));
                encoder.Save(fileStream);
            }

            //group image(s)
            var imgCollection = new System.Collections.Specialized.StringCollection();
            imgCollection.Add(path);

            //set up our clipboard data
            DataObject data = new DataObject();
            data.SetFileDropList(imgCollection);
            data.SetData("Bitmap", src);          // transparency is not preserved. STILL NEEDS A FIX
            data.SetData("Preferred DropEffect", DragDropEffects.Move);

            //push it all to the clipboard
            Clipboard.Clear();
            Clipboard.SetDataObject(data, true);
        }

        void CopyLink(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText($"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={LoadedWork.Id}");
        }

        public static List<Translation> GetTranslatedTags(List<string> tags)
        {
            var newTags = new List<Translation>();

            foreach (var t in tags) newTags.Add(new Translation(t));

            return newTags;
        }

        async void AnimateImageShift(System.Action callback)
        {
            var original = new Thickness(0, 31, 0, 115);
            var away = new Thickness(-155, 31, 155, 115);

            var opacityhide = new DoubleAnimation(0.0, TimeSpan.FromSeconds(0.2));
            var opacityshow = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.2));
            var moveaway = new ThicknessAnimation(away, TimeSpan.FromSeconds(0.2));
            var movein = new ThicknessAnimation(original, TimeSpan.FromSeconds(0.2));
            movein.EasingFunction = new PowerEase() { Power = 2 };

            mainImage.BeginAnimation(OpacityProperty, opacityhide);
            mainImage.BeginAnimation(MarginProperty, moveaway);

            await Task.Delay(300);
            callback();

            mainImage.BeginAnimation(OpacityProperty, opacityshow);
            mainImage.BeginAnimation(MarginProperty, movein);
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
                if (Original.Length > 150) return "";
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
