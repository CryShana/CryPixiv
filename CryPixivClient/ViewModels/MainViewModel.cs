using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryPixivClient.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void Changed([CallerMemberName]string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #region Private fields
        ObservableCollection<Work> foundWorks = new ObservableCollection<Work>();
        string status = "Idle";
        string title = "CryPixiv";
        bool isWorking = false;
        string titleSuffix = "";
        List<Work> dailyRankings = new List<Work>();
        List<Work> bookmarks = new List<Work>();
        List<Work> following = new List<Work>();
        List<Work> results = new List<Work>();
        int columns = 4;
        SynchronizationContext UIContext;
        #endregion

        #region Properties
        public ObservableCollection<Work> FoundWorks
        {
            get => foundWorks;
            set { foundWorks = value; Changed(); }
        }

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

        #endregion

        public MainViewModel()
        {
            UIContext = SynchronizationContext.Current;
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

        public async Task Show(List<Work> cache, PixivAccount.WorkMode mode, string titleSuffix, string statusPrefix, 
            Func<int, Task<List<Work>>> getWorks, bool waitForUser = true)
        {
            // set starting values
            MainWindow.CurrentWorkMode = mode;
            MainWindow.DynamicWorksLimit = MainWindow.DefaultWorksLimit;

            // load cached results if they exist
            FoundWorks.Clear();
            int count = cache?.Count ?? 0;
            if (cache != null) FoundWorks.SwapCollection(cache);

            // show status
            TitleSuffix = titleSuffix;
            Status = $"{statusPrefix}...";

            // start searching...
            await Task.Run(async () =>
            {
                cache.Clear();
                bool first = false;
                int currentPage = 0;
                for (;;)
                {
                    // if limit exceeded, stop downloading until user scrolls
                    if (MainWindow.DynamicWorksLimit < cache.Count && waitForUser)
                    {
                        Status = "Waiting for user to scroll to get more works... (" + FoundWorks.Count + " works displayed)";
                        IsWorking = false;
                        await Task.Delay(200);
                        continue;
                    }
                    if (MainWindow.CurrentWorkMode != mode) break;  // if user changes mode - break;

                    try
                    {
                        // start downloading next page
                        IsWorking = true;
                        currentPage++;

                        // download current page
                        var works = await getWorks(currentPage);
                        if (works == null || MainWindow.CurrentWorkMode != mode) break;

                        // if cache has less entries than downloaded - swap cache with newest entries and keep updating...
                        if (cache.Count + works.Count > FoundWorks.Count)
                        {
                            if (first == false) UIContext.Send((a) => FoundWorks.SwapCollection(cache), null);

                            cache.AddRange(works);
                            UIContext.Send((a) => FoundWorks.AddList(works), null);
                            first = true;
                        }
                        else cache.AddRange(works);

                        Status = $"{statusPrefix}... " + FoundWorks.Count + " works";
                    }
                    catch (Exception ex)
                    {
                        break;
                    }
                }

                IsWorking = false;
                Status = "Done. (Found " + FoundWorks.Count + " works)";
            });
        }

        public async void ShowDailyRankings() =>
            await Show(dailyRankings, PixivAccount.WorkMode.Ranking, "Daily Ranking", "Getting daily ranking", (page) => MainWindow.Account.GetDailyRanking(page));

        public async void ShowFollowing() =>
            await Show(following, PixivAccount.WorkMode.Following, "Following", "Getting following", (page) => MainWindow.Account.GetFollowing(page));

        public async void ShowBookmarks() =>
            await Show(bookmarks, PixivAccount.WorkMode.Bookmarks, "Bookmarks", "Getting bookmarks", (page) => MainWindow.Account.GetBookmarks(page));

        internal void ShowSearch()
        {
            throw new NotImplementedException();
        }
    }

    public static class Extensions
    {
        public static void SwapCollection<T>(this ObservableCollection<T> collection, IEnumerable<T> target)
        {
            collection.Clear();
            collection.AddList(target);
        }
        public static void AddList<T>(this ObservableCollection<T> collection, IEnumerable<T> target)
        {
            foreach (var i in target) collection.Add(i);
        }
    }
}
