using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient
{
    public static class Translator
    {
        public static string Translate(string text)
        {
            string source = "ja";
            string target = "en";

            string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=" + source + "&tl=" + target + "&dt=t&q=" + System.Web.HttpUtility.UrlEncode(text);

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                try
                {
                    var content = client.DownloadString(url);

                    var part1 = content.Substring(content.IndexOf('"') + 1);
                    var result = part1.Substring(0, part1.IndexOf('"'));
                    return result;
                }
                catch (Exception ex)
                {
                    string msg = ex.GetBaseException().Message;
                    return "";
                }
            }
        }
    }
}
