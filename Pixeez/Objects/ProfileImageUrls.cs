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
        public string Meidum { get; set; }
    }
}
