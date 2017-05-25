using CryPixivClient.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CryPixivClient
{
    public class Scheduler<T>
    {
        SynchronizationContext UIContext;
        MyObservableCollection<T> collection;
        Queue<Tuple<T, Action>> JobQueue;
        Queue<Tuple<T, Action>> PriorityJobQueue;

        DispatcherTimer timer;
        Func<T, T, bool> removalComparison;
        public int Count => (PriorityJobQueue.Count(x => x.Item2 == Action.Add) + JobQueue.Count(x => x.Item2 == Action.Add)
            - PriorityJobQueue.Count(x => x.Item2 == Action.Remove) - JobQueue.Count(x => x.Item2 == Action.Remove)) + collection.Count;

        public Scheduler(ref MyObservableCollection<T> collection, Func<T, T, bool> removalComparison, SynchronizationContext context = null)
        {
            this.collection = collection;
            this.removalComparison = removalComparison;

            if (context == null) UIContext = SynchronizationContext.Current;
            else UIContext = context;

            JobQueue = new Queue<Tuple<T, Action>>();
            PriorityJobQueue = new Queue<Tuple<T, Action>>();

            timer = new DispatcherTimer(DispatcherPriority.Loaded);
            timer.Interval = TimeSpan.FromMilliseconds(60);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public void AddItem(T item, bool asap = false)
        {
            if (asap == false) JobQueue.Enqueue(new Tuple<T, Action>(item, Action.Add));
            else PriorityJobQueue.Enqueue(new Tuple<T, Action>(item, Action.Add));
        }
        public void RemoveItem(T item, bool asap = false)
        {
            if (asap == false) JobQueue.Enqueue(new Tuple<T, Action>(item, Action.Remove));
            else PriorityJobQueue.Enqueue(new Tuple<T, Action>(item, Action.Remove));
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
                            if (removalComparison == null) collection.Remove(job.Item1);
                            else
                            {
                                T toRemove = default(T);
                                foreach (T item in collection)
                                    if (removalComparison(job.Item1, item)) { toRemove = item; break; }
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
        }
    }

    public enum Action
    {
        Add,
        Remove
    }
}
