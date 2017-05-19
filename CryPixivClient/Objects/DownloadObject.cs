using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient.Objects
{
    public class DownloadObject : INotifyPropertyChanged
    {
        bool isError = false;
        double percentage = 0.0;
        int completedPages = 0;
        string errormsg = "";

        public PixivWork Work { get; set; }
        public bool IsError { get => isError; set { isError = value; Changed(); } }
        public bool IsCompleted => (Percentage == 100.0) ? true : false;
        public bool IsWorking => (Percentage > 0 && percentage < 100) ? true : false;
        public double Percentage
        {
            get => percentage;
            set
            {
                percentage = value;
                Changed();
                Changed("PercentageText");
                Changed("IsCompleted");
                Changed("IsWorking");
            }
        }
        public string PercentageText => Math.Round(Percentage, 2).ToString("0.00") + "%";
        public int CompletedPages { get => completedPages; set { completedPages = value; Changed(); Changed("CompletedPagesText"); } }
        public string CompletedPagesText => $"{CompletedPages}/{Work.PageCount.Value}";
        public string ErrorMessage { get => errormsg; set { errormsg = value; Changed(); } }

        public DownloadObject(PixivWork work)
        {
            this.Work = work;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void Changed([CallerMemberName]string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
