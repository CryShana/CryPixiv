using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryPixivClient.Objects
{
    public class PixivWork : Work, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static readonly string[] NsfwTags = { "R-18" };

        public bool IsNSFW
        {
            get
            {
                foreach (var t in Tags)
                    foreach (var tt in NsfwTags)
                        if (t.ToLower() == tt.ToLower()) return true;

                return false;
            }
        }

        public bool ShouldBlur => IsNSFW && MainWindow.IsNSFWAllowed() == false;

        public string PageCountText => (PageCount == null || PageCount.Value == 1) ? "" : PageCount.Value.ToString();
        public bool IsFavorited
        {
            get
            {
                if (IsBookmarked == null) IsBookmarked = FavoriteId != null && FavoriteId != 0;

                return IsBookmarked.Value;
            }
        }
        public string BookmarkText => (IsFavorited) ? "Remove bookmark" : "Bookmark";

        public int OrderNumber { get; set; } = int.MaxValue;
        public string IdText => Id?.ToString() ?? "";

        bool shouldSkipFirst = false;
        public ImageSource img = null;
        public ImageSource ImageThumbnail
        {
            get
            {
                if (img == null && shouldSkipFirst == false)
                {
                    try
                    {
                        img = GetImage(ImageUrls.SquareMedium);
                    }
                    catch
                    {
                        // failed to load
                        img = null;
                    }
                }

                shouldSkipFirst = false;
                return img;
            }
        }

        public PixivWork(Work work)
        {
            Id = work.Id;
            Title = work.Title;
            Caption = work.Caption;
            TagsOld = work.TagsOld;
            TagsNew = work.TagsNew;
            FavoriteId = work.FavoriteId;
            ImageUrls = work.ImageUrls;
            Width = work.Width;
            Height = work.Height;
            TotalBookmarks = work.TotalBookmarks;
            Restrict = work.Restrict;
            CreatedTime = work.CreatedTime;
            User = work.User;
            IsBookmarked = work.IsBookmarked;
            PageCount = work.PageCount;
            Type = work.Type;
            Stats = work.Stats;
            MetaPages = work.MetaPages;
            MetaSinglePage = work.MetaSinglePage;
        }

        public void UpdateFavorite() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFavorited"));
        public void UpdateThumbnail() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageThumbnail"));
        public void UpdateNSFW() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ShouldBlur"));
        public void ResetThumbnail()
        {
            img = null;
            shouldSkipFirst = true;
        }

        public static ImageSource GetImage(string url)
        {
            var image = new BitmapImage();
            var buffer = DownloadImage(url);

            using (var stream = new MemoryStream(buffer))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }

            image.Freeze();
            return image;
        }

        static byte[] DownloadImage(string url)
        {
            var buffer = new byte[0];
            using (var client = new WebClient())
            {
                client.Headers.Add("Referer", "http://spapi.pixiv.net/");
                client.Headers.Add("User-Agent", "PixivIOSApp/5.8.0");
                client.UseDefaultCredentials = true;
                buffer = client.DownloadData(url);
            }
            return buffer;
        }
    }
}
