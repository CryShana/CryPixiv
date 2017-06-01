using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixeez.Objects
{
    public class ProfileImageUrls
    {

        [JsonProperty("medium")]
        public string Medium { get; set; }

        [JsonProperty("px_50x50")]
        public string MediumOld { get; set; }


        public string MainImage => Medium ?? MediumOld;
    }
}
