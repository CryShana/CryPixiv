using CryPixivClient.Commands;
using CryPixivClient.Objects;
using CryPixivClient.Properties;
using CryPixivClient.Windows;
using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace CryPixivClient.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public const int DefaultPerPage = 30;
        public const int ListViewItemWidth = 250;
        public void Changed([CallerMemberName]string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #region Private fields
        MyObservableCollection<PixivWork> displayedWorks_Results = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_Ranking = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_Following = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_Bookmarks = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_BookmarksPrivate = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_Recommended = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_User = new MyObservableCollection<PixivWork>();

        readonly SemaphoreSlim semaphore;

        string status = "Idle";
        string collectionstatus = "-";
        string title = "CryPixiv";
        Thickness margin = new Thickness(0, 94, 0, 22);
        bool isWorking = false;
        string titleSuffix = "";
        List<PixivWork> dailyRankings = new List<PixivWork>();
        List<PixivWork> bookmarks = new List<PixivWork>();
        List<PixivWork> bookmarksprivate = new List<PixivWork>();
        List<PixivWork> following = new List<PixivWork>();
        List<PixivWork> results = new List<PixivWork>();
        List<PixivWork> recommended = new List<PixivWork>();
        List<PixivWork> user = new List<PixivWork>();
        int currentPageResults = 1;
        SynchronizationContext UIContext;

        ICommand bookmarkcmd;
        ICommand openbrowsercmd;
        ICommand opencmd;
        ICommand downloadselectedcmd;
        #endregion

        #region Properties
        public Func<PixivWork, PixivWork, bool> PixivWorkEqualityComparer = (a, b) => a.Id.Value == b.Id.Value;
        public Func<PixivWork, long> PixivIdGetter = a => a.Id ?? -1;
        public Scheduler<PixivWork> Scheduler_DisplayedWorks_Results { get; private set; }
        public Scheduler<PixivWork> Scheduler_DisplayedWorks_Ranking { get; private set; }
        public Scheduler<PixivWork> Scheduler_DisplayedWorks_Following { get; private set; }
        public Scheduler<PixivWork> Scheduler_DisplayedWorks_Bookmarks { get; private set; }
        public Scheduler<PixivWork> Scheduler_DisplayedWorks_BookmarksPrivate { get; private set; }
        public Scheduler<PixivWork> Scheduler_DisplayedWorks_Recommended { get; private set; }
        public Scheduler<PixivWork> Scheduler_DisplayedWorks_User { get; private set; }

        public MyObservableCollection<PixivWork> DisplayedWorks_Results
        {
            get => displayedWorks_Results;
            set { displayedWorks_Results = value; Changed(); }
        }
        public MyObservableCollection<PixivWork> DisplayedWorks_Ranking
        {
            get => displayedWorks_Ranking;
            set { displayedWorks_Ranking = value; Changed(); }
        }
        public MyObservableCollection<PixivWork> DisplayedWorks_Following
        {
            get => displayedWorks_Following;
            set { displayedWorks_Following = value; Changed(); }
        }
        public MyObservableCollection<PixivWork> DisplayedWorks_Bookmarks
        {
            get => displayedWorks_Bookmarks;
            set { displayedWorks_Bookmarks = value; Changed(); }
        }
        public MyObservableCollection<PixivWork> DisplayedWorks_Recommended
        {
            get => displayedWorks_Recommended;
            set { displayedWorks_Recommended = value; Changed(); }
        }
        public MyObservableCollection<PixivWork> DisplayedWorks_BookmarksPrivate
        {
            get => displayedWorks_BookmarksPrivate;
            set { displayedWorks_BookmarksPrivate = value; Changed(); }
        }
        public MyObservableCollection<PixivWork> DisplayedWorks_User
        {
            get => displayedWorks_User;
            set { displayedWorks_User = value; Changed(); }
        }

        public int CurrentPageResults { get => currentPageResults; set { currentPageResults = value; } }
        public string Status
        {
            get => status;
            set { status = value; Changed(); }
        }
        public string CollectionStatus
        {
            get => collectionstatus;
            set { collectionstatus = value; Changed(); }
        }
        public Thickness ListMargin
        {
            get
            {
                return margin;
            }
            set
            {
                margin = value; Changed();
            }
        }
        public bool IsWorking
        {
            get => isWorking;
            set { isWorking = value; Changed(); }
        }

        public string TitleSuffix
        {
            get => titleSuffix;
            set { titleSuffix = value; Changed("Title"); }
        }

        public string Title => title + (string.IsNullOrEmpty(titleSuffix) ? "" : " - " + titleSuffix);

        public string LastSearchQuery { get; set; }
        public int MaxResults { get; private set; }
        public bool Finished { get; private set; }
        #endregion

        #region Commands
        public ICommand BookmarkCmd => bookmarkcmd ?? (bookmarkcmd = new RelayCommand<PixivWork>(BookmarkWork));
        public ICommand OpenBrowserCmd => openbrowsercmd ?? (openbrowsercmd = new RelayCommand<PixivWork>(OpenInBrowser));
        public ICommand OpenCmd => opencmd ?? (opencmd = new RelayCommand<PixivWork>(OpenWork));
        public ICommand DownloadSelectedCmd => downloadselectedcmd ?? (downloadselectedcmd = new RelayCommand<PixivWork>((w) => DownloadSelectedWorks(w)));
        #endregion

        public MainViewModel()
        {
            UIContext = SynchronizationContext.Current;
            semaphore = new SemaphoreSlim(1);

            Scheduler_DisplayedWorks_Results = new Scheduler<PixivWork>(ref displayedWorks_Results, PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.Search);
            Scheduler_DisplayedWorks_Ranking = new Scheduler<PixivWork>(ref displayedWorks_Ranking, PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.Ranking);
            Scheduler_DisplayedWorks_Following = new Scheduler<PixivWork>(ref displayedWorks_Following, PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.Following);
            Scheduler_DisplayedWorks_Recommended = new Scheduler<PixivWork>(ref displayedWorks_Recommended, PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.Recommended);
            Scheduler_DisplayedWorks_Bookmarks = new Scheduler<PixivWork>(ref displayedWorks_Bookmarks, PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.BookmarksPublic);
            Scheduler_DisplayedWorks_BookmarksPrivate = new Scheduler<PixivWork>(ref displayedWorks_BookmarksPrivate, PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.BookmarksPrivate);
            Scheduler_DisplayedWorks_User = new Scheduler<PixivWork>(ref displayedWorks_User, PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.User);
        }

        #region Show Methods
        public async Task Show(List<PixivWork> cache, MyObservableCollection<PixivWork> displayCollection,
            PixivAccount.WorkMode mode, string titleSuffix, Func<int, Task<List<PixivWork>>> getWorks,
            Scheduler<PixivWork> scheduler, bool waitForUser = true, bool fixInvalid = true)
        {
            // set starting values
            MainWindow.CurrentWorkMode = mode;
            MainWindow.DynamicWorksLimit = MainWindow.DefaultWorksLimit;

            CancelRunningSearches();

            // show status
            TitleSuffix = titleSuffix;
            Finished = false;
            Status = "Preparing to get data...";
            CollectionStatus = "-";

            var csrc = new CancellationTokenSource();
            queuedSearches.Enqueue(csrc);

            try
            {
                // start searching...
                await Task.Run(async () =>
                {
                    cache.Clear();
                    int currentPage = 0;
                    bool lastWasStuck = false;
                    for (;;)
                    {
                        if (MainWindow.CurrentWorkMode != mode || csrc.IsCancellationRequested) break;  // if user changes mode - break;

                        // if limit exceeded, stop downloading until user scrolls
                        if (MainWindow.DynamicWorksLimit < cache.Count && waitForUser && cache.Count >= displayCollection.Count)
                        {
                            MainWindow.LimitReached = true;
                            UIContext.Post(a => MainWindow.currentWindow.SchedulerJobFinished(scheduler, null, displayCollection), null);
                            lastWasStuck = true;
                            IsWorking = false;
                            await Task.Delay(200);
                            continue;
                        }
                        else
                        {
                            if (lastWasStuck)
                            {
                                Status = $"Continuing...";
                                lastWasStuck = false;
                            }
                        }

                        try
                        {
                            // start downloading next page
                            IsWorking = true;
                            currentPage++;

                            // download current page
                            var works = await getWorks(currentPage);

                            if (works == null || MainWindow.CurrentWorkMode != mode
                                || works.Count == 0 || csrc.IsCancellationRequested) break;

                            // start NUMBERIN
                            if (mode != PixivAccount.WorkMode.Recommended) works.AssignOrderToWorks(currentPage, DefaultPerPage);

                            cache.AddRange(works);
                            UIContext.Send(async (a) =>
                            {
                                await semaphore.WaitAsync();
                                displayCollection.UpdateWith(works, scheduler, fixInvalid);
                                semaphore.Release();
                            }, null);
                        }
                        catch (Exception ex)
                        {
                            break;
                        }
                    }

                    if (MainWindow.CurrentWorkMode == mode)
                    {
                        IsWorking = false;
                        Finished = true;
                    }
                });
            }
            catch (TaskCanceledException tex)
            {
                // ignore
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Download Error: " + ex.Message, "Download Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        const int InitialRetries = 4;
        int retries = 4;
        public async void ShowSearch(string query, bool autosort = true, int continuePage = 1)
        {
            bool otherWasRunning = LastSearchQuery != query && query != null;

            if (query == null) query = LastSearchQuery;
            MaxResults = -1;
            Finished = false;
            retries = InitialRetries;
            LastSearchQuery = query;

            CancelRunningSearches();

            // set starting values
            var mode = PixivAccount.WorkMode.Search;
            MainWindow.CurrentWorkMode = mode;

            // show status
            TitleSuffix = "";
            Status = "Preparing to get data...";
            CollectionStatus = "-";

            // load cached results if they exist
            if (otherWasRunning) await ResetSearchResults();

            var csrc = new CancellationTokenSource();
            queuedSearches.Enqueue(csrc);

            try
            {
                // start searching...
                await Task.Run(async () =>
                {
                    int currentPage = continuePage - 1;
                    for (;;)
                    {
                        if (MainWindow.CurrentWorkMode != mode || csrc.IsCancellationRequested) break; // if user changes mode or requests task to be cancelled - break;
                                                                                                       // check if max results reached
                        if (MaxResults != -1 && MaxResults <= results.Count) break;

                        try
                        {
                            // start downloading next page
                            IsWorking = true;
                            currentPage++;

                            // download current page
                            var works = await MainWindow.Account.SearchWorks(query, currentPage);
                            if (MainWindow.CurrentWorkMode != mode || csrc.IsCancellationRequested) break;
                            if (works == null || works.Count == 0)
                            {
                                if (DisplayedWorks_Results.Count < MaxResults && MainWindow.ShowingError == false)
                                {
                                    Status = "Retrying...";
                                    currentPage--;
                                    retries--;
                                    if (retries <= 0) break;
                                    continue;
                                }
                                else break;
                            }
                            if (MaxResults == -1) MaxResults = works.Pagination.Total ?? 0;

                            var wworks = works.ToPixivWork();
                            results.AddToList(wworks);

                            UIContext.Send(async (a) =>
                            {
                                await semaphore.WaitAsync();
                                DisplayedWorks_Results.UpdateWith(wworks, Scheduler_DisplayedWorks_Results, false);
                                semaphore.Release();
                            }, null);

                            currentPageResults = currentPage;
                        }
                        catch (Exception ex)
                        {
                            break;
                        }
                    }

                    if (MainWindow.CurrentWorkMode == mode)
                    {
                        IsWorking = false;
                        MainWindow.SetSearchButtonState(false);
                        UIContext.Post(a => MainWindow.currentWindow.SchedulerJobFinished(
                                Scheduler_DisplayedWorks_Results, null, displayedWorks_Results), null);

                        Finished = true;
                    }
                }, csrc.Token);
            }
            catch (TaskCanceledException tex)
            {
                // ignore
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Download Error: " + ex.Message, "Download Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Show Method Callers
        public async void ShowDailyRankings() =>
            await Show(dailyRankings, DisplayedWorks_Ranking, PixivAccount.WorkMode.Ranking, "Daily Ranking",
                (page) => MainWindow.Account.GetDailyRanking(page), Scheduler_DisplayedWorks_Ranking);

        public async void ShowFollowing() =>
            await Show(following, DisplayedWorks_Following, PixivAccount.WorkMode.Following, "Following",
                (page) => MainWindow.Account.GetFollowing(page), Scheduler_DisplayedWorks_Following);

        public async void ShowBookmarksPublic() =>
            await Show(bookmarks, DisplayedWorks_Bookmarks, PixivAccount.WorkMode.BookmarksPublic, "Bookmarks",
                (page) => MainWindow.Account.GetBookmarks(page, PixivAccount.Publicity.Public), Scheduler_DisplayedWorks_Bookmarks);

        public async void ShowBookmarksPrivate() =>
            await Show(bookmarksprivate, DisplayedWorks_BookmarksPrivate, PixivAccount.WorkMode.BookmarksPrivate, "Private Bookmarks",
                (page) => MainWindow.Account.GetBookmarks(page, PixivAccount.Publicity.Private), Scheduler_DisplayedWorks_BookmarksPrivate);

        public async void ShowRecommended() =>
            await Show(recommended, DisplayedWorks_Recommended, PixivAccount.WorkMode.Recommended, "Recommended",
                (page) => MainWindow.Account.GetRecommended(page), Scheduler_DisplayedWorks_Recommended, fixInvalid: false);
        public async void ShowUserWork(long userId, string username) =>
            await Show(user, DisplayedWorks_User, PixivAccount.WorkMode.User, "User work - " + username,
                (page) => MainWindow.Account.GetUserWorks(userId, page), Scheduler_DisplayedWorks_User);

        #endregion

        #region Other Methods
        public void UpdateListMargins(double actualWidth)
        {
            // MANUAL VIRTUALIZINGTILEPANEL CENTERING
            // (0, 94, 0, 22) is the default margin
            actualWidth -= 30;
            int itemsPerRow = (int)(actualWidth / ListViewItemWidth);
            double toAdd = actualWidth - ListViewItemWidth * itemsPerRow;
            ListMargin = new Thickness((toAdd / 2.0) - 7, 94, 0, 22);
        }
        Queue<CancellationTokenSource> queuedSearches = new Queue<CancellationTokenSource>();
        public void CancelRunningSearches()
        {
            // cancel other running searches
            while (queuedSearches.Count != 0)
            {
                var dq = queuedSearches.Dequeue();
                if (dq.IsCancellationRequested) continue;
                else dq.Cancel();
            }
        }
        public async Task ResetSearchResults()
        {
            await semaphore.WaitAsync();
            await Task.Run(() => results.Clear());

            Finished = false;
            MainWindow.LimitReached = false;
            DisplayedWorks_Results = new MyObservableCollection<PixivWork>();
            Scheduler_DisplayedWorks_Results.Stop();
            Scheduler_DisplayedWorks_Results = new Scheduler<PixivWork>(ref displayedWorks_Results, PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.Search);

            semaphore.Release();
        }
        public async Task ResetRecommended(bool autoshow = true)
        {
            bool wasRecommended = MainWindow.CurrentWorkMode == PixivAccount.WorkMode.Recommended;

            await semaphore.WaitAsync();
            recommended = new List<PixivWork>();
            DisplayedWorks_Recommended = new MyObservableCollection<PixivWork>();
            Scheduler_DisplayedWorks_Recommended.Stop();
            Scheduler_DisplayedWorks_Recommended = new Scheduler<PixivWork>(ref displayedWorks_Recommended,
                PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.Recommended, UIContext);
            semaphore.Release();

            if (wasRecommended && autoshow) ShowRecommended();
        }
        public async Task ResetUsers()
        {
            await semaphore.WaitAsync();
            user = new List<PixivWork>();
            DisplayedWorks_User = new MyObservableCollection<PixivWork>();
            Scheduler_DisplayedWorks_User.Stop();
            Scheduler_DisplayedWorks_User = new Scheduler<PixivWork>(ref displayedWorks_User,
                PixivWorkEqualityComparer, PixivIdGetter, PixivAccount.WorkMode.User, UIContext);
            semaphore.Release();
        }
        public void ForceRefreshImages()
        {
            // refresh all images depending on current mode
            switch (MainWindow.CurrentWorkMode)
            {
                case PixivAccount.WorkMode.Ranking:
                    UpdateImages(DisplayedWorks_Ranking);
                    break;
                case PixivAccount.WorkMode.Search:
                    UpdateImages(displayedWorks_Results);
                    break;
                case PixivAccount.WorkMode.Following:
                    UpdateImages(DisplayedWorks_Following);
                    break;
                case PixivAccount.WorkMode.BookmarksPublic:
                    UpdateImages(DisplayedWorks_Bookmarks);
                    break;
                case PixivAccount.WorkMode.BookmarksPrivate:
                    UpdateImages(DisplayedWorks_BookmarksPrivate);
                    break;
                case PixivAccount.WorkMode.Recommended:
                    UpdateImages(DisplayedWorks_Recommended);
                    break;
                case PixivAccount.WorkMode.User:
                    UpdateImages(DisplayedWorks_User);
                    break;
            }
        }
        void UpdateImages(MyObservableCollection<PixivWork> collection)
        {
            foreach (var i in collection) if (i.img == null) i.UpdateThumbnail();
        }
        public List<PixivWork> GetCurrentCache()
        {
            switch (MainWindow.CurrentWorkMode)
            {
                case PixivAccount.WorkMode.Search:
                    return results;

                case PixivAccount.WorkMode.Ranking:
                    return dailyRankings;

                case PixivAccount.WorkMode.Following:
                    return following;

                case PixivAccount.WorkMode.BookmarksPublic:
                    return bookmarks;

                case PixivAccount.WorkMode.BookmarksPrivate:
                    return bookmarksprivate;

                case PixivAccount.WorkMode.Recommended:
                    return recommended;

                case PixivAccount.WorkMode.User:
                    return user;
                default:
                    return null;
            }
        }

        public PixivWork OpenNextWork(PixivWork currentItem, bool openWindow = false)
        {
            var colview = MainWindow.GetCurrentCollectionViewSource().View;

            bool next = false;
            PixivWork nextwork = null;
            foreach (PixivWork i in colview)
            {
                if (next)
                {
                    if (MainWindow.IsNSFWAllowed() == false && i.IsNSFW) continue;
                    nextwork = i; break;
                }
                if (i.Id.Value == currentItem.Id.Value) next = true;
            }

            if (nextwork == null) return null;

            if (openWindow) OpenWork(nextwork);
            return nextwork;
        }
        public PixivWork OpenPrevWork(PixivWork currentItem, bool openWindow = false)
        {
            var colview = MainWindow.GetCurrentCollectionViewSource().View;

            PixivWork prevwork = null;
            foreach (PixivWork i in colview)
            {
                if (i.Id.Value == currentItem.Id.Value) break;
                if (MainWindow.IsNSFWAllowed() == false && i.IsNSFW) continue;
                prevwork = i;
            }

            if (prevwork == null) return null;

            if (openWindow) OpenWork(prevwork);
            return prevwork;
        }
        #endregion

        #region Command Methods
        public void OpenInBrowser(PixivWork work)
        {
            Process.Start($"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={work.Id}");
        }
        public async void BookmarkWork(PixivWork work)
        {
            if (work.Id == null) return;

            if (work.IsFavorited)
            {
                work.IsBookmarked = false;
                work.UpdateFavorite();

                // remove from bookmarks
                var result = await MainWindow.Account.RemoveFromBookmarks(work.Id.Value);
                if (result == false) work.IsBookmarked = true;
                work.UpdateFavorite();
            }
            else
            {
                work.IsBookmarked = true;
                work.UpdateFavorite();

                // add to bookmarks
                var result = await MainWindow.Account.AddToBookmarks(work.Id.Value);
                if (result.Item1 == false) work.IsBookmarked = false;
                work.UpdateFavorite();
            }
        }

        public List<WorkDetails> OpenWorkWindows = new List<WorkDetails>();
        public void OpenWork(PixivWork work)
        {
            var window = OpenWorkWindows.Find(x => x.LoadedWork.Id == work.Id);
            if (window != null)
            {
                window.Focus();
                return;
            }

            WorkDetails form = new WorkDetails(work);
            OpenWorkWindows.Add(form);
            form.Closing += (a, b) => OpenWorkWindows.Remove((WorkDetails)a);
            form.Show();
        }

        public List<DownloadManager> RunningDownloadManagers = new List<DownloadManager>();
        public void DownloadSelectedWorks(PixivWork work, bool force = false)
        {
            List<PixivWork> selected = null;

            if (force == false) selected = MainWindow.GetSelectedWorks();
            else selected = new List<PixivWork>() { work };

            if (MainWindow.IsNSFWAllowed() == false)
            {
                // remove all NSFW works
                selected.RemoveAll(x => x.IsNSFW);
            }

            var uid = DownloadManager.GenerateUniqueIdentifier(selected);
            var matches = RunningDownloadManagers.FindAll(x => x.UniqueIdentifier == uid);
            var existing = (matches == null) ? null : ((matches.Count == 1) ? matches.First() : matches.Find(x => x.ToDownload.Count == selected.Count));

            if (existing != null)
            {
                if (existing.IsFinished == false)
                {
                    existing.Focus();
                    return;
                }
                else existing.Close();
            }

            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (string.IsNullOrEmpty(Settings.Default.LastDestination) == false)
            {
                dialog.SelectedPath = Settings.Default.LastDestination;
            }
            dialog.ShowNewFolderButton = true;
            var result = dialog.ShowDialog();
            if (result == DialogResult.Cancel) return;

            string destination = dialog.SelectedPath;
            if (Directory.Exists(destination) == false) return;

            Settings.Default.LastDestination = destination;
            Settings.Default.Save();

            // start download queue
            DownloadManager manager = new DownloadManager(selected, destination);
            RunningDownloadManagers.Add(manager);
            manager.Closing += (a, b) => RunningDownloadManagers.Remove((DownloadManager)a);
            manager.Show();
        }
        #endregion
    }

    public static class Extensions
    {
        public static void UpdateWith(this MyObservableCollection<PixivWork> collection, IEnumerable<PixivWork> target,
            Scheduler<PixivWork> associatedScheduler, bool fixInvalid = true)
        {
            if (target.Count() == 0) return;

            foreach (var ti in target)
            {
                bool shouldAdd = true;
                foreach (var i in collection)
                {
                    if (i.Id != ti.Id) continue;
                    shouldAdd = false;

                    // update info
                    i.OrderNumber = ti.OrderNumber;
                    i.IsBookmarked = ti.IsBookmarked;
                    i.FavoriteId = ti.FavoriteId;
                    i.UpdateFavorite();

                    if (i.Stats != null && ti.Stats != null) i.Stats.Score = ti.Stats.Score;
                }

                if (shouldAdd) associatedScheduler.AddItem(ti);
            }

            if (fixInvalid == false) return;

            // fix invalid/removed entries
            int min = target.First().OrderNumber;
            int max = target.Last().OrderNumber;

            List<PixivWork> duplicates = new List<PixivWork>();
            foreach (var item in collection)
            {
                if (item.OrderNumber < min || item.OrderNumber > max) continue;

                bool found = false;
                foreach (var ti in target) if (item.Id == ti.Id) { found = true; break; }

                if (found == false) duplicates.Add(item);
            }

            foreach (var d in duplicates) associatedScheduler.RemoveItem(d);
        }

        public static void AddToList(this List<PixivWork> collection, IEnumerable<PixivWork> target)
        {
            // add to collection, ignore existing ones
            foreach (var i in target)
            {
                if (collection.Count(x => x.Id == i.Id) > 0) continue;
                else collection.Add(i);
            }
        }

        public static void AddList<T>(this MyObservableCollection<T> collection, IEnumerable<T> target)
        {
            foreach (var i in target) collection.Add(i);
        }

        public static List<PixivWork> ToPixivWork(this IEnumerable<Work> collection)
        {
            List<PixivWork> works = new List<PixivWork>();
            foreach (var w in collection) works.Add(new PixivWork(w));
            return works;
        }

        public static void AssignOrderToWorks(this List<PixivWork> collection, int page, int perPage)
        {
            int startNumber = perPage * page - (collection.Count - 1);

            for (int i = 0; i < collection.Count; i++)
            {
                collection[i].OrderNumber = startNumber;
                startNumber++;
            }
        }

        public static int Count(this ICollectionView view)
        {
            int count = 0;
            foreach (var i in view) count++;
            return count;
        }
    }
}
