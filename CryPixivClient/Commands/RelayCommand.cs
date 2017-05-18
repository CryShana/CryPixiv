using CryPixivClient.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryPixivClient.Commands
{
    public class RelayCommand<T> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => Callback((T)parameter);

        public Action<T> Callback { get; set; }
        public RelayCommand(Action<T> toDo) => Callback = toDo;
    }
}
