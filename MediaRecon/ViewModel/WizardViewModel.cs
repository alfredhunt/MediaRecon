using MediaRecon;
using Microsoft.Toolkit.Mvvm.Input;
using MvvmWizard.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class WizardViewModel
    {
        public WizardViewModel()
        {
            this.SharedContext = new Dictionary<string, object>();
        }

        public Dictionary<string, object> SharedContext { get; }

        private RelayCommand closeCommand;
        public RelayCommand CloseCommand => closeCommand ??= new RelayCommand(Close);

        private void Close()
        {
            Debug.WriteLine("WizardViewModel.Close()");
        }
    }
}
