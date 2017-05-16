using CryPixivClient.Properties;
using CryPixivClient.ViewModels;
using CryPixivClient.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CryPixivClient
{
    public partial class MainWindow : Window
    {
        public static MainViewModel MainModel;
        public static PixivAccount Account = null;
        public MainWindow()
        {
            InitializeComponent();
            LoadWindowData();
            LoadAccount();

            PixivAccount.AuthFailed += AuthenticationFailed;
            MainModel = (MainViewModel)FindResource("mainViewModel");
            DataContext = MainModel;

            ShowLoginPrompt();
        }

        private void AuthenticationFailed(object sender, string e)
        {
            ShowLoginPrompt(true);
        }

        void ShowLoginPrompt(bool force = false)
        {
            if (Account?.AuthDetails?.IsExpired == false && force == false) return;

            LoginWindow login = new LoginWindow(Account != null);
            login.ShowDialog();

            if (Account == null || Account.IsLoggedIn == false) { Environment.Exit(1); return; }

            SaveAccount();
        }

        void LoadWindowData()
        {
            if (Settings.Default.WindowHeight > 10)
            {
                Height = Settings.Default.WindowHeight;
                Width = Settings.Default.WindowWidth;
                Left = Settings.Default.WindowLeft;
                Top = Settings.Default.WindowTop;
            }
        }

        void LoadAccount()
        {
            if (Settings.Default.Username.Length < Settings.Default.MinUsernameLength) return;
            Account = new PixivAccount(Settings.Default.Username);

            Account.LoginWithAccessToken(
                Settings.Default.AuthAccessToken,
                Settings.Default.AuthRefreshToken,
                Settings.Default.AuthExpiresIn,
                DateTime.Parse(Settings.Default.AuthIssued));
        }

        void SaveAccount()
        {
            // save account data
            Settings.Default.Username = Account.Username;
            Settings.Default.AuthIssued = Account.AuthDetails.TimeIssued.ToString();
            Settings.Default.AuthExpiresIn = Account.AuthDetails.ExpiresIn ?? 0;
            Settings.Default.AuthAccessToken = Account.AuthDetails.AccessToken;
            Settings.Default.AuthRefreshToken = Account.AuthDetails.RefreshToken;
            Settings.Default.Save();
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // save window data
            Settings.Default.WindowHeight = Height;
            Settings.Default.WindowWidth = Width;
            Settings.Default.WindowLeft = Left;
            Settings.Default.WindowTop = Top;
            Settings.Default.Save();
        }
    }
}
