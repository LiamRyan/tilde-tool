using System;
using System.Windows.Input;

namespace Tildetool
{
    class CloseCommand : ICommand
    {
      public void Execute(object? parameter)
      {
         App.Current.Shutdown();
      }
      public bool CanExecute(object? parameter)
      {
         return true;
      }
      public event EventHandler? CanExecuteChanged;
   }
}
