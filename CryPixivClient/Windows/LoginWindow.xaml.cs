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
                ToggleState(false);
                ShowProgress();
                SetProgressState(true);

                // get new accesstoken with refreshtoken...                
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
            MainWindow.Account = new PixivAccount(txtUsername.Text);
            var result = await MainWindow.Account.Login(txtPassword.Password);

            // respond to result
            if (result.Item1 == false)
            {
                SetProgressState(false);
                ToggleState(true);

                txtUsername.Focus();
                txtUsername.SelectAll();
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
