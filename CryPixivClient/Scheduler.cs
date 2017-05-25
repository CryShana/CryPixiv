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

        DispatcherTimer timer;
        public Scheduler(ref MyObservableCollection<T> collection, SynchronizationContext context = null)
        {
            this.collection = collection;

            if (context == null) UIContext = SynchronizationContext.Current;
            else UIContext = context;

            JobQueue = new Queue<Tuple<T, Action>>();

            timer = new DispatcherTimer(DispatcherPriority.Loaded);
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public void AddItem(T item) => JobQueue.Enqueue(new Tuple<T, Action>(item, Action.Add));
        public void RemoveItem(T item) => JobQueue.Enqueue(new Tuple<T, Action>(item, Action.Remove));
        public void Stop() => timer.Stop();
        public void Continue() => timer.Start();

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (JobQueue.Count == 0) return;

            var job = JobQueue.Dequeue();

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
                            collection.Remove(job.Item1);
                            break;
                    }
                }
                catch(Exception ex)
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
