using CryPixivClient.Commands;
using CryPixivClient.Objects;
using CryPixivClient.Properties;
using CryPixivClient.Windows;
using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;

namespace CryPixivClient.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public const int DefaultPerPage = 30;
        public void Changed([CallerMemberName]string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
     

        #region Private fields
        MyObservableCollection<PixivWork> displayedWorks_Results = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_Ranking = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_Following = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_Bookmarks = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_BookmarksPrivate = new MyObservableCollection<PixivWork>();
        MyObservableCollection<PixivWork> displayedWorks_Recommended = new MyObservableCollection<PixivWork>();

        readonly SemaphoreSlim semaphore;

        string status = "Idle";
        string title = "CryPixiv";
        bool isWorking = false;
        string titleSuffix = "";
        List<PixivWork> dailyRankings = new List<PixivWork>();
        List<PixivWork> bookmarks = new List<PixivWork>();
        List<PixivWork> bookmarksprivate = new List<PixivWork>();
        List<PixivWork> following = new List<PixivWork>();
        List<PixivWork> results = new List<PixivWork>();
        List<PixivWork> recommended = new List<PixivWork>();
        int currentPageResults = 1;
        string lastSearchQuery = null;
        int columns = 4;
        SynchronizationContext UIContext;

        ICommand bookmarkcmd;
        ICommand openbrowsercmd;
        ICommand opencmd;
        ICommand downloadselectedcmd;
        #endregion

        #region Properties
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
        

        public int CurrentPageResults { get => currentPageResults; set { currentPageResults = value; } }
        public string Status
        {
            get => status;
            set { status = value; Changed(); }
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

        public int Columns => columns;

        public string LastSearchQuery => lastSearchQuery;
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
        }

        #region Show Methods
        public async Task Show(List<PixivWork> cache, MyObservableCollection<PixivWork> displayCollection,
            PixivAccount.WorkMode mode, string titleSuffix, string statusPrefix,
            Func<int, Task<List<PixivWork>>> getWorks, bool waitForUser = true, bool fixInvalid = true)
        {
            // set starting values
            MainWindow.CurrentWorkMode = mode;
            MainWindow.DynamicWorksLimit = MainWindow.DefaultWorksLimit;

            // show status
            TitleSuffix = titleSuffix;
            Status = $"{statusPrefix}...";

            // start searching...
            await Task.Run(async () =>
            {
                cache.Clear();
                int currentPage = 0;
                bool lastWasStuck = false;
                for (;;)
                {
                    if (MainWindow.CurrentWorkMode != mode) break;  // if user changes mode - break;

                    // if limit exceeded, stop downloading until user scrolls
                    if (MainWindow.DynamicWorksLimit < cache.Count && waitForUser && cache.Count >= displayCollection.Count)
                    {
                        MainWindow.LimitReached = true;
                        lastWasStuck = true;
                        Status = "Waiting for user to scroll to get more works... (" + displayCollection.Count + " works displayed)";
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
                        MainWindow.LimitReached = false;
                    }

                    try
                    {
                        // start downloading next page
                        IsWorking = true;
                        currentPage++;

                        // download current page
                        var works = await getWorks(currentPage);
                        if (works == null || MainWindow.CurrentWorkMode != mode || works.Count == 0) break;

                        // start NUMBERIN
                        if(mode != PixivAccount.WorkMode.Recommended) works.AssignOrderToWorks(currentPage, DefaultPerPage);
                        
                        cache.AddRange(works);
                        UIContext.Send(async (a) =>
                        {
                            await semaphore.WaitAsync();
                            displayCollection.UpdateWith(works, fixInvalid);
                            semaphore.Release();
                        }, null);

                        Status = $"{statusPrefix}... " + cache.Count + " works" + ((displayCollection.Count > cache.Count) ? $" (Displayed: {displayCollection.Count} works from cache)" : "");
                    }
                    catch (Exception ex)
                    {
                        break;
                    }
                }

                if (MainWindow.CurrentWorkMode == mode)
                {
                    IsWorking = false;
                    Status = "Done. (Found " + displayCollection.Count + " works)";
                }
            });
        }
        public async void ShowSearch(string query, bool autosort = true, int continuePage = 1)
        {
            bool otherWasRunning = LastSearchQuery != query && query != null;

            if (query == null) query = LastSearchQuery;
            int maxResultCount = -1;
            lastSearchQuery = query;

            CancelRunningSearches();

            // set starting values
            var mode = PixivAccount.WorkMode.Search;
            MainWindow.CurrentWorkMode = mode;

            // load cached results if they exist
            await semaphore.WaitAsync();
            if (otherWasRunning)
            {
                DisplayedWorks_Results = new MyObservableCollection<PixivWork>();
            }
            // refresh if necessary
            semaphore.Release();

            // show status
            TitleSuffix = "";
            Status = "Searching...";

            var csrc = new CancellationTokenSource();
            queuedSearches.Enqueue(csrc);

            // start searching...
            await Task.Run(async () =>
            {
                results.Clear();
                int currentPage = continuePage - 1;
                for (;;)
                {
                    if (MainWindow.CurrentWorkMode != mode || csrc.IsCancellationRequested) break; // if user changes mode or requests task to be cancelled - break;
                    // check if max results reached
                    if (maxResultCount != -1 && maxResultCount <= results.Count) break;

                    try
                    {
                        // start downloading next page
                        IsWorking = true;
                        currentPage++;

                        // download current page
                        var works = await MainWindow.Account.SearchWorks(query, currentPage);
                        if (works == null || MainWindow.CurrentWorkMode != mode || csrc.IsCancellationRequested || works.Count == 0) break;
                        if (maxResultCount == -1) maxResultCount = works.Pagination.Total ?? 0;

                        var wworks = works.ToPixivWork();
                        results.AddToList(wworks);

                        UIContext.Send(async (a) =>
                        {
                            await semaphore.WaitAsync();
                            DisplayedWorks_Results.UpdateWith(wworks, false);
                            semaphore.Release();
                        }, null);

                        currentPageResults = currentPage;

                        Status = $"Searching... {DisplayedWorks_Results.Count}/{maxResultCount} works";
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
                    Status = ((csrc.IsCancellationRequested) ? "Stopped. " : "Done. ") + "(Found " + DisplayedWorks_Results.Count + " works)";
                }
            }, csrc.Token);
        }
        #endregion

        public async void ShowDailyRankings() =>
            await Show(dailyRankings, DisplayedWorks_Ranking, PixivAccount.WorkMode.Ranking, "Daily Ranking", 
                "Getting daily ranking", (page) => MainWindow.Account.GetDailyRanking(page));

        public async void ShowFollowing() =>
            await Show(following, DisplayedWorks_Following, PixivAccount.WorkMode.Following, "Following", 
                "Getting following", (page) => MainWindow.Account.GetFollowing(page));

        public async void ShowBookmarksPublic() =>
            await Show(bookmarks, DisplayedWorks_Bookmarks, PixivAccount.WorkMode.BookmarksPublic, "Bookmarks", 
                "Getting bookmarks", (page) => MainWindow.Account.GetBookmarks(page, PixivAccount.Publicity.Public));
        public async void ShowBookmarksPrivate() =>
            await Show(bookmarksprivate, DisplayedWorks_BookmarksPrivate, PixivAccount.WorkMode.BookmarksPrivate, "Private Bookmarks",
                "Getting private bookmarks", (page) => MainWindow.Account.GetBookmarks(page, PixivAccount.Publicity.Private));
        public async void ShowRecommended() =>
            await Show(recommended, DisplayedWorks_Recommended, PixivAccount.WorkMode.Recommended, "Recommended",
                "Getting recommended feed", (page) => MainWindow.Account.GetRecommended(page), fixInvalid: false);

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
        public async Task ResetRecommended()
        {
            bool wasRecommended = MainWindow.CurrentWorkMode == PixivAccount.WorkMode.Recommended;
            if (wasRecommended)
            {
                MainWindow.CurrentWorkMode = PixivAccount.WorkMode.Following;
                await Task.Delay(300);
            }

            await semaphore.WaitAsync();
            recommended = new List<PixivWork>();
            DisplayedWorks_Recommended = new MyObservableCollection<PixivWork>();
            semaphore.Release();

            if (wasRecommended)
            {
                ShowRecommended();
            }
        }
        public void UpdateColumns(double w)
        {
            if (w < 590) columns = 3;
            else if (w < 727) columns = 4;
            else if (w < 846) columns = 5;
            else if (w < 986) columns = 6;
            else columns = (int)Math.Floor(w / 140.0);

            Changed("Columns");
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
            }
        }

        void UpdateImages(MyObservableCollection<PixivWork> collection)
        {
            foreach(var i in collection) i.UpdateThumbnail();            
        }


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

        public PixivWork OpenNextWork(PixivWork currentItem, bool openWindow = false)
        {
            var colview = MainWindow.GetCurrentCollectionViewSource().View;

            bool next = false;
            PixivWork nextwork = null;
            foreach(PixivWork i in colview)
            {
                if (next) { nextwork = i; break; }
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
                prevwork = i;
            }

            if (prevwork == null) return null;

            if (openWindow) OpenWork(prevwork);
            return prevwork;
        }
    }

    public static class Extensions
    {
        public static void SwapCollection<T>(this MyObservableCollection<T> collection, IEnumerable<T> target)
        {
            collection.Clear();
            collection.AddList(target);
        }

        public static void UpdateWith(this MyObservableCollection<PixivWork> collection, IEnumerable<PixivWork> target, bool fixInvalid = true)
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
                
                if (shouldAdd) collection.Add(ti);
            }

            if (fixInvalid == false) return;

            // fix invalid/removed entries
            int min = target.First().OrderNumber;
            int max = target.Last().OrderNumber;

            List<PixivWork> duplicates = new List<PixivWork>();
            foreach(var item in collection)
            {
                if (item.OrderNumber < min || item.OrderNumber > max) continue;

                bool found = false;
                foreach(var ti in target) if (item.Id == ti.Id) { found = true; break; }

                if (found == false) duplicates.Add(item);
            }

            foreach (var d in duplicates) collection.Remove(d);
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
    }
}
