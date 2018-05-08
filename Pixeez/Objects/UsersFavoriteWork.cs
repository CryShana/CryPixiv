using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixeez.Objects
{
    public class UsersFavoriteWork
    {

        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }

        [JsonProperty("tags")]
        public IList<string> Tags { get; set; }

        [JsonProperty("publicity")]
        public string Publicity { get; set; }

        [JsonProperty("work")]
        public Work Work { get; set; }
    }

    public class NewIllustration
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("image_urls")]
        public Dictionary<string, string> ImageUrls { get; set; }
        [JsonProperty("caption")]
        public string Caption { get; set; }
        [JsonProperty("restrict")]
        public int Restrict { get; set; }
        [JsonProperty("user")]
        public User ArtistUser { get; set; }
        [JsonProperty("tags")]
        public List<Tag> Tags { get; set; }
        [JsonProperty("tools")]
        public List<string> Tools { get; set; }
        [JsonProperty("create_date")]
        public DateTime Created { get; set; }
        [JsonProperty("page_count")]
        public int PageCount { get; set; }
        [JsonProperty("width")]
        public int Width { get; set; }
        [JsonProperty("height")]
        public int Height { get; set; }
        [JsonProperty("sanity_level")]
        public int SanityLevel { get; set; }
        [JsonProperty("series")]
        public object Series { get; set; }
        [JsonProperty("meta_single_page")]
        public Dictionary<string, string> MetaSinglePage { get; set; }
        [JsonProperty("meta_pages")]
        public List<object> MetaPages { get; set; }
        [JsonProperty("total_view")]
        public int TotalViews { get; set; }
        [JsonProperty("total_bookmarks")]
        public int TotalBookmarks { get; set; }
        [JsonProperty("is_bookmarked")]
        public bool IsBookmarked { get; set; }
        [JsonProperty("visible")]
        public bool Visible { get; set; }
        [JsonProperty("is_muted")]
        public bool Muted { get; set; }

        public Work GetWork()
        {
            var w = new Work() {
                Id = Id,
                PageCount = PageCount,
                TagsNew = Tags,
                Restrict = Restrict,
                IsBookmarked = IsBookmarked,
                Title = Title,
                Caption = Caption,
                CreatedTime = Created,
                Height = Height,
                Width = Width,
                ImageUrls = new ImageUrls()
                {
                    Large = ImageUrls["large"],
                    Original = ImageUrls["large"],
                    Medium = ImageUrls["medium"],
                    SquareMedium = ImageUrls["square_medium"]
                },
                Type = Type,
                Stats = new WorkStats()
                {
                    Score = this.TotalBookmarks,
                    ViewsCount = this.TotalViews
                },
                User = ArtistUser
            };
            return w;
        }
    }
}
