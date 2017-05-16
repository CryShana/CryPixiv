using Pixeez.Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void Changed([CallerMemberName]string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #region Private fields
        ObservableCollection<Work> foundWorks;
        string status = "Idle";
        #endregion

        #region Properties
        public ObservableCollection<Work> FoundWorks
        {
            get { return foundWorks; }
            set { foundWorks = value; Changed(); }
        }

        public string Status
        {
            get { return status; }
            set { status = value; Changed(); }
        }

        #endregion
    }
}
