using CryPixivClient.Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient.ViewModels
{
    public class DesignMainViewModel
    {
        ObservableCollection<PixivWork> foundWorks = new ObservableCollection<PixivWork>();

        public ObservableCollection<PixivWork> FoundWorks
        {
            get => foundWorks;
            set { foundWorks = value; }
        }

        public DesignMainViewModel()
        {
            FoundWorks.Add(new PixivWork(new Pixeez.Objects.Work()
            {
                Id = 2312,
                Title = "Some caption",
                PageCount = 4,
                ImageUrls = new Pixeez.Objects.ImageUrls()
                {
                    SquareMedium = "http://i1.pixiv.net/c/128x128/img-master/img/2017/05/15/17/51/51/62914408_p0_square1200.jpg"
                }
            }));
        }
    }
}
