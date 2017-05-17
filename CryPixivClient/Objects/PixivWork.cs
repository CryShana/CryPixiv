using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryPixivClient.Objects
{
    public class PixivWork : Work
    {
        public string PageCountText => (PageCount == null || PageCount.Value == 1) ? "" : PageCount.Value.ToString();
        public bool IsFavorited => (FavoriteId == null) ? false : true;

        ImageSource img = null;
        public ImageSource ImageThumbnail
        {
            get
            {
                if (img == null)
                {
                    try
                    {
                        var image = new BitmapImage();
                        var buffer = new byte[0];
                        using(var client = new WebClient())
                        {
                            client.Headers.Add("Referer", "http://spapi.pixiv.net/");
                            client.Headers.Add("User-Agent", "PixivIOSApp/5.8.0");
                            client.UseDefaultCredentials = true;
                            buffer = client.DownloadData(ImageUrls.Px128x128);
                        }

                        using (var stream = new MemoryStream(buffer))
                        {
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.StreamSource = stream;
                            image.EndInit();
                        }

                        image.Freeze();
                        img = image;
                    }
                    catch
                    {
                        // failed to load
                    }
                }
                return img;
            }
        }

        public PixivWork(Work work)
        {
            Id = work.Id;
            Title = work.Title;
            Caption = work.Caption;
            Tags = work.Tags;
            Tools = work.Tools;
            ImageUrls = work.ImageUrls;
            Width = work.Width;
            Height = work.Height;
            Stats = work.Stats;
            Publicity = work.Publicity;
            AgeLimit = work.AgeLimit;
            CreatedTime = work.CreatedTime;
            ReuploadedTime = work.ReuploadedTime;
            User = work.User;
            IsManga = work.IsManga;
            IsLiked = work.IsLiked;
            FavoriteId = work.FavoriteId;
            PageCount = work.PageCount;
            BookStyle = work.BookStyle;
            Type = work.Type;
            Metadata = work.Metadata;
            ContentType = work.ContentType;
        }
    }
}
