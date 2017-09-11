using CryPixivClient.Objects;
using System;
using System.Collections.Generic;
using System.Windows;

namespace CryPixivClient.Windows
{
    public partial class BookmarkSearch : Window
    {
        public List<string> ToFilter { get; set; } = new List<string>();

        public BookmarkSearch(List<PixivWork> works)
        {
            InitializeComponent();
            txtFilter.Focus();

            // get recent tags
            var tags = new List<string>();
            foreach (var b in works)
                foreach (var t in b.Tags) if (tags.Contains(t) == false && tags.Count < 50) tags.Add(t);
            
            ccTags.ItemsSource = WorkDetails.GetTranslatedTags(tags);
        }

        void ConfirmClick(object sender, RoutedEventArgs e)
        {
            var txt = txtFilter.Text.Trim();

            if (string.IsNullOrEmpty(txt)) Close();

            if (txt.Contains(" OR "))
            {
                var tags = txt.Split(new string[] { " OR " }, StringSplitOptions.RemoveEmptyEntries);
                ToFilter.AddRange(tags);
            }
            else
            {
                ToFilter.Add(txt);
            }

            Close();
        }

        void ccTags_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ccTags.SelectedIndex == -1) return;

            var text = ccTags.SelectedItem as Translation;
            Clipboard.SetText(text.Original);
        }
    }
}
