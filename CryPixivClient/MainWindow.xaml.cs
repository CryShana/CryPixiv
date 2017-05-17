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

        public static bool LimitReached = false;
        public static int DynamicWorksLimit = 100;
        public const int DefaultWorksLimit = 100;
        public static PixivAccount.WorkMode CurrentWorkMode;
        public static CollectionViewSource MainCollectionView;
        public static CollectionViewSource MainCollectionViewRanking;
        public static CollectionViewSource MainCollectionViewFollowing;
        public static CollectionViewSource MainCollectionViewBookmarks;

        public MainWindow()
        {
            InitializeComponent();

            // set up all data
            MainModel = (MainViewModel)FindResource("mainViewModel");
            MainCollectionView = (CollectionViewSource)FindResource("ItemListViewSource");
            MainCollectionViewRanking = (CollectionViewSource)FindResource("ItemListViewSourceRanking");
            MainCollectionViewFollowing = (CollectionViewSource)FindResource("ItemListViewSourceFollowing");
            MainCollectionViewBookmarks = (CollectionViewSource)FindResource("ItemListViewSourceBookmarks");
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

        void ToggleLists(PixivAccount.WorkMode mode)
        {
            mainList.IsEnabled = mode == PixivAccount.WorkMode.Search;
            mainList.Visibility = (mode == PixivAccount.WorkMode.Search) ? Visibility.Visible : Visibility.Hidden;

            mainListRanking.IsEnabled = mode == PixivAccount.WorkMode.Ranking;
            mainListRanking.Visibility = (mode == PixivAccount.WorkMode.Ranking) ? Visibility.Visible : Visibility.Hidden;

            mainListFollowing.IsEnabled = mode == PixivAccount.WorkMode.Following;
            mainListFollowing.Visibility = (mode == PixivAccount.WorkMode.Following) ? Visibility.Visible : Visibility.Hidden;

            mainListBookmarks.IsEnabled = mode == PixivAccount.WorkMode.Bookmarks;
            mainListBookmarks.Visibility = (mode == PixivAccount.WorkMode.Bookmarks) ? Visibility.Visible : Visibility.Hidden;
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
            ToggleLists(PixivAccount.WorkMode.Ranking);
            MainModel.ShowDailyRankings();
        }

        void btnFollowing_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtons(PixivAccount.WorkMode.Following);
            ToggleLists(PixivAccount.WorkMode.Following);
            MainModel.ShowFollowing();
        }

        void btnBookmarks_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtons(PixivAccount.WorkMode.Bookmarks);
            ToggleLists(PixivAccount.WorkMode.Bookmarks);
            MainModel.ShowBookmarks();
        }

        bool searching = false;
        void SetSearchButtonState(bool isSearching)
        {
            if (isSearching)
            {
                btnSearch.Content = "Stop";
                btnSearch.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#FFFFA5A5");
            }
            else
            {
                btnSearch.Content = "Search";
                btnSearch.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#FFDDDDDD");
            }
        }

        void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (searching)
            {
                searching = false;
                MainModel.CancelRunningSearches();
                SetSearchButtonState(false);
                return;
            }

            if (txtSearchQuery.Text.Length < 2) return;

            ToggleButtons(PixivAccount.WorkMode.Search);
            ToggleLists(PixivAccount.WorkMode.Search);
            UpdateSearchFilter(PixivAccount.WorkMode.Search, checkPopular.IsChecked == true);
            if (MainModel?.LastSearchQuery != txtSearchQuery.Text) MainModel.CurrentPageResults = 1;

            searching = true;
            SetSearchButtonState(true);

            MainModel.ShowSearch(txtSearchQuery.Text, checkPopular.IsChecked == true, MainModel.CurrentPageResults);
        }
        private void btnResults_Click(object sender, RoutedEventArgs e)
        {
            txtSearchQuery.Text = MainModel.LastSearchQuery;

            ToggleButtons(PixivAccount.WorkMode.Search);
            ToggleLists(PixivAccount.WorkMode.Search);
            UpdateSearchFilter(PixivAccount.WorkMode.Search, checkPopular.IsChecked == true);

            searching = true;
            SetSearchButtonState(true);

            MainModel.ShowSearch(null, checkPopular.IsChecked == true, MainModel.CurrentPageResults);  // "null" as search query will attempt to use the previous query
        }
        void checkPopular_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWorkMode != PixivAccount.WorkMode.Search) return;
            UpdateSearchFilter(PixivAccount.WorkMode.Search, checkPopular.IsChecked == true);
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
                if (LimitReached) DynamicWorksLimit += 30;
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

        void UpdateSearchFilter(PixivAccount.WorkMode mode, bool sort = false)
        {
            MainCollectionView.SortDescriptions.Clear();
            if (sort) MainCollectionView.SortDescriptions.Add(new System.ComponentModel.SortDescription("Stats.Score", System.ComponentModel.ListSortDirection.Descending));
        }
    }
}
