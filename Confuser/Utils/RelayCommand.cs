using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Confuser
{
    class RelayCommand : ICommand
    {
        public RelayCommand(Func<object, bool> canExe, Action<object> exe)
        {
            this.canExe = canExe;
            this.exe = exe;
        }
        Func<object, bool> canExe;
        Action<object> exe;

        public bool CanExecute(object parameter)
        {
            return canExe(parameter);
        }

        EventHandler canExeChanged;
        public event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
                canExeChanged = (EventHandler)Delegate.Combine(value, canExeChanged);
            }
            remove
            {
                CommandManager.RequerySuggested -= value;
                canExeChanged = (EventHandler)Delegate.Remove(canExeChanged, value);
            }
        }

        public void OnCanExecuteChanged()
        {
            canExeChanged(this, EventArgs.Empty);
        }

        public void Execute(object parameter)
        {
            exe(parameter);
        }
    }
}
