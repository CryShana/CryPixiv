using CryPixivClient.Properties;
using Pixeez;
using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient
{
    public class PixivAccount
    {
        public string Username { get; set; }
        public bool IsLoggedIn { get; set; }
        public Authorize AuthDetails => tokens?.AuthDetails;
        public static event EventHandler<string> AuthFailed;


        Tokens tokens;

        public PixivAccount(string username)
        {
            Username = username;
        }

        public Tuple<bool, string> LoginWithAccessToken(string accesstoken, string refreshtoken, int expiresin, DateTime issued)
        {
            try
            {
                tokens = Auth.AuthorizeWithAccessToken(accesstoken, refreshtoken, expiresin, issued);

                IsLoggedIn = true;
                return new Tuple<bool, string>(true, "Success");
            }
            catch (Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
        }
        public async Task<bool> UpdateLogin(string encPassword)
        {
            try
            {
                var pass = DecryptPassword(encPassword, AuthDetails.RefreshToken);
                tokens = await Auth.AuthorizeAsync(Username, pass);

                // encrypt password again with the new refresh token
                Settings.Default.AuthPassword = EncryptPassword(pass, MainWindow.Account.AuthDetails.RefreshToken);
                Settings.Default.Save();

                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<Tuple<bool, string>> Login(string password)
        {
            try
            {
                tokens = await Auth.AuthorizeAsync(Username, password);

                IsLoggedIn = true;
                return new Tuple<bool, string>(true, "Success");
            }
            catch (Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
        }
        public async Task<Paginated<Work>> SearchWorks(string searchQuery, int page = 1) => await GetData(() => tokens.SearchWorksAsync(searchQuery, page));
        public async Task<List<Work>> GetDailyRanking(int page = 1)
        {
            var result = await GetData(() => tokens.GetRankingAllAsync(page: page));
            if (result == null) return null;

            var works = result.First().Works.Select(x => x.Work).ToList();
            return works;
        }
        public async Task<List<Work>> GetFollowing(int page = 1, Publicity publicity = Publicity.Public)
        {
            var result = await GetData(() => tokens.GetMyFollowingWorksAsync(page: page, publicity: publicity.ToString().ToLower()));
            return result.ToList();
        }
        public async Task<List<Work>> GetBookmarks(int page = 1, Publicity publicity = Publicity.Public)
        {
            var result = await GetData(() => tokens.GetMyFavoriteWorksAsync(page: page, publicity: publicity.ToString().ToLower()));
            return result.Select(x => x.Work).ToList();
        }

        async Task<T> GetData<T>(Func<Task<T>> toDo)
        {
            try
            {
                if (AuthDetails.IsExpired) throw new Exception("Expired AuthToken!");

                var result = await toDo();
                return result;
            }
            catch (Exception ex)
            {
                IsLoggedIn = false;
                AuthFailed?.Invoke(this, ex.Message);
                return default(T);
            }
        }

        public static string EncryptPassword(string password, string refreshtoken)
        {
            try
            {
                var aes = Aes.Create();

                aes.Padding = PaddingMode.ISO10126;
                aes.Mode = CipherMode.CBC;
                aes.IV = Encoding.ASCII.GetBytes(refreshtoken.ToUpper()).Take(16).ToArray();
                aes.Key = Encoding.ASCII.GetBytes(refreshtoken).Take(32).ToArray();

                using(var mstream = new MemoryStream())
                {
                    using(var crstream = new CryptoStream(mstream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        using(var writer = new StreamWriter(crstream))
                        {
                            writer.Write(password);
                        }
                    }

                    return Convert.ToBase64String(mstream.ToArray());
                }
            }
            catch (Exception ex)
            {
                return "";
            }
        }
        public static string DecryptPassword(string encryptedPassword, string refreshtoken)
        {
            try
            {
                var aes = Aes.Create();

                aes.Padding = PaddingMode.ISO10126;
                aes.Mode = CipherMode.CBC;
                aes.IV = Encoding.ASCII.GetBytes(refreshtoken.ToUpper()).Take(16).ToArray();
                aes.Key = Encoding.ASCII.GetBytes(refreshtoken).Take(32).ToArray();

                using (var mstream = new MemoryStream(Convert.FromBase64String(encryptedPassword)))
                {
                    using (var crstream = new CryptoStream(mstream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        string content = "";
                        using (var writer = new StreamReader(crstream))
                        {
                            content = writer.ReadToEnd();
                        }
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public enum Publicity
        {
            Public,
            Private
        }
        public enum WorkMode
        {
            Ranking,
            Following,
            Search,
            Bookmarks
        }
    }
}
