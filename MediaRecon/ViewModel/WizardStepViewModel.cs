using MvvmWizard.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class WizardStepViewModel : StepViewModelBase
    {
        private bool forwardButtonIsEnabled;
        private bool backButtonIsEnabled;

        public bool ForwardButtonIsEnabled { get => forwardButtonIsEnabled; set => SetProperty(ref forwardButtonIsEnabled, value); }
        public bool BackButtonIsEnabled { get => backButtonIsEnabled; set => SetProperty(ref backButtonIsEnabled, value); }

        public void DisabledNavigation()
        {
            ForwardButtonIsEnabled = false;
            BackButtonIsEnabled = false;
        }

        public void EnableNavigation()
        {
            ForwardButtonIsEnabled = true;
            BackButtonIsEnabled = true;
        }
    }

}
