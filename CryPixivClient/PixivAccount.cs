using CryPixivClient.Objects;
using CryPixivClient.Properties;
using CryPixivClient.ViewModels;
using Pixeez;
using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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

        public async Task<User> GetUser(long userId)
        {
            try
            {
                var result = await tokens.GetUsersAsync(userId);
                return result.Item1.First();
            }
            catch
            {
                return null;
            }
        }

        public async Task<Tuple<bool, string>> FollowUser(long userId, bool isPublic = true)
        {
            try
            {
                var result = await tokens.FollowUnfollowUser(true, userId, isPublic);
                return result;
            }
            catch (Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
        }
        public async Task<Tuple<bool, string>> UnfollowUser(long userId, bool isPublic = true)
        {
            try
            {
                var result = await tokens.FollowUnfollowUser(false, userId, isPublic);
                return result;
            }
            catch (Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
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

                IsLoggedIn = true;
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

        public bool renewNecessary = false;
        string lastEcd = null;
        #region Getting Data
        public async Task<Paginated<Work>> SearchWorks(string searchQuery, int page = 1)
        {
            Tuple<Paginated<Work>, string> result = null;
            try
            {
                result = await GetData(() => tokens.SearchWorksAsync(searchQuery, page, mode: "tag", perPage: MainViewModel.DefaultPerPage, ecd: lastEcd, renew: renewNecessary));
            }
            catch (Exception ex)
            {
                return null;
            }

            if (result == null || result.Item1 == null) return new Paginated<Work>();

            // get last ECD
            var date = result.Item1.Last().CreatedTime.Date;
            lastEcd = date.ToString("yyyy-MM-dd");

            // return
            return result.Item1;
        }
        public async Task<List<PixivWork>> GetRanking(int page = 1, string rtype = "day")
        {
            var result = await GetData(() => tokens.GetRankingAllAsync(page: page, perPage: MainViewModel.DefaultPerPage, mode: rtype));

            if (result == null || result.Item1 == null) return new List<PixivWork>();
            return result.Item1.ToPixivWork();
        }
        public async Task<List<PixivWork>> GetFollowing(int page = 1, Publicity publicity = Publicity.Public)
        {
            var result = await GetData(() => tokens.GetMyFollowingWorksAsync(page: page, publicity: publicity.ToString().ToLower(), perPage: MainViewModel.DefaultPerPage));

            if (result == null || result.Item1 == null) return new List<PixivWork>();
            return result.Item1.ToPixivWork();
        }
        public async Task<List<PixivWork>> GetBookmarks(int page = 1, Publicity publicity = Publicity.Public)
        {
            var result = await GetData(() => tokens.GetMyFavoriteWorksAsync(page: page, publicity: publicity.ToString().ToLower(), perPage: MainViewModel.DefaultPerPage));

            if (result == null || result.Item1 == null) return new List<PixivWork>();
            return result.Item1.Select(x => x.Work).ToPixivWork();
        }
        public async Task<List<PixivWork>> GetRecommended(int page = 1)
        {
            var result = await GetData(() => tokens.GetRecommendedWorks(page: page, perPage: MainViewModel.DefaultPerPage));
            return result.Item1.ToPixivWork();
        }
        public async Task<List<PixivWork>> GetUserWorks(long userId, int page = 1)
        {
            var result = await GetData(() => tokens.GetUsersWorksAsync(userId, page: page, perPage: MainViewModel.DefaultPerPage));

            if (result == null || result.Item1 == null) return new List<PixivWork>();
            return result.Item1.ToPixivWork();
        }

        void ShowError(string msg, bool accessTokenExpired = false)
        {
            IsLoggedIn = false;
            if (MainWindow.IsClosing) return;

            MainWindow.ShowingError = true;
            if (accessTokenExpired == false) MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            AuthFailed?.Invoke(this, msg);
        }
        async Task<Tuple<T, string>> GetData<T>(Func<Task<Tuple<T, string>>> toDo)
        {
            try
            {
                MainWindow.ShowingError = false;
                if (AuthDetails.IsExpired) throw new Exception("Expired session! Please login again!");

                var result = await toDo();
                if (result == null || string.IsNullOrEmpty(result.Item2) == false)
                {
                    if (result.Item2 == Tokens.AccessTokenErrorMessage)
                    {
                        // access token expired, just relogin without error message and automatically continue the search if user was searching
                        ShowError(result.Item2, true);
                    }
                    else ShowError((result == null) ? "Unknown error." : result.Item2);
                }

                return result;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return null;
            }
        }
        #endregion

        public async Task<Tuple<bool, long?>> AddToBookmarks(long workId, bool isPublic = true)
        {
            try
            {
                var result = await tokens.AddMyFavoriteWorksAsync(workId, publicity: ((isPublic) ? "public" : "private"));
                if (result == null || string.IsNullOrEmpty(result.Item2) == false) ShowError((result == null) ? "Unknown error." : result.Item2);
                return new Tuple<bool, long?>(true, result.Item1.First().Id);
            }
            catch
            {
                return new Tuple<bool, long?>(false, null); ;
            }
        }
        public async Task<bool> RemoveFromBookmarks(long workId)
        {
            try
            {
                var result = await tokens.DeleteMyFavoriteWorksAsync(workId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[] DownloadImage(string url) => tokens.DownloadImage(url);
        public async Task<byte[]> DownloadImageAsync(string url) => await tokens.DownloadImageAsync(url);

        public static string EncryptPassword(string password, string refreshtoken)
        {
            try
            {
                var aes = Aes.Create();

                aes.Padding = PaddingMode.ISO10126;
                aes.Mode = CipherMode.CBC;
                aes.IV = Encoding.ASCII.GetBytes(refreshtoken.ToUpper()).Take(16).ToArray();
                aes.Key = Encoding.ASCII.GetBytes(refreshtoken).Take(32).ToArray();

                using (var mstream = new MemoryStream())
                {
                    using (var crstream = new CryptoStream(mstream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        using (var writer = new StreamWriter(crstream))
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
            BookmarksPublic,
            BookmarksPrivate,
            Recommended,
            User
        }
    }
}
