using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Pixeez.Objects;
using System.Linq;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Threading;
using System.Globalization;

namespace Pixeez
{
    public enum MethodType
    {
        GET = 0,
        POST = 1,
        DELETE = 2,
    }

    public class PhpLoginInfo
    {
        public bool Success { get; set; }
        public string DeviceToken { get; set; }
        public string SessionId { get; set; }
    }

    public class AsyncResponse : IDisposable
    {
        public AsyncResponse(HttpResponseMessage source)
        {
            this.Source = source;
        }

        public HttpResponseMessage Source { get; }

        public Task<Stream> GetResponseStreamAsync()
        {
            return this.Source.Content.ReadAsStreamAsync();
        }

        public Task<string> GetResponseStringAsync()
        {
            return this.Source.Content.ReadAsStringAsync();
        }

        public Task<byte[]> GetResponseByteArrayAsync()
        {
            return this.Source.Content.ReadAsByteArrayAsync();
        }

        public void Dispose()
        {
            this.Source?.Dispose();
        }
    }

    public class Auth
    {
        const string ClientId = "bYGKuGVw91e0NMfPGp44euvGt59s";
        const string ClientSecret = "HP3RmkgAmEGro0gn1x9ioawQE8WMfvLXDz3ZqxpK";

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>string</c> username (required)</para>
        /// <para>- <c>string</c> password (required)</para>
        /// </summary>
        /// <returns>Tokens.</returns>
        public static async Task<Tokens> AuthorizeAsync(string username, string password, string deviceToken = "")
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Referer", "http://www.pixiv.net/");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PixivIOSApp/5.8.0");

            // Invalid grant_type parameter or parameter missing

