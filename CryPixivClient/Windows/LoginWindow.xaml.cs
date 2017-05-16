using CryPixivClient.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CryPixivClient.Windows
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(bool useExisting)
        {
            InitializeComponent();

            if (useExisting == false)
            {
                this.Height = 123;
                txtUsername.Focus();
            }
            else
            {
                txtUsername.Text = Settings.Default.Username;
                ToggleState(false);
                SetProgressState(true);
                ShowProgress();

                // get new accesstoken with refreshtoken...      
                AttemptUpdateLogin();
            }
        }

        async void Login_Click(object sender, RoutedEventArgs e)
        {
            if (txtUsername.Text.Length < Settings.Default.MinUsernameLength && 
                txtPassword.Password.Length < Settings.Default.MinPasswordLength) return;

            // show progress, disable controls
            ToggleState(false);
            SetProgressState(true);
            ShowProgress();

            // attempt login
            var result = await AttemptLogin(txtUsername.Text, txtPassword.Password);

            // respond to result
            if (result == false)
            {
                SetProgressState(false);
                ToggleState(true);

                txtUsername.Focus();
                txtUsername.SelectAll();
            }
            else Close();
        }

        async Task<bool> AttemptLogin(string username, string password)
        {
            MainWindow.Account = new PixivAccount(txtUsername.Text);            
            var result = await MainWindow.Account.Login(txtPassword.Password);
            if (result.Item1)
            {
                // automatically store encrypted password - because refresh tokens are not working with pixiv
                Settings.Default.AuthPassword = PixivAccount.EncryptPassword(txtPassword.Password, MainWindow.Account.AuthDetails.RefreshToken);
                Settings.Default.Save();
            }
            return result.Item1;
        }
        async void AttemptUpdateLogin()
        {
            var result = await MainWindow.Account.UpdateLogin(Settings.Default.AuthPassword);

            if (result == false)
            {
                SetProgressState(false);
                ToggleState(true);

                txtPassword.Focus();
                txtPassword.SelectAll();
            }
            else Close();
        }
        
        void ToggleState(bool state)
        {
            txtUsername.IsEnabled = state;
            txtPassword.IsEnabled = state;
            btnLogin.IsEnabled = state;
        }

        void ShowProgress()
        {
            DoubleAnimation dan = new DoubleAnimation(171, TimeSpan.FromSeconds(0.5))
            {
                EasingFunction = new PowerEase() { Power = 2 }
            };
            BeginAnimation(HeightProperty, dan);
        }

        void SetProgressState(bool working)
        {
            if (working)
            {
                txtStatus.Text = "Logging in...";
                txtStatus.Foreground = Brushes.Black;
                progressBar.IsIndeterminate = true;
            }
            else
            {
                txtStatus.Text = "Login failed!";
                txtStatus.Foreground = Brushes.Red;
                progressBar.IsIndeterminate = false;
            }
        }       
    }
}
