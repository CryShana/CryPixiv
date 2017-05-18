using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient.Objects
{
    public class MyObservableCollection<T> : ObservableCollection<T>
    {
        public MyObservableCollection() : base() { }
        public MyObservableCollection(IEnumerable<T> collection) : base(collection) { }
            
        public void InsertRange(IEnumerable<T> items)
        {
            if (items.Count() == 0) return;

            this.CheckReentrancy();
            foreach (var item in items) this.Items.Add(item);
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        public void InsertRange(IEnumerable<T> items, Predicate<T> predicate)
        {
            this.CheckReentrancy();
            foreach (var item in items) if(predicate(item)) this.Items.Add(item);
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
