using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient.Objects
{
    public class Cache<T>
    {
        public int MaxSize { get; }
        static List<CachedItem> items;

        public int Count => items.Count;

        public Cache(int maxsize = 10)
        {
            MaxSize = maxsize;
            items = new List<CachedItem>();
        }

        public void Add(T item, Predicate<T> match)
        {
            var existing = items.Find(x => match(x.Item));
            if (existing != null)
            {
                // update existing
                existing.LastAccessed = DateTime.Now;
                return;
            }
            else
            {
                if (items.Count == MaxSize)
                {
                    // remove oldest one
                    items.RemoveAt(0);
                }

                items.Add(new CachedItem(item));
            }

            // sort the collection again
            items.Sort((a, b) => a.LastAccessed.CompareTo(b.LastAccessed));
        }
        public bool Remove(Predicate<T> match) => items.RemoveAll(x => match(x.Item)) > 0;
        public bool Contains(T item)
        {
            foreach (var i in items) if (i.Item.Equals(item)) return true;
            return false;
        }
        public bool Contains(Predicate<T> match) => items.Count(x => match(x.Item)) > 0;
        public List<T> GetFromCache(Predicate<T> match, bool quietAccess = false)
        {
            if (Contains(match) == false) return null;

            var t = items.FindAll(x => match(x.Item));
            if (quietAccess == false) foreach(var tt in t) tt.LastAccessed = DateTime.Now;
            return t.Select(a => a.Item).ToList();
        }

        public class CachedItem
        {
            public T Item { get; }
            public DateTime LastAccessed { get; set; }
            public CachedItem(T item)
            {
                Item = item;
                LastAccessed = DateTime.Now;
            }
        }
    }
}
