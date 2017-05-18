using CryPixivClient.Objects;
using CryPixivClient.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public partial class WorkDetails : Window
    {
        public PixivWork LoadedWork { get; }
        Dictionary<int, ImageSource> DownloadedImages = new Dictionary<int, ImageSource>();
        event EventHandler<ImageSource> ImageDownloaded;
        int currentPage = 1;

        public WorkDetails(PixivWork work)
        {
            InitializeComponent();
            SetWindow();
            LoadedWork = work;
            DataContext = this;

            this.Closing += WorkDetails_Closing;

            txtTitle.Text = LoadedWork.Title;
            SetPageStatus();
            txtPage.Text = $"{currentPage}/{LoadedWork.PageCount} ({DownloadedImages.Count})";


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
        }

        async Task DownloadImages()
        {
            SetProgressBar(true);
            for(int i = 0; i < LoadedWork.PageCount; i++)
            {
                if (isClosing) break;
                var img = await Task.Run(() => LoadedWork.GetImage(LoadedWork.GetImageUri(LoadedWork.ImageUrls.Large, i)));
                DownloadedImages.Add(i + 1, img);
                SetPageStatus();
                ImageDownloaded?.Invoke(this, img);
            }
            SetProgressBar(false);
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
                case Key.Enter:
                    Window_PreviewMouseDoubleClick(this, null);
                    break;
                case Key.Escape:
                    Close();
                    break;
            }
        }

        void SetProgressBar(bool show)
        {
            progressBar.Visibility = show ? Visibility.Visible : Visibility.Hidden;
        }
        void Window_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        void SetWindow()
        {
            if (Settings.Default.DetailWindowHeight == 0) return;

            Height = Settings.Default.DetailWindowHeight;
            Width = Settings.Default.DetailWindowWidth;
            Left = Settings.Default.DetailWindowLeft;
            Top = Settings.Default.DetailWindowTop;
        }

        bool isClosing = false;
        void WorkDetails_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.DetailWindowHeight = Height;
            Settings.Default.DetailWindowWidth = Width;
            Settings.Default.DetailWindowLeft = Left;
            Settings.Default.DetailWindowTop = Top;
            Settings.Default.Save();

            isClosing = true;
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
            MainWindow.MainModel.DownloadSelectedWorks(LoadedWork);
        }
    }
}