            var param = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "grant_type", "password" },
                { "client_id", ClientId },
                { "client_secret", ClientSecret },
                { "device_token", deviceToken }
            });

            var requestIssued = DateTime.Now;
            var response = await httpClient.PostAsync("https://oauth.secure.pixiv.net/auth/token", param);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException();

            var json = await response.Content.ReadAsStringAsync();
            var authorize = JToken.Parse(json).SelectToken("response").ToObject<Authorize>();
            authorize.TimeIssued = requestIssued;

            return new Tokens(authorize);
        }

        public static async Task<PhpLoginInfo> AuthorizePHPSession(string username, string password, string dToken = null)
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Referer", "http://www.pixiv.net/");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "PixivIOSApp/5.8.0");

                // get post key
                var response = await httpClient.GetAsync("https://accounts.pixiv.net/login?lang=en&source=pc&view_type=page&ref=wwwtop_accounts_index");
                var content = await response.Content.ReadAsStringAsync();

                content = content.Substring(content.IndexOf("post_key"), 55);
                var postKey = Regex.Match(content, @".*?value=""(.*?)"".*").Groups[1].Value;

                // login
                var param = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "pixiv_id", username },
                    { "password", password },
                    { "captcha", "" },
                    { "g_recaptcha_response", "" },
                    { "post_key", postKey },
                    { "source", "pc" },
                    { "ref", "wwwtop_accounts_index" }
                });

                if (string.IsNullOrEmpty(dToken) == false) httpClient.DefaultRequestHeaders.Add("Cookie", $"device_token={dToken};");
                httpClient.DefaultRequestHeaders.Add("Origin", @"https://accounts.pixiv.net");
                response = await httpClient.PostAsync("https://accounts.pixiv.net/api/login?lang=en", param);
                if (response.IsSuccessStatusCode == false) throw new Exception("Bad request!");

                content = await response.Content.ReadAsStringAsync();

                // if success, get Set-Cookie values
                string deviceToken = null;
                string phpsessionid = null;
                response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> values);
                foreach (var v in values)
                {
                    string phppattern = "PHPSESSID=(.*?);";
                    string devicepattern = "device_token=(.*?);";

                    if (Regex.IsMatch(v, phppattern)) phpsessionid = Regex.Match(v, phppattern).Groups[1].Value;
                    else if (Regex.IsMatch(v, devicepattern)) deviceToken = Regex.Match(v, devicepattern).Groups[1].Value;
                }
                if (deviceToken == null || phpsessionid == null) throw new Exception("Invalid response received!");

                return new PhpLoginInfo()
                {
                    Success = true,
                    DeviceToken = deviceToken,
                    SessionId = phpsessionid
                };
            }
            catch
            {
                return new PhpLoginInfo()
                {
                    Success = false
                };
            }

        }
        public static Tokens AuthorizeWithAccessToken(string accessToken, string refreshToken, int expires, DateTime issued)
        {
            return new Tokens(new Authorize()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expires,
                TimeIssued = issued
            });
        }
    }

    public class Tokens
    {
        public Authorize AuthDetails { get; private set; }
        public string AccessToken => AuthDetails.AccessToken;

        internal Tokens(Authorize auth)
        {
            AuthDetails = auth;
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>MethodType</c> type (required) [ GET, POST ]</para>
        /// <para>- <c>string</c> url (required)</para>
        /// <para>- <c>IDictionary</c> param (required)</para>
        /// <para>- <c>IDictionary</c> header (optional)</para>
        /// </summary>
        /// <returns>AsyncResponse.</returns>
        public async Task<AsyncResponse> SendRequestAsync(MethodType type, string url, IDictionary<string, string> param, IDictionary<string, string> headers = null)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Referer", "http://spapi.pixiv.net/");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PixivIOSApp/5.8.0");
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + this.AccessToken);

            if (headers != null)
            {
                foreach (var header in headers)
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            AsyncResponse asyncResponse = null;

            if (type == MethodType.POST)
            {
                var reqParam = new FormUrlEncodedContent(param);
                var response = await httpClient.PostAsync(url, reqParam);
                asyncResponse = new AsyncResponse(response);
            }
            else if (type == MethodType.DELETE)
            {
                var uri = url;

                if (param != null)
                {
                    var query_string = "";
                    foreach (KeyValuePair<string, string> kvp in param)
                    {
                        if (query_string == "")
                            query_string += "?";
                        else
                            query_string += "&";

                        query_string += kvp.Key + "=" + WebUtility.UrlEncode(kvp.Value);
                    }
                    uri += query_string;
                }

                var response = await httpClient.DeleteAsync(uri);
                asyncResponse = new AsyncResponse(response);
            }
            else
            {
                var uri = url;

                if (param != null)
                {
                    var query_string = "";
                    foreach (KeyValuePair<string, string> kvp in param)
                    {
                        if (query_string == "")
                            query_string += "?";
                        else
                            query_string += "&";

                        query_string += kvp.Key + "=" + WebUtility.UrlEncode(kvp.Value);
                    }
                    uri += query_string;
                }

                var response = await httpClient.GetAsync(uri);
                asyncResponse = new AsyncResponse(response);
            }

            return asyncResponse;
        }

        public const string AccessTokenErrorMessage = "Access token expired.";
        private async Task<Tuple<T, string>> AccessApiAsync<T>(MethodType type, string url, IDictionary<string, string> param, IDictionary<string, string> headers = null)
        {
            using (var response = await this.SendRequestAsync(type, url, param, headers))
            {
                var json = await response.GetResponseStringAsync();
                T obj = default(T);

                if (json == "{}" || json.Contains("{\"system\":{\"message\":404,\"code\":null}}}") ||
                    json.Contains("存在しないランキングページを参照しています"))
                {
                    return new Tuple<T, string>(obj, null);
                }

                json = json.Replace("created_time", "create_date"); // to make it compatible with newer JSON entries
                json = json.Replace("tags", "tags_old");
                try
                {
                    if (typeof(T) == typeof(bool))
                    {
                        if (json.Contains("success")) obj = (dynamic)true;
                    }
                    else obj = JToken.Parse(json).SelectToken("response").ToObject<T>();
                }
                catch (NullReferenceException nex)
                {
                    if (json.Contains("1000ページまでしか取得できませ")) return new Tuple<T, string>(obj, "Can only get up to 1000 pages of works!");
                    if (json.ToLower().Contains("access token")) return new Tuple<T, string>(obj, AccessTokenErrorMessage);
                    if (json.Contains("has_error")) return new Tuple<T, string>(obj, "Known Error: " + "Invalid response from server received!" + "\n\nResponse: " + json ?? "-");
                    else return new Tuple<T, string>(obj, "Error: " + nex.Message + "\n\nResponse: " + json ?? "-");
                }
                catch (Exception ex)
                {
                    return new Tuple<T, string>(obj, "Error: " + ex.Message + "\n\nResponse: " + json ?? "-");
                }

                if (obj is IPagenated)
                    ((IPagenated)obj).Pagination = JToken.Parse(json).SelectToken("pagination").ToObject<Pagination>();

                return new Tuple<T, string>(obj, null);
            }
        }
        private async Task<Tuple<T, string>> AccessApiAsyncNew<T>(MethodType type, string url, IDictionary<string, string> param, IDictionary<string, string> headers = null) where T : class
        {
            using (var response = await this.SendRequestAsync(type, url, param, headers))
            {
                var json = await response.GetResponseStringAsync();
                T obj = default(T);

                if (json == "{}" || json.Contains("存在しないランキングページを参照しています")) return new Tuple<T, string>(obj, null);

                try
                {
                    obj = JToken.Parse(json).SelectToken("illusts").ToObject<T>();  // response
                }
                catch (NullReferenceException nex)
                {
                    if (json.Contains("Error occurred at the OAuth process")) return new Tuple<T, string>(obj, "Known Error: " + nex.Message + "\n\nResponse: " + json ?? "-");
                    else return new Tuple<T, string>(obj, "Error: " + nex.Message + "\n\nResponse: " + json ?? "-");

                }
                catch (Exception ex)
                {
                    return new Tuple<T, string>(obj, "Error: " + ex.Message + "\n\nResponse: " + json ?? "-");
                }

                return new Tuple<T, string>(obj, null);
            }
        }

        public void LogError(params string[] errors)
        {
            string line = $"[{DateTime.Now.ToString("HH:mm:ss")}] ";
            foreach (var s in errors) line += s;
            Debug.WriteLine(line);
        }

        public byte[] DownloadImage(string baseUri = null)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Referer", "http://spapi.pixiv.net/");
            client.DefaultRequestHeaders.Add("User-Agent", "PixivIOSApp/5.8.0");
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + this.AccessToken);

            return client.GetByteArrayAsync(baseUri).Result;
        }
        public async Task<byte[]> DownloadImageAsync(string baseUri = null)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Referer", "http://spapi.pixiv.net/");
            client.DefaultRequestHeaders.Add("User-Agent", "PixivIOSApp/5.8.0");
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + this.AccessToken);

            return await client.GetByteArrayAsync(baseUri);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> illustId (required)</para>
        /// </summary>
        /// <returns>Works.</returns>
        public async Task<Tuple<List<Work>, string>> GetWorksAsync(long illustId)
        {
            var url = "https://public-api.secure.pixiv.net/v1/works/" + illustId.ToString() + ".json";

            var param = new Dictionary<string, string>
            {
                { "profile_image_sizes", "px_170x170,px_50x50" },
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" },
                { "include_stats", "true" },
            };

            return await this.AccessApiAsync<List<Work>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> authorId (required)</para>
        /// </summary>
        /// <returns>Users.</returns>
        public async Task<Tuple<List<User>, string>> GetUsersAsync(long authorId)
        {
            var url = "https://public-api.secure.pixiv.net/v1/users/" + authorId.ToString() + ".json";

            var param = new Dictionary<string, string>
            {
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "include_stats", "1" } ,
                { "include_profile", "1" } ,
                { "include_workspace", "1" } ,
                { "include_contacts", "1" } ,
            };

            return await this.AccessApiAsync<List<User>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> maxId (optional)</para>
        /// <para>- <c>bool</c> showR18 (optional)</para>
        /// </summary>
        /// <returns>Feeds.</returns>
        public async Task<Tuple<List<Feed>, string>> GetMyFeedsAsync(long maxId = 0, bool showR18 = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/me/feeds.json";

            var param = new Dictionary<string, string>
            {
                { "relation", "all" } ,
                { "type", "touch_nottext" } ,
                { "show_r18", Convert.ToInt32(showR18).ToString() } ,
            };

            if (maxId != 0)
                param.Add("max_id", maxId.ToString());

            return await this.AccessApiAsync<List<Feed>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>UsersFavoriteWorks. (Pagenated)</returns>
        public async Task<Tuple<Paginated<UsersFavoriteWork>, string>> GetMyFavoriteWorksAsync(int page = 1, int perPage = 30, string publicity = "public", bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/me/favorite_works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "publicity", publicity } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await this.AccessApiAsync<Paginated<UsersFavoriteWork>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> workID (required)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        public async Task<Tuple<List<UsersFavoriteWork>, string>> AddMyFavoriteWorksAsync(long workId, string comment = "", IEnumerable<string> tags = null, string publicity = "public")
        {
            var url = "https://public-api.secure.pixiv.net/v1/me/favorite_works.json";

            var param = new Dictionary<string, string>
            {
                { "work_id", workId.ToString() } ,
                { "publicity", publicity } ,
                { "comment", comment } ,
            };

            if (tags != null)
                param.Add("tags", string.Join(",", tags));

            return await this.AccessApiAsync<List<UsersFavoriteWork>>(MethodType.POST, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> workId (required)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        public async Task<Tuple<Paginated<UsersFavoriteWork>, string>> DeleteMyFavoriteWorksAsync(long workId, string publicity = "public")
        {
            //var url = "https://public-api.secure.pixiv.net/v1/me/favorite_works.json";
            var url = "https://app-api.pixiv.net/v1/illust/bookmark/delete";  // new way of doing it :D

            var param = new Dictionary<string, string>
            {
                { "illust_id", workId.ToString() } ,
                { "restrict", publicity } ,
            };

            return await this.AccessApiAsync<Paginated<UsersFavoriteWork>>(MethodType.POST, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> authorId (required)</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        public async Task<Tuple<Paginated<Work>, string>> GetMyFollowingWorksAsync(int page = 1, int perPage = 30, string publicity = "public", bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/me/following/works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "publicity", publicity } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await this.AccessApiAsync<Paginated<Work>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> authorId (required)</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        public async Task<Tuple<Paginated<Work>, string>> GetUsersWorksAsync(long authorId, int page = 1, int perPage = 30, string publicity = "public", bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/users/" + authorId.ToString() + "/works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "publicity", publicity } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await this.AccessApiAsync<Paginated<Work>>(MethodType.GET, url, param);
        }

        public async Task<Tuple<bool, string>> FollowUnfollowUser(bool follow, long authorId, bool isPublic = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/me/favorite-users.json";

            var param = (follow) ?
                new Dictionary<string, string>
                {
                    { "target_user_id", authorId.ToString() } ,
                    { "publicity", (isPublic) ? "public" : "private" }
                } :
                new Dictionary<string, string>
                {
                    { "delete_ids", authorId.ToString() } ,
                    { "publicity", (isPublic) ? "public" : "private" }
                };

            Tuple<bool, string> res = (follow) ?
                await this.AccessApiAsync<bool>(MethodType.POST, url, param) :
                await this.AccessApiAsync<bool>(MethodType.DELETE, url, param);

            return new Tuple<bool, string>(res.Item1, res?.Item2);
        }


        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> authorId (required)</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>UsersFavoriteWorks. (Pagenated)</returns>
        public async Task<Tuple<Paginated<UsersFavoriteWork>, string>> GetUsersFavoriteWorksAsync(long authorId, int page = 1, int perPage = 30, string publicity = "public", bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/users/" + authorId.ToString() + "/favorite_works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "publicity", publicity } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await this.AccessApiAsync<Paginated<UsersFavoriteWork>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> maxId (optional)</para>
        /// <para>- <c>bool</c> showR18 (optional)</para>
        /// </summary>
        /// <returns>Feed.</returns>
        public async Task<Tuple<List<Feed>, string>> GetUsersFeedsAsync(long authorId, long maxId = 0, bool showR18 = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/users/" + authorId.ToString() + "/feeds.json";

            var param = new Dictionary<string, string>
            {
                { "relation", "all" } ,
                { "type", "touch_nottext" } ,
                { "show_r18", Convert.ToInt32(showR18).ToString() } ,
            };

            if (maxId != 0)
                param.Add("max_id", maxId.ToString());

            return await this.AccessApiAsync<List<Feed>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>string</c> mode (optional) [ day, week, month, day_male, day_female, week_rookie, week_original, day_r18, week_r18, day_male_r18, day_female_r18, week_r18g ]</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> date (optional) [ 2015-04-01 ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>RankingAll. (Pagenated)</returns>
        public async Task<Tuple<Paginated<Work>, string>> GetRankingAllAsync(string mode = "day", int page = 1, int perPage = 30, string date = "", bool includeSanityLevel = true)
        {
            // var url = "https://public-api.secure.pixiv.net/v1/ranking/all";
            var url = "https://app-api.pixiv.net/v1/illust/ranking";

            var param = new Dictionary<string, string>
            {
                { "mode", mode } ,
                { "offset", (perPage * (page - 1)).ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw,original" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            if (!string.IsNullOrWhiteSpace(date))
                param.Add("date", date);

            return await this.AccessApiAsyncNew<Paginated<Work>>(MethodType.GET, url, param);
        }


        // use this to keep track of ECD pages retreived before renewing the ECD
        public static bool IsUsingECD = false;
        static int ecdPage = 0;
        static string activeEcd = null;
        static string searchquery = null;
        static int RenewTries = 0;

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>string</c> q (required)</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> mode (optional) [ text, tag, exact_tag, caption ]</para>
        /// <para>- <c>string</c> period (optional) [ all, day, week, month ]</para>
        /// <para>- <c>string</c> order (optional) [ desc, asc ]</para>
        /// <para>- <c>string</c> sort (optional) [ date ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>Works. (Pagenated)</returns>
        public async Task<Tuple<Paginated<Work>, string>> SearchWorksAsync(string query, int page = 1, int perPage = 30, string mode = "text",
            string period = "all", string order = "desc", string sort = "date", bool includeSanityLevel = true, string ecd = null, bool renew = false)
        {
            // ecd page tracker should be reset whenever a new search is started
            if (page <= 2 || query != searchquery)
            {
                searchquery = null;
                activeEcd = null;
                ecdPage = 0;
            }
            if (renew == false) RenewTries = 0;

            // start search
            var url = "https://public-api.secure.pixiv.net/v1/search/works.json";
            var param = new Dictionary<string, string>
            {
                { "q", query } ,
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "period", period } ,
                { "order", order } ,
                { "sort", sort } ,
                { "mode", mode } ,

                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            // Decide here whether to use the ECD approach
            bool useEcd = false;
            bool limitReached = perPage * page >= 20000;
            bool ecdLimitReached = perPage * ecdPage >= 20000;
            if (ecd != null && limitReached)
            {
                useEcd = true;
                if (ecdLimitReached || renew)
                {
                    // if an existing ECD run reaches it's limit. Renew ECD and reset ECD page counter. (Add day)
                    DateTime d = DateTime.ParseExact(ecd, "yyyy-MM-dd", new CultureInfo("en-US"));
                    d = d.AddDays(RenewTries * (-1));
                    ecd = d.ToString("yyyy-MM-dd");

                    activeEcd = ecd;
                    ecdPage = 1;

                    RenewTries++;
                }
                else
                {
                    // if existing ECD didn't reach the limit yet, keep increased the page counter
                    if (ecdPage >= 1)
                    {
                        ecdPage++;
                    }
                    else
                    {
                        // this happens if ECD counter hasn't been started yet.
                        ecdPage = 1;
                        searchquery = query;
                        activeEcd = ecd;
                    }
                }
            }

            IsUsingECD = useEcd;
            if (useEcd == false) return await this.AccessApiAsync<Paginated<Work>>(MethodType.GET, url, param);            
            else return await AccessApiAsyncECD();
        }

        public async Task<Tuple<Paginated<Work>, string>> AccessApiAsyncECD()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Referer", "http://spapi.pixiv.net/");
            client.DefaultRequestHeaders.Add("User-Agent", "PixivIOSApp/5.8.0");

            var response = await client.GetAsync($"https://" +
                $"www.pixiv.net/search.php?word={searchquery}&order=date_d&p={ecdPage}&ecd={activeEcd}");

            if (response.IsSuccessStatusCode == false) return new Tuple<Paginated<Work>, string>(new Paginated<Work>(), "Bad Request");

            var src = await response.Content.ReadAsStringAsync();

            // parse contents
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(src);

            var inputs = from input in htmlDoc.DocumentNode.Descendants("li")
                         where input.Attributes["class"] != null
                         where input.Attributes["class"].Value == "image-item"
                         select input;

            Paginated<Work> foundWorks = new Paginated<Work>();

            // go through all entries

            var tasks = new List<Task>();
            foreach(var i in inputs)
            {
                string content = i.InnerHtml;
                var workId = long.Parse(Regex.Match(content, @"data-id=""(\d*?)""").Groups[1].Value);
                Task t = new Task(() =>
                {
                    var wrks = GetWorksAsync(workId).Result;

                    lock (foundWorks)
                    {
                        foundWorks.Add(wrks.Item1.First());
                    }
                });
                tasks.Add(t);
                t.Start();
            }

            await Task.WhenAll(tasks);

            return new Tuple<Paginated<Work>, string>(foundWorks, null); // set paginated works
        }


        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>Works. (Pagenated)</returns>
        public async Task<Tuple<Paginated<Work>, string>> GetLatestWorksAsync(int page = 1, int perPage = 30, bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,

                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await this.AccessApiAsync<Paginated<Work>>(MethodType.GET, url, param);
        }

        public async Task<Tuple<Paginated<Work>, string>> GetRecommendedWorks(int page = 1, int perPage = 30)
        {
            var url = "https://app-api.pixiv.net/v1/illust/recommended";

            var param = new Dictionary<string, string>
            {
                { "offset", (perPage * (page - 1)).ToString() } ,
                { "per_page", perPage.ToString() }
            };

            return await this.AccessApiAsyncNew<Paginated<Work>>(MethodType.GET, url, param);
        }
    }
}
