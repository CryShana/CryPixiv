using CryPixivClient.Properties;
using CryPixivClient.ViewModels;
using CryPixivClient.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CryPixivClient
{
    class MeMe
    {
        public int Value { get; set; }
    }
    public partial class MainWindow : Window
    {
        public static MainViewModel MainModel;
        public static PixivAccount Account = null;
        public static SynchronizationContext UIContext;

        public static int DynamicWorksLimit = 100;
        public const int DefaultWorksLimit = 100;
        public static PixivAccount.WorkMode CurrentWorkMode;
        public static CollectionViewSource MainCollectionView;

        public MainWindow()
        {
            InitializeComponent();

            // set up all data
            MainModel = (MainViewModel)FindResource("mainViewModel");
            MainCollectionView = (CollectionViewSource)FindResource("ItemListViewSource");
            UIContext = SynchronizationContext.Current;
            LoadWindowData();
            LoadAccount();

            // events
            PixivAccount.AuthFailed += AuthenticationFailed;
            
            // start
            ShowLoginPrompt();
            btnDailyRankings_Click(this, null);
            this.Loaded += (a, b) => txtSearchQuery.Focus();
        }

        private void AuthenticationFailed(object sender, string e)
        {
            UIContext.Send((a) => ShowLoginPrompt(true), null);
        }

        void ShowLoginPrompt(bool force = false)
        {
            if (Account?.AuthDetails?.IsExpired == false && force == false) return;

            LoginWindow login = new LoginWindow(Account != null);
            login.ShowDialog();

            if (Account == null || Account.IsLoggedIn == false) { Environment.Exit(1); return; }

            SaveAccount();
        }

        #region Saving/Loading

        void LoadWindowData()
        {
            if (Settings.Default.WindowHeight > 10)
            {
                Height = Settings.Default.WindowHeight;
                Width = Settings.Default.WindowWidth;
                Left = Settings.Default.WindowLeft;
                Top = Settings.Default.WindowTop;
            }
        }

        void LoadAccount()
        {
            if (Settings.Default.Username.Length < Settings.Default.MinUsernameLength) return;
            Account = new PixivAccount(Settings.Default.Username);

            Account.LoginWithAccessToken(
                Settings.Default.AuthAccessToken,
                Settings.Default.AuthRefreshToken,
                Settings.Default.AuthExpiresIn,
                DateTime.Parse(Settings.Default.AuthIssued));
        }

        void SaveAccount()
        {
            // save account data
            Settings.Default.Username = Account.Username;
            Settings.Default.AuthIssued = Account.AuthDetails.TimeIssued.ToString();
            Settings.Default.AuthExpiresIn = Account.AuthDetails.ExpiresIn ?? 0;
            Settings.Default.AuthAccessToken = Account.AuthDetails.AccessToken;
            Settings.Default.AuthRefreshToken = Account.AuthDetails.RefreshToken;
            Settings.Default.Save();
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // save window data
            Settings.Default.WindowHeight = Height;
            Settings.Default.WindowWidth = Width;
            Settings.Default.WindowLeft = Left;
            Settings.Default.WindowTop = Top;
            Settings.Default.Save();
        }
        #endregion

        #region Main Buttons
        void btnDailyRankings_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtons(PixivAccount.WorkMode.Ranking);
            PrepareFilter(PixivAccount.WorkMode.Ranking);
            MainModel.ShowDailyRankings(); 
        }

        void btnFollowing_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtons(PixivAccount.WorkMode.Following);
            PrepareFilter(PixivAccount.WorkMode.Following);
            MainModel.ShowFollowing();
        }

        void btnBookmarks_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtons(PixivAccount.WorkMode.Bookmarks);
            PrepareFilter(PixivAccount.WorkMode.Bookmarks);
            MainModel.ShowBookmarks();
        }

        void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (txtSearchQuery.Text.Length < 2) return;

            ToggleButtons(PixivAccount.WorkMode.Search);
            PrepareFilter(PixivAccount.WorkMode.Search, checkPopular.IsChecked == true);
            if (MainModel?.LastSearchQuery != txtSearchQuery.Text) MainModel.CurrentPageResults = 1;

            MainModel.ShowSearch(txtSearchQuery.Text, checkPopular.IsChecked == true, MainModel.CurrentPageResults);
        }
        private void btnResults_Click(object sender, RoutedEventArgs e)
        {
            txtSearchQuery.Text = MainModel.LastSearchQuery;

            ToggleButtons(PixivAccount.WorkMode.Search);
            PrepareFilter(PixivAccount.WorkMode.Search, checkPopular.IsChecked == true);
            MainModel.ShowSearch(null, checkPopular.IsChecked == true, MainModel.CurrentPageResults);  // "null" as search query will attempt to use the previous query
        }
        void checkPopular_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWorkMode != PixivAccount.WorkMode.Search) return;
            PrepareFilter(PixivAccount.WorkMode.Search, checkPopular.IsChecked == true);
        }
        #endregion

        #region Column display control
        void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MainModel.UpdateColumns(ActualWidth - 20);
            mainList_ScrollChanged(mainList, null);
        }

        void Window_StateChanged(object sender, EventArgs e) => Window_SizeChanged(this, null);

        void mainList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Get the border of the listview(first child of a listview)
            Decorator border = VisualTreeHelper.GetChild((ListView)sender, 0) as Decorator;

            // Get scrollviewer
            ScrollViewer scrollViewer = border.Child as ScrollViewer;

            // how much further can it go until it asks for updates
            double pointForUpade = scrollViewer.ScrollableHeight * 0.7;
            if (scrollViewer.VerticalOffset > pointForUpade)
            {
                // update it
                DynamicWorksLimit += 30;
            }
        }
        #endregion

        void ToggleButtons(PixivAccount.WorkMode mode)
        {
            btnDailyRankings.IsEnabled = mode != PixivAccount.WorkMode.Ranking;
            btnBookmarks.IsEnabled = mode != PixivAccount.WorkMode.Bookmarks;
            btnFollowing.IsEnabled = mode != PixivAccount.WorkMode.Following;
            btnResults.IsEnabled = mode != PixivAccount.WorkMode.Search && MainModel.LastSearchQuery != null;
        }

        void PrepareFilter(PixivAccount.WorkMode mode, bool sort = false)
        {
            MainCollectionView.SortDescriptions.Clear();
            if (sort) MainCollectionView.SortDescriptions.Add(new System.ComponentModel.SortDescription("Stats.Score", System.ComponentModel.ListSortDirection.Descending));

            // do stuff here...
            switch (mode)
            {
                case PixivAccount.WorkMode.Search:
                    break;
                case PixivAccount.WorkMode.Ranking:
                    // sort this by ACTUAL ORDER - maybe don't keep backup at all
                    break;
                case PixivAccount.WorkMode.Bookmarks:
                    // sort this by ORDER ADDED TO BOOKMARK
                    // refresh existing ones - ALWAYS CHECK HOW EXISTING ONES ARE CHECKED - check properties, not just Id
                    break;
                case PixivAccount.WorkMode.Following:
                    MainCollectionView.SortDescriptions.Add(new System.ComponentModel.SortDescription("CreatedTime", System.ComponentModel.ListSortDirection.Descending));
                    break;
            }
        }
    }
}
