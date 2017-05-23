using CryPixivClient.Objects;
using CryPixivClient.Properties;
using CryPixivClient.ViewModels;
using CryPixivClient.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public partial class MainWindow : Window
    {
        public static MainWindow currentWindow;
        public static MainViewModel MainModel;
        public static PixivAccount Account = null;
        public static SynchronizationContext UIContext;

        public static bool LimitReached = false;
        public static int DynamicWorksLimit = 100;
        public const int DefaultWorksLimit = 100;
        public static PixivAccount.WorkMode CurrentWorkMode;
        public static CollectionViewSource MainCollectionView;
        public static CollectionViewSource MainCollectionViewSorted;
        public static CollectionViewSource MainCollectionViewRecommended;
        public static CollectionViewSource MainCollectionViewRanking;
        public static CollectionViewSource MainCollectionViewFollowing;
        public static CollectionViewSource MainCollectionViewBookmarks;
        public static CollectionViewSource MainCollectionViewBookmarksPrivate;
        public static CollectionViewSource MainCollectionViewUser;

        public MainWindow()
        {
            InitializeComponent();
            currentWindow = this;

            // set up all data
            MainModel = (MainViewModel)FindResource("mainViewModel");
            MainCollectionView = (CollectionViewSource)FindResource("ItemListViewSource");
            MainCollectionViewSorted = (CollectionViewSource)FindResource("ItemListViewSourceSorted");
            MainCollectionViewRecommended = (CollectionViewSource)FindResource("ItemListViewSourceRecommended");
            MainCollectionViewRanking = (CollectionViewSource)FindResource("ItemListViewSourceRanking");
            MainCollectionViewFollowing = (CollectionViewSource)FindResource("ItemListViewSourceFollowing");
            MainCollectionViewBookmarks = (CollectionViewSource)FindResource("ItemListViewSourceBookmarks");
            MainCollectionViewBookmarksPrivate = (CollectionViewSource)FindResource("ItemListViewSourceBookmarksPrivate");
            MainCollectionViewUser = (CollectionViewSource)FindResource("ItemListViewSourceUser");
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
            Panel.SetZIndex(mainList, (mode == PixivAccount.WorkMode.Search && checkPopular.IsChecked == false) ? 10 : 0);
            Panel.SetZIndex(mainListSorted, (mode == PixivAccount.WorkMode.Search && checkPopular.IsChecked == true) ? 10 : 0);

            Panel.SetZIndex(mainListRecommended, (mode == PixivAccount.WorkMode.Recommended) ? 10 : 0);

            Panel.SetZIndex(mainListRanking, (mode == PixivAccount.WorkMode.Ranking) ? 10 : 0);

            Panel.SetZIndex(mainListFollowing, (mode == PixivAccount.WorkMode.Following) ? 10 : 0);

            Panel.SetZIndex(mainListBookmarks, (mode == PixivAccount.WorkMode.BookmarksPublic) ? 10 : 0);

            Panel.SetZIndex(mainListBookmarksPrivate, (mode == PixivAccount.WorkMode.BookmarksPrivate) ? 10 : 0);

            Panel.SetZIndex(mainListUser, (mode == PixivAccount.WorkMode.User) ? 10 : 0);
        }

        void ShowLoginPrompt(bool force = false)
        {
            if (Account?.AuthDetails?.IsExpired == false && force == false) return;

            LoginWindow login = new LoginWindow(Account != null);
            login.ShowDialog();

            if (Account == null || Account.IsLoggedIn == false) { Environment.Exit(1); return; }

            SaveAccount();
            if(MainModel.DisplayedWorks_Ranking.Count > 0) MainModel.ForceRefreshImages();
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

            checkNSFW.IsChecked = Settings.Default.NSFW;
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
            Settings.Default.NSFW = checkNSFW.IsChecked == true;
            Settings.Default.WindowHeight = Height;
            Settings.Default.WindowWidth = Width;
            Settings.Default.WindowLeft = Left;
            Settings.Default.WindowTop = Top;
            Settings.Default.Save();

            Environment.Exit(1);
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
            ToggleButtons(PixivAccount.WorkMode.BookmarksPublic);
            ToggleLists(PixivAccount.WorkMode.BookmarksPublic);
            MainModel.ShowBookmarksPublic(); 
        }
        void btnBookmarksPrivate_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtons(PixivAccount.WorkMode.BookmarksPrivate);
            ToggleLists(PixivAccount.WorkMode.BookmarksPrivate);
            MainModel.ShowBookmarksPrivate();
        }
        void btnRecommended_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtons(PixivAccount.WorkMode.Recommended);
            ToggleLists(PixivAccount.WorkMode.Recommended);
            MainModel.ShowRecommended();
        }
        public static void ShowUserWork(long userId, string username)
        {
            if (userId <= 0 || CurrentWorkMode == PixivAccount.WorkMode.User)
                return;

            currentWindow.Dispatcher.Invoke(() =>
            {
                currentWindow.ToggleButtons(PixivAccount.WorkMode.User);
                currentWindow.ToggleLists(PixivAccount.WorkMode.User);
                MainModel.ShowUserWork(userId, username);
            });
        }

        bool searching = false;
        public static void SetSearchButtonState(bool isSearching)
        {
            UIContext.Send((a) =>
            {
                if (isSearching)
                {
                    currentWindow.btnSearch.Content = "Stop";
                    currentWindow.btnSearch.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#FFFFA5A5");
                }
                else
                {
                    currentWindow.btnSearch.Content = "Search";
                    currentWindow.btnSearch.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#FFDDDDDD");
                }
            }, null);
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

            searching = true;
            SetSearchButtonState(true);

            MainModel.ShowSearch(null, checkPopular.IsChecked == true, MainModel.CurrentPageResults);  // "null" as search query will attempt to use the previous query
        }
        void checkPopular_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWorkMode != PixivAccount.WorkMode.Search) return;
            ToggleLists(CurrentWorkMode);
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
            btnBookmarks.IsEnabled = mode != PixivAccount.WorkMode.BookmarksPublic;
            btnBookmarksPrivate.IsEnabled = mode != PixivAccount.WorkMode.BookmarksPrivate;
            btnFollowing.IsEnabled = mode != PixivAccount.WorkMode.Following;
            btnResults.IsEnabled = mode != PixivAccount.WorkMode.Search && MainModel.LastSearchQuery != null;
            btnRecommended.IsEnabled = mode != PixivAccount.WorkMode.Recommended;

            checkPopular.IsEnabled = mode == PixivAccount.WorkMode.Search;
        }

        public static List<PixivWork> GetSelectedWorks()
        {
            switch (CurrentWorkMode)
            {
                case PixivAccount.WorkMode.Search:
                    if (currentWindow.checkPopular.IsChecked == true) return currentWindow.mainListSorted.SelectedItems.Cast<PixivWork>().ToList();
                    return currentWindow.mainList.SelectedItems.Cast<PixivWork>().ToList();

                case PixivAccount.WorkMode.Ranking:
                    return currentWindow.mainListRanking.SelectedItems.Cast<PixivWork>().ToList();

                case PixivAccount.WorkMode.Following:
                    return currentWindow.mainListFollowing.SelectedItems.Cast<PixivWork>().ToList();

                case PixivAccount.WorkMode.BookmarksPublic:
                    return currentWindow.mainListBookmarks.SelectedItems.Cast<PixivWork>().ToList();

                case PixivAccount.WorkMode.BookmarksPrivate:
                    return currentWindow.mainListBookmarksPrivate.SelectedItems.Cast<PixivWork>().ToList();

                case PixivAccount.WorkMode.Recommended:
                    return currentWindow.mainListRecommended.SelectedItems.Cast<PixivWork>().ToList();

                case PixivAccount.WorkMode.User:
                    return currentWindow.mainListUser.SelectedItems.Cast<PixivWork>().ToList();
                default:
                    return null;
            }
        }

        public static CollectionViewSource GetCurrentCollectionViewSource()
        {
            switch (CurrentWorkMode)
            {
                case PixivAccount.WorkMode.Search:
                    if (currentWindow.checkPopular.IsChecked == true) return MainCollectionViewSorted;
                    else return MainCollectionView;

                case PixivAccount.WorkMode.Ranking:
                    return MainCollectionViewRanking;

                case PixivAccount.WorkMode.Following:
                    return MainCollectionViewFollowing;

                case PixivAccount.WorkMode.BookmarksPublic:
                    return MainCollectionViewBookmarks;

                case PixivAccount.WorkMode.BookmarksPrivate:
                    return MainCollectionViewBookmarksPrivate;

                case PixivAccount.WorkMode.Recommended:
                    return MainCollectionViewRecommended;

                case PixivAccount.WorkMode.User:
                    return MainCollectionViewUser;
                default:
                    return null;
            }
        }

        public static bool IsNSFWAllowed() => currentWindow.checkNSFW.IsChecked == true;

        private void mainListBookmarks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((ListView)sender).SelectedItems.Count == 0) return;
            var selected = ((ListView)sender).SelectedItem as PixivWork;
            if (IsNSFWAllowed() == false && selected.IsNSFW) return;

            MainModel.OpenCmd.Execute(selected);
        }

        private async void ResetResults_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This will reset the current Recommended results? You sure?", "Reset results", MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes)
            {
                await MainModel.ResetRecommended();
            }
        }

        void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This will log you out. Are you sure?", "Log out", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                == MessageBoxResult.Yes)
            {
                Settings.Default.Username = "";
                Settings.Default.AuthPassword = "";
                Settings.Default.Save();

                Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
            }
        }

        private void checkNSFW_Click(object sender, RoutedEventArgs e)
        {
            var collection = GetCurrentCollectionViewSource().Source as MyObservableCollection<PixivWork>;
            foreach (var i in collection) if(i.IsNSFW) i.UpdateNSFW();
        }
    }
}
