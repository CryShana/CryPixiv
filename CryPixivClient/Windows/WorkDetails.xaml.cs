using CryPixivClient.Objects;
using CryPixivClient.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
                        // check cache
                        var uid = LoadedWork.User.Id.Value;
                        var artistImg = ArtistThumbnailsCache.GetFromCache(x => x.Item1 == uid);

                        if (artistImg == null)
                        {
                            // otherwise download it
                            var url = LoadedWork?.User?.ProfileImageUrls?.MainImage;
                            if (url == null) return null;
                            img = PixivWork.GetImage(url);

                            ArtistThumbnailsCache.Add(new Tuple<long, ImageSource>(uid, img), x => x.Item1 == uid);
                        }
                        else img = artistImg.First().Item2;
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
        static AsyncLocker locker = new AsyncLocker();

        public PixivWork LoadedWork { get; private set; }
        List<DownloadedImageData> DownloadedImages = new List<DownloadedImageData>();
        

        const int WorkCacheLimit = 6;
        static Cache<Tuple<long, DownloadedImageData>> PreviousDownloadsCache = new Cache<Tuple<long, DownloadedImageData>>(40);
        static Cache<Tuple<long, ImageSource>> ArtistThumbnailsCache = new Cache<Tuple<long, ImageSource>>(50);

        int currentPage = 1;

        public WorkDetails(PixivWork work)
        {
            InitializeComponent();
            // setup window
            SetWindow();
            if (wasMaximized) WindowState = WindowState.Maximized;

            // set datacontext
            DataContext = this;

            // setup event handlers
            this.Closing += WorkDetails_Closing;
            this.StateChanged += (a, b) => wasMaximized = WindowState == WindowState.Maximized;

            // load given work
            LoadWork(work);
        }

        bool openedCache = false;
        void LoadWork(PixivWork newWork, bool doAnimation = false)
        {
            timestamp = DateTime.Now; // used for doubleclicking

            // load work
            LoadedWork = newWork;

            // set to initial values
            currentPage = 1;
            DownloadedImages.Clear();
            mainImage.Source = null;
            img = null;

            // reset datacontext to refresh bindings
            DataContext = null; DataContext = this; 

            // load cached results if available
            var prevd = PreviousDownloadsCache.GetFromCache(x => x.Item1 == newWork.Id.Value);
            if (prevd != null) DownloadedImages = new List<DownloadedImageData>(prevd.Select(x => x.Item2).OrderBy(a => a.Page));
            
            // set work info
            comboTags.ItemsSource = GetTranslatedTags(LoadedWork.Tags);
            txtClipboard.Text = "Click on tag to copy it!";
            txtScore.Text = $"Score: {LoadedWork.Stats?.Score ?? LoadedWork.TotalBookmarks}";
            txtArtist.Text = LoadedWork.User.Name;
            txtResolution.Text = $"{LoadedWork.Width}x{LoadedWork.Height}";
            txtTitle.Text = LoadedWork.Title;
            Title = $"Work Details - ({LoadedWork.Id}) {LoadedWork.Title}";
            SetPageStatus();

            // set cached image if exists or use thumbnail
            if (DownloadedImages.Find(x => x.Page == 1) != null)
            {
                if (doAnimation) AnimateImageShift(() => SetImage(1));
                else SetImage(1);
            }
            else if (LoadedWork.img != null)
            {
                if (doAnimation) AnimateImageShift(() => mainImage.Source = LoadedWork.ImageThumbnail);
                else mainImage.Source = LoadedWork.ImageThumbnail;
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
            var data = DownloadedImages.Find(x => x.Page == currentPage);
            if (data != null)
            {
                var img = (BitmapImage)data.ImageData;

                txtResolution.Text = $"{img.PixelWidth}x{img.PixelHeight}";
            }
        }

        async Task DownloadImages()
        {
            if (DownloadedImages.Count >= LoadedWork.PageCount) return;

            var lworkid = LoadedWork.Id;
            using (await locker.LockAsync())
            {
                try
                {
                    if (lworkid != LoadedWork.Id) return;
                    SetProgressBar(true);

                    for (int i = 0; i < LoadedWork.PageCount; i++)
                    {
                        // if page is not yet downloaded, download it
                        ImageSource dimg = null;

                        var page = DownloadedImages.Find(x => x.Page == i + 1);
                        if (page == null)
                        {
                            // start downloading it - befor and after download - check if work has been switched
                            if (isClosing || lworkid != LoadedWork.Id) break;
                            dimg = await Task.Run(() => PixivWork.GetImage(LoadedWork.GetImageUri(LoadedWork.OriginalImageUrl, i)));
                            if (isClosing || lworkid != LoadedWork.Id) break;
                        }
                        else dimg = page.ImageData;

                        // cache image and update status
                        var dwImage = new DownloadedImageData(i + 1, dimg);
                        DownloadedImages.Add(dwImage);
                        CacheDownloads(lworkid.Value, dwImage);
                        SetPageStatus();
                        ImageDownloaded?.Invoke(this, dimg);
                    }
                }
                finally
                {
                    isClosing = false;
                    SetProgressBar(false);
                }
            }
        }

        public void SetImage(int page)
        {
            if (DownloadedImages.Count == 0 & page == 1) return;
            var img = DownloadedImages.Find(x => x.Page == page);
            if (img == null) return;

            currentPage = page;
            try
            {
                mainImage.Source = img.ImageData;
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

        void CacheDownloads(long wrkId, DownloadedImageData img) => PreviousDownloadsCache.Add(new Tuple<long, DownloadedImageData>(wrkId, img), x => x.Item1 == wrkId && x.Item2.Page == img.Page);        
        void btnInternet_Click(object sender, RoutedEventArgs e) => MainWindow.MainModel.OpenInBrowser(LoadedWork);      
        void btnBookmark_Click(object sender, RoutedEventArgs e) => MainWindow.MainModel.BookmarkWork(LoadedWork);       
        void DownloadSelected(object sender, RoutedEventArgs e) => MainWindow.MainModel.DownloadSelectedWorks(LoadedWork, true);       

        void txtArtist_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released) return;

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
            if (DownloadedImages.Count(x => x.Page == currentPage) == 0)
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
            var src = DownloadedImages.Find(x => x.Page == currentPage).ImageData as BitmapImage;

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

            await Task.Delay(200);
            callback();

            mainImage.BeginAnimation(OpacityProperty, opacityshow);
            mainImage.BeginAnimation(MarginProperty, movein);
        }

        public class DownloadedImageData
        {
            public int Page { get; set; }
            public ImageSource ImageData { get; set; }
            public DownloadedImageData(int page, ImageSource data)
            {
                Page = page;
                ImageData = data;
            }
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
