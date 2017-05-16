using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace CryPixivClient.Converters
{
    public class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // NOT YET IMPLEMENTED -- this is too slow
            string uri = (string)value;

            try
            {
                var str = MainWindow.Account.DownloadImage(uri);

                using (var mstream = new MemoryStream(str))
                {
                    BitmapImage img = new BitmapImage();
                    img.BeginInit();
                    img.StreamSource = mstream;
                    img.EndInit();
                    return img;
                }
            }
            catch
            {
                // do nothing
                return new BitmapImage();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
