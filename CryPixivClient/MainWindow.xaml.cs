using CryPixivClient.Objects;
using CryPixivClient.Properties;
using CryPixivClient.ViewModels;
using CryPixivClient.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CryPixivClient
{
    public partial class MainWindow : Window
    {
        public static MainWindow currentWindow;
        public static MainViewModel MainModel;
        public static PixivAccount Account = null;
        public static SynchronizationContext UIContext;
        public static bool ShowingError = false;

        public static bool Paused = false;
        public static bool LimitReached = false;
        public static int DynamicWorksLimit = 100;
        public const int DefaultWorksLimit = 100;
        public const int SearchHistoryLimit = 25;
        public const string HistoryPath = "searchhistory.txt";
        public static PixivAccount.WorkMode CurrentWorkMode;
        public static CollectionViewSource MainCollectionViewSorted;
        public static CollectionViewSource MainCollectionViewRecommended;
        public static CollectionViewSource MainCollectionViewRanking;
        public static CollectionViewSource MainCollectionViewFollowing;
        public static CollectionViewSource MainCollectionViewBookmarks;
        public static CollectionViewSource MainCollectionViewBookmarksPrivate;
        public static CollectionViewSource MainCollectionViewUser;
        public static MyObservableCollection<string> SearchHistory = new MyObservableCollection<string>();
        public static bool IsClosing = false;

        public MainWindow()
        {
            InitializeComponent();
            currentWindow = this;

            // set up all data
            MainModel = (MainViewModel)FindResource("mainViewModel");
            MainCollectionViewSorted = (CollectionViewSource)FindResource("ItemListViewSourceSorted");
            MainCollectionViewRecommended = (CollectionViewSource)FindResource("ItemListViewSourceRecommended");
            MainCollectionViewRanking = (CollectionViewSource)FindResource("ItemListViewSourceRanking");
            MainCollectionViewFollowing = (CollectionViewSource)FindResource("ItemListViewSourceFollowing");
            MainCollectionViewBookmarks = (CollectionViewSource)FindResource("ItemListViewSourceBookmarks");
            MainCollectionViewBookmarksPrivate = (CollectionViewSource)FindResource("ItemListViewSourceBookmarksPrivate");
            MainCollectionViewUser = (CollectionViewSource)FindResource("ItemListViewSourceUser");
            UIContext = SynchronizationContext.Current;
            LoadSearchHistory();
            LoadWindowData();
            LoadAccount();
            SetupPopups();

            // events
            PixivAccount.AuthFailed += AuthenticationFailed;
            Scheduler<PixivWork>.JobFinished += this.SchedulerJobFinished;

            // start
            ShowLoginPrompt();
            btnDailyRankings_Click(this, null);
            this.Loaded += (a, b) => txtSearchQuery.Focus();
        }

        void ShowLoginPrompt(bool force = false)
        {
            if (Account?.AuthDetails?.IsExpired == false && force == false) return;

            LoginWindow login = new LoginWindow(Account != null);
            login.ShowDialog();

            if (Account == null || Account.IsLoggedIn == false) { Environment.Exit(1); return; }

            SaveAccount();
            if (MainModel.DisplayedWorks_Ranking.Count > 0) MainModel.ForceRefreshImages();
        }

        void AuthenticationFailed(object sender, string e)
        {
            UIContext.Send(async (a) => {
                ShowLoginPrompt(true);

                if (CurrentWorkMode == PixivAccount.WorkMode.Search)
                {
                    // needs to be pressed twice, because first time only stops existing searches that got stuck
                    btnSearch_Click(this, null);
                    await Task.Delay(500);
                    btnSearch_Click(this, null);
                }
            }, null);
        }

        void ToggleLists(PixivAccount.WorkMode mode)
        {
            mainListSorted.Visibility = (mode == PixivAccount.WorkMode.Search) ? Visibility.Visible : Visibility.Hidden;

            mainListRecommended.Visibility = (mode == PixivAccount.WorkMode.Recommended) ? Visibility.Visible : Visibility.Hidden;

            mainListRanking.Visibility = (mode == PixivAccount.WorkMode.Ranking) ? Visibility.Visible : Visibility.Hidden;

            mainListFollowing.Visibility = (mode == PixivAccount.WorkMode.Following) ? Visibility.Visible : Visibility.Hidden;

            mainListBookmarks.Visibility = (mode == PixivAccount.WorkMode.BookmarksPublic) ? Visibility.Visible : Visibility.Hidden;

            mainListBookmarksPrivate.Visibility = (mode == PixivAccount.WorkMode.BookmarksPrivate) ? Visibility.Visible : Visibility.Hidden;

            mainListUser.Visibility = (mode == PixivAccount.WorkMode.User) ? Visibility.Visible : Visibility.Hidden;

            if (mode != PixivAccount.WorkMode.User) followUserPopup.Hide(PopUp.TransitionType.ZoomIn);
            else followUserPopup.Show(PopUp.TransitionType.ZoomIn);
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

        void LoadSearchHistory()
        {
            if (File.Exists(HistoryPath) == false) { SearchHistory = new MyObservableCollection<string>(); return; }

            try
            {
                string[] content = File.ReadAllLines(HistoryPath);

                content.ToList().RemoveAll(x => x.Length == 0 || x == "\n" || x.Length > 200);
                if (content.Length > SearchHistoryLimit) content = content.Reverse().Skip(content.Length - SearchHistoryLimit).Reverse().ToArray();

                SearchHistory = new MyObservableCollection<string>(content);
            }
            catch
            {
                MessageBox.Show("Invalid history search file. Will be deleted after closing this message.", "Invalid History Search File", MessageBoxButton.OK, MessageBoxImage.Error);
                File.Delete(HistoryPath);
            }
        }

        void SaveSearchHistory()
        {
            if (File.Exists(HistoryPath)) File.Delete(HistoryPath);

            try
            {
                while (SearchHistory.Count > SearchHistoryLimit) SearchHistory.RemoveAt(SearchHistory.Count - 1);

                File.WriteAllLines(HistoryPath, SearchHistory);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save search history!\n\n" + ex.Message, "Failed to save!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsSearching)
            {
                if (MessageBox.Show("Are you sure you wish to terminate the search?", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Question)
                    == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            IsClosing = true;

            // save window data
            Settings.Default.NSFW = checkNSFW.IsChecked == true;
            Settings.Default.WindowHeight = Height;
            Settings.Default.WindowWidth = Width;
            Settings.Default.WindowLeft = Left;
            Settings.Default.WindowTop = Top;
            Settings.Default.Save();

            SaveSearchHistory();

            // clear temp directory if files exist
            while (WorkDetails.CreatedTemporaryFiles.Count > 0)
            {
                var f = WorkDetails.CreatedTemporaryFiles.Dequeue();
                if (File.Exists(f)) File.Delete(f);
            }

            Environment.Exit(1);
        }
        #endregion

        #region Main Buttons
        void ToggleButtons(PixivAccount.WorkMode mode)
        {
            btnRankings.IsEnabled = mode != PixivAccount.WorkMode.Ranking;
            btnBookmarks.IsEnabled = mode != PixivAccount.WorkMode.BookmarksPublic;
            btnBookmarksPrivate.IsEnabled = mode != PixivAccount.WorkMode.BookmarksPrivate;
            btnFollowing.IsEnabled = mode != PixivAccount.WorkMode.Following;
            btnResults.IsEnabled = mode != PixivAccount.WorkMode.Search && MainModel.LastSearchQuery != null;
            btnRecommended.IsEnabled = mode != PixivAccount.WorkMode.Recommended;
        }

        void btnDailyRankings_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtons(PixivAccount.WorkMode.Ranking);
            ToggleLists(PixivAccount.WorkMode.Ranking);
            MainModel.ShowRanking();
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
        void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            popupTags?.Hide();

            if (IsSearching)
            {
                MainModel.CancelRunningSearches();
                SetSearchButtonState(false);
                return;
            }

            if (txtSearchQuery.Text.Length < 2) return;

            ToggleButtons(PixivAccount.WorkMode.Search);
            ToggleLists(PixivAccount.WorkMode.Search);
            if (MainModel?.LastSearchQuery != txtSearchQuery.Text) MainModel.CurrentPageResults = 1;

            IsSearching = true;
            SetSearchButtonState(true);
            SearchHistory.Insert(0, txtSearchQuery.Text);
            MainModel.ShowSearch(txtSearchQuery.Text, checkPopular.IsChecked == true, MainModel.CurrentPageResults);
        }
        void btnResults_Click(object sender, RoutedEventArgs e)
        {
            txtSearchQuery.Text = MainModel.LastSearchQuery;

            ToggleButtons(PixivAccount.WorkMode.Search);
            ToggleLists(PixivAccount.WorkMode.Search);

            SetSearchButtonState(true);
            MainModel.ShowSearch(null, checkPopular.IsChecked == true, MainModel.CurrentPageResults);  // "null" as search query will attempt to use the previous query
        }
        async void checkPopular_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWorkMode == PixivAccount.WorkMode.Search && IsSearching)
            {
                // don't allow it to be checked when search in progress
                MessageBox.Show("Stop the search first!", "Stop searching", MessageBoxButton.OK, MessageBoxImage.Warning);
                checkPopular.IsChecked = !checkPopular.IsChecked;
                return;
            }

            // if same tag is entered and there are results - ask User...
            if (MainModel.LastSearchQuery == txtSearchQuery.Text && MainModel.DisplayedWorks_Results.Count > 0)
            {
                if (MessageBox.Show("This will reset the search. Are you sure?", "Reset search",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    checkPopular.IsChecked = !checkPopular.IsChecked;
                    return;
                }

                MainModel.LastSearchQuery = "";
                await MainModel.ResetSearchResults();
            }

            var view = MainCollectionViewSorted.View;
            using (view.DeferRefresh())
            {
                if (checkPopular.IsChecked == true)
                {
                    MainCollectionViewSorted.SortDescriptions.Clear();
                    MainCollectionViewSorted.SortDescriptions.Add(new SortDescription("Stats.Score", ListSortDirection.Descending));
                }
                else
                {
                    MainCollectionViewSorted.SortDescriptions.Clear();
                }
            }
        }
        async void ResetResults_Click(object sender, RoutedEventArgs e)
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
        void checkNSFW_Click(object sender, RoutedEventArgs e)
        {
            var collection = GetCurrentCollectionViewSource().Source as MyObservableCollection<PixivWork>;
            foreach (var i in collection) if (i.IsNSFW) i.UpdateNSFW();
        }
        #endregion

        #region Column Control / Scrollviewer
        void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MainModel.UpdateListMargins(this.ActualWidth);
            mainList_ScrollChanged(GetCurrentListView(), null);
        }

        void Window_StateChanged(object sender, EventArgs e) => Window_SizeChanged(this, null);

        bool scrolled = false;
        void mainList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Get the border of the listview(first child of a listview)
            Decorator border = VisualTreeHelper.GetChild((ListView)sender, 0) as Decorator;

            // Get scrollviewer
            ScrollViewer scrollViewer = border.Child as ScrollViewer;

            // speed up scrolling when scrolling with mouse
            bool mouseIsDown = Mouse.LeftButton == MouseButtonState.Pressed;
            if (e != null && scrolled == false && mouseIsDown == false)
            {
                scrolled = true;
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + e.VerticalChange * 3);
            }
            else scrolled = false;

            // how much further can it go until it asks for updates
            double pointForUpade = scrollViewer.ScrollableHeight * 0.7;
            double pointForUpade2 = scrollViewer.ScrollableHeight * 0.95;
            if (scrollViewer.VerticalOffset > pointForUpade)
            {
                if (LimitReached && CurrentWorkMode != PixivAccount.WorkMode.Search)
                {
                    // load more results in other workmodes
                    DynamicWorksLimit += 60;
                    LimitReached = false;
                }
            }
        }
        #endregion

        #region Static Methods
        public static List<PixivWork> GetSelectedWorks()
        {
            switch (CurrentWorkMode)
            {
                case PixivAccount.WorkMode.Search:
                    return currentWindow.mainListSorted.SelectedItems.Cast<PixivWork>().ToList();

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

        public static ListView GetCurrentListView()
        {
            switch (CurrentWorkMode)
            {
                case PixivAccount.WorkMode.Search:
                    return currentWindow.mainListSorted;

                case PixivAccount.WorkMode.Ranking:
                    return currentWindow.mainListRanking;

                case PixivAccount.WorkMode.Following:
                    return currentWindow.mainListFollowing;

                case PixivAccount.WorkMode.BookmarksPublic:
                    return currentWindow.mainListBookmarks;

                case PixivAccount.WorkMode.BookmarksPrivate:
                    return currentWindow.mainListBookmarksPrivate;

                case PixivAccount.WorkMode.Recommended:
                    return currentWindow.mainListRecommended;

                case PixivAccount.WorkMode.User:
                    return currentWindow.mainListUser;
                default:
                    return null;
            }
        }

        public static CollectionViewSource GetCurrentCollectionViewSource()
        {
            switch (CurrentWorkMode)
            {
                case PixivAccount.WorkMode.Search:
                    return MainCollectionViewSorted;

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

        static long currentUserId = -1;
        public async static void ShowUserWork(long userId, string username)
        {
            if (userId <= 0 || userId == currentUserId) return;
            else await MainModel.ResetUsers();

            currentUserId = userId;
            currentWindow.Dispatcher.Invoke(() =>
            {
                currentWindow.ToggleButtons(PixivAccount.WorkMode.User);
                currentWindow.ToggleLists(PixivAccount.WorkMode.User);
                MainModel.ShowUserWork(userId, username);
            });
        }

        public static bool IsSearching = false;
        public static void SetSearchButtonState(bool isSearching)
        {
            UIContext.Send((a) =>
            {
                if (isSearching)
                {
                    IsSearching = true;
                    Paused = false;
                    currentWindow.btnPause.Content = "Pause";
                    currentWindow.btnSearch.Content = "Stop";
                    currentWindow.btnSearch.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#FFFFA5A5");
                }
                else
                {
                    IsSearching = false;
                    currentWindow.btnSearch.Content = "Search";
                    currentWindow.btnSearch.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#FFDDDDDD");
                }
            }, null);
        }
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (var stream = client.OpenRead("http://www.google.com"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion

        void list_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((ListView)sender).SelectedItems.Count == 0) return;
            var selected = ((ListView)sender).SelectedItem as PixivWork;
            if (IsNSFWAllowed() == false && selected.IsNSFW) return;

            MainModel.OpenCmd.Execute(selected);
        }
        public void SchedulerJobFinished(Scheduler<PixivWork> sender, Tuple<PixivWork, Action> job,
            MyObservableCollection<PixivWork> associatedCollection)
        {
            if (sender.AssociatedWorkMode != CurrentWorkMode) return;
            bool isSearch = CurrentWorkMode == PixivAccount.WorkMode.Search;

            // get cache
            var cache = MainModel.GetCurrentCache();

            // set status text
            var toBeAdded = sender.ToAddCount;
            if (MainModel.Finished)
            {
                MainModel.Status = "Done. " + ((toBeAdded > 0) ? $"({toBeAdded} to be added)" : "");
            }
            else if (MainModel.IsWorking)
            {
                MainModel.Status = $"Searching... {associatedCollection.Count}" + ((isSearch) ? $"{((MainModel.MaxResults == -1) ? "" : $"/{MainModel.MaxResults}")}" : "") + $" ({toBeAdded} to be added)";
            }
            else if (LimitReached)
            {
                MainModel.Status = "Limit reached. Scroll for more. " + ((toBeAdded > 0) ? $" ({toBeAdded} to be added)" : "");
            }
            else
            {
                MainModel.Status = "Idle. " + ((toBeAdded > 0) ? $" ({toBeAdded} to be added)" : "");
            }

            MainModel.CollectionStatus = $"Found {cache.Count} items.";
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWorkMode != PixivAccount.WorkMode.Search) return;

            if (Paused == false)
            {
                Paused = true;
                btnPause.Content = "Continue";
                MainModel.Status = "Paused.";
                MainModel.CancelRunningSearches();
                SetSearchButtonState(false);
            }
            else
            {
                SetSearchButtonState(true);
                btnSearch_Click(this, null);
            }
        }

        void SetupPopups()
        {
            // Sets up the History Search pop up
            var history = new List<string>();

            TextBlock txt = new TextBlock()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(20, 18, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Text = "Search History",
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = System.Windows.Media.Brushes.Gray
            };

            ListBox lstBox = new ListBox()
            {
                Margin = new Thickness(20, 39, 18, 19),
                BorderBrush = null,
            };

            lstBox.ItemTemplate = (DataTemplate)FindResource("searchHistoryTemplate");
            SearchHistory.CollectionChanged += (a, b) => { lstBox.ItemsSource = WorkDetails.GetTranslatedTags(SearchHistory.ToList()); };
            lstBox.ItemsSource = WorkDetails.GetTranslatedTags(SearchHistory.ToList());
            lstBox.SelectionChanged += (a, b) =>
            {
                var tt = lstBox.SelectedItem as Translation;
                if (tt != null) txtSearchQuery.Text = tt.Original;
            };

            popupTags.AddContent(txt, lstBox);

            // Will also need to set up the "Follow User" popup here... (maybe even a NSFW checkbox popup for further customization)
            followUserPopup.SetArrow(PopUp.ArrowPosition.None);
        }


        void txtSearchQuery_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = txtSearchQuery.Text;

            if (text.Length == 0) popupTags?.Hide();
            else popupTags?.Show();
        }

        void txtSearchQuery_LostFocus(object sender, RoutedEventArgs e) => popupTags?.Hide();

        void txtSearchQuery_GotFocus(object sender, RoutedEventArgs e) => txtSearchQuery_TextChanged(this, null);

        void popupTags_MouseLeave(object sender, MouseEventArgs e) => popupTags?.Hide();

        void list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var list = GetCurrentListView();
            MainModel.SelectedStatus = $"{list.SelectedItems.Count} selected";
        }

        #region DailyRanking Context Menu
        void DailyClick(object sender, RoutedEventArgs e)
        {
            MainModel.SwitchRankingType(RankingType.Day);
            btnRankings.Content = "Daily Ranking";            
        }

        void WeeklyClick(object sender, RoutedEventArgs e)
        {
            MainModel.SwitchRankingType(RankingType.Week);
            btnRankings.Content = "Weekly Ranking";
        }

        void MonthlyClick(object sender, RoutedEventArgs e)
        {
            MainModel.SwitchRankingType(RankingType.Month);
            btnRankings.Content = "Monthly Ranking";
        }

        void ForMalesClick(object sender, RoutedEventArgs e)
        {
            MainModel.SwitchRankingType(RankingType.Day_Male);
            btnRankings.Content = "Male Ranking";
        }

        void ForFemalesClick(object sender, RoutedEventArgs e)
        {
            MainModel.SwitchRankingType(RankingType.Day_Female);
            btnRankings.Content = "Female Ranking";
        }

        void Daily18Click(object sender, RoutedEventArgs e)
        {
            MainModel.SwitchRankingType(RankingType.Day_R18);
            btnRankings.Content = "Daily R-18";
        }

        void Weekly18Click(object sender, RoutedEventArgs e)
        {
            MainModel.SwitchRankingType(RankingType.Week_R18);
            btnRankings.Content = "Weekly R-18";
        }
        #endregion
    }
}
