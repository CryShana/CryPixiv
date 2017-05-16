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
        List<Work> dailyRankings;
        List<Work> bookmarks;
        List<Work> results;
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

        public async void ShowDailyRankings()
        {
            // load cached results if they exist
            int count = dailyRankings?.Count ?? 0;
            if (dailyRankings != null)
            {
                FoundWorks.SwapCollection(dailyRankings);
            }

            // set starting values
            MainWindow.CurrentWorkMode = PixivAccount.WorkMode.Ranking;
            MainWindow.DynamicWorksLimit = MainWindow.DefaultWorksLimit;

            // show status
            TitleSuffix = "Daily Ranking";
            Status = "Getting daily ranking...";

            // start searching...
            await Task.Run(async () =>
            {
                dailyRankings = new List<Work>();
                bool first = false;
                int currentPage = 0;
                for (;;)
                {
                    // if limit exceeded, stop downloading until user scrolls
                    if (MainWindow.DynamicWorksLimit < dailyRankings.Count)
                    {
                        Status = "Waiting for user to scroll to get more works... (" + FoundWorks.Count + " works displayed)";
                        IsWorking = false;
                        await Task.Delay(200);
                        continue;
                    }
                    if (MainWindow.CurrentWorkMode != PixivAccount.WorkMode.Ranking) break;  // if user changes mode - break;

                    try
                    {
                        // start downloading next page
                        IsWorking = true;
                        currentPage++;

                        // download current page
                        var works = await MainWindow.Account.GetDailyRanking(currentPage);
                        if (works == null) break;

                        // if cache has less entries than downloaded - swap cache with newest entries and keep updating...
                        if (dailyRankings.Count + works.Count > FoundWorks.Count)
                        {
                            UIContext.Send((a) =>
                            {
                                if (first == false) FoundWorks.SwapCollection(dailyRankings);
                                dailyRankings.AddRange(works);
                                FoundWorks.AddList(works);
                                first = true;
                            }, null);
                        }

                        Status = "Getting daily ranking... " + FoundWorks.Count + " works";
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

        public async void ShowFollowing()
        {
            // do stuff
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
