using CryPixivClient.Objects;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace CryPixivClient.Windows
{
    public partial class WorkDetails : Window
    {
        public PixivWork LoadedWork { get; }
        public WorkDetails(PixivWork work)
        {
            InitializeComponent();
            LoadedWork = work;
        }
    }
}
