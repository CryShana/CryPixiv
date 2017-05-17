using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Pixeez.Objects
{
    public class ImageUrls
    {
        [JsonProperty("px_128x128")]
        public string px128x128 { get; set; }

        string sqMedium = null;
        [JsonProperty("square_medium")]
        public string SquareMedium
        {
            get => sqMedium ?? px128x128;
            set { sqMedium = value; }
        }

        [JsonProperty("medium")]
        public string Medium { get; set; }

        [JsonProperty("large")]
        public string Large { get; set; }
    }

    public class FavoritedCount
    {
        [JsonProperty("public")]
        public int? Public { get; set; }

        [JsonProperty("private")]
        public int? Private { get; set; }
    }

    public class WorkStats
    {
        [JsonProperty("scored_count")]
        public int? ScoredCount { get; set; }

        [JsonProperty("score")]
        public int? Score { get; set; }

        [JsonProperty("views_count")]
        public int? ViewsCount { get; set; }

        [JsonProperty("favorited_count")]
        public FavoritedCount FavoritedCount { get; set; }

        [JsonProperty("commented_count")]
        public int? CommentedCount { get; set; }
    }

    public class Page
    {

        [JsonProperty("image_urls")]
        public ImageUrls ImageUrls { get; set; }
    }

    public class Metadata
    {

        [JsonProperty("pages")]
        public IList<Page> Pages { get; set; }
    }

    public class Work
    {

        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("caption")]
        public string Caption { get; set; }

        /*
        [JsonProperty("tags")]
        public List<Dictionary<string,string>> Tags { get; set; }*/   //Was commented out because of compatibility issues between new and old entries!

        [JsonProperty("favorite_id")]
        public long? FavoriteId { get; set; }

        [JsonProperty("image_urls")]
        public ImageUrls ImageUrls { get; set; }

        [JsonProperty("width")]
        public int? Width { get; set; }

        [JsonProperty("height")]
        public int? Height { get; set; }

        [JsonProperty("stats")]
        public WorkStats Stats { get; set; }

        [JsonProperty("total_bookmarks")]
        public long? TotalBookmarks { get; set; }

        [JsonProperty("restrict")]
        public int? Restrict { get; set; }

        [JsonProperty("create_date")]
        public DateTimeOffset CreatedTime { get; set; }

        [JsonProperty("user")]
        public User User { get; set; }

        [JsonProperty("is_bookmarked")]
        public bool? IsBookmarked { get; set; }

        [JsonProperty("page_count")]
        public int? PageCount { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        public string GetImageUri(string baseUri, int pageNumber = 0)
        {
            if (pageNumber > PageCount) return null;
            return baseUri.Replace("p0", "p" + pageNumber);
        }
    }
}
