using CryPixivClient.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace CryPixivClient
{
    public class Scheduler<T>
    {
        public delegate void JobHandler(Scheduler<T> sender, Tuple<T, Action> job, MyObservableCollection<T> associatedCollection);
        public static event JobHandler JobFinished;

        SynchronizationContext UIContext;
        MyObservableCollection<T> collection;
        Queue<Tuple<T, Action>> JobQueue;
        Queue<Tuple<T, Action>> PriorityJobQueue;
        SortedDictionary<long, bool?> StatusTree;
        Func<T, long> GetIdFromItem;

        public PixivAccount.WorkMode AssociatedWorkMode { get; }

        DispatcherTimer timer;
        Func<T, T, bool> equilityComparer;

        public int ToAddCount => JobQueue.Count(x => x.Item2 == Action.Add) + PriorityJobQueue.Count(x => x.Item2 == Action.Add);
        public int Count => (PriorityJobQueue.Count(x => x.Item2 == Action.Add) + JobQueue.Count(x => x.Item2 == Action.Add)
            - PriorityJobQueue.Count(x => x.Item2 == Action.Remove) - JobQueue.Count(x => x.Item2 == Action.Remove)) + collection.Count;

        public Scheduler(ref MyObservableCollection<T> collection, Func<T, T, bool> equalityComparer, 
            Func<T, long> GetIdFromItem, PixivAccount.WorkMode workmode,
            SynchronizationContext context = null)
        {
            StatusTree = new SortedDictionary<long, bool?>();
            this.collection = collection;
            this.equilityComparer = equalityComparer;
            this.AssociatedWorkMode = workmode;
            this.GetIdFromItem = GetIdFromItem;

            if (context == null) UIContext = SynchronizationContext.Current;
            else UIContext = context;

            JobQueue = new Queue<Tuple<T, Action>>();
            PriorityJobQueue = new Queue<Tuple<T, Action>>();

            timer = new DispatcherTimer(DispatcherPriority.Loaded);
            timer.Interval = TimeSpan.FromMilliseconds(60);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public bool AddItem(T item, bool asap = false)
        {
            if (ContainsItem(item, Action.Add)) return false;

            // Update StatusTree
            var id = GetIdFromItem(item);
            if (StatusTree.ContainsKey(id)) StatusTree[id] = true;        
            else StatusTree.Add(id, true);

            if (asap == false) JobQueue.Enqueue(new Tuple<T, Action>(item, Action.Add));
            else PriorityJobQueue.Enqueue(new Tuple<T, Action>(item, Action.Add));
            return true;
        }
        public bool RemoveItem(T item, bool asap = false)
        {
            if (ContainsItem(item, Action.Remove)) return false;

            // Update StatusTree
            var id = GetIdFromItem(item);
            if (StatusTree.ContainsKey(id)) StatusTree[id] = false;
            else StatusTree.Add(id, false);

            if (asap == false) JobQueue.Enqueue(new Tuple<T, Action>(item, Action.Remove));
            else PriorityJobQueue.Enqueue(new Tuple<T, Action>(item, Action.Remove));
            return true;
        }
        public void MarkNone(T item)
        {
            // Update StatusTree
            var id = GetIdFromItem(item);
            if (StatusTree.ContainsKey(id)) StatusTree[id] = null;
            else StatusTree.Add(id, null);
        }

        public bool ContainsItem(T item, Action action)
        {
            var id = GetIdFromItem(item);
            if (StatusTree.ContainsKey(id) == false) return false;

            if (action == Action.Add && StatusTree[id] == true) return true;
            if (action == Action.Remove && StatusTree[id] == false) return true;
            return false;
        }
        
        public void Clear()
        {
            JobQueue.Clear();
            PriorityJobQueue.Clear();
        }
        public void Stop() => timer.Stop();
        public void Continue() => timer.Start();

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (MainWindow.Paused) return;

            if (JobQueue.Count == 0 && PriorityJobQueue.Count == 0) return;

            var job = (PriorityJobQueue.Count > 0) ? PriorityJobQueue.Dequeue() : JobQueue.Dequeue();

            UIContext.Send((a) =>
            {
                try
                {
                    switch (job.Item2)
                    {
                        case Action.Add:
                            collection.Add(job.Item1);
                            break;
                        case Action.Remove:
                            if (equilityComparer == null) collection.Remove(job.Item1);
                            else
                            {
                                T toRemove = default(T);
                                foreach (T item in collection)
                                    if (equilityComparer(job.Item1, item)) { toRemove = item; break; }
                                collection.Remove(toRemove);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // ignore for now
                }
            }, null);

            if (JobQueue.Count + PriorityJobQueue.Count >= 500) timer.Interval = TimeSpan.FromMilliseconds(30);
            else timer.Interval = TimeSpan.FromMilliseconds(60);

            JobFinished?.Invoke(this, job, collection);
        }
    }

    public enum Action
    {
        Add,
        Remove
    }
}
