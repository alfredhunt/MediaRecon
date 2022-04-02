
using Microsoft.Toolkit.Mvvm.Input;
using MvvmWizard.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class SaveViewModel : StepViewModelBase
    {
        public override async Task OnTransitedFrom(TransitionContext transitionContext)
        {
            if (transitionContext.TransitToStep < transitionContext.TransitedFromStep)
            {
                // Moving back
                if (Analysis.Running)
                {
                    try
                    {
                        Analysis.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
                return;
            }

            if (transitionContext.IsSkipAction)
            {
                // Skip button clicked
                return;
            }

            transitionContext.SharedContext["Analysis"] = Analysis;

            // Save data here
            await Task.Delay(0);
        }

        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            Analysis = transitionContext.SharedContext["Analysis"] as Analysis;

            if (transitionContext.TransitToStep > transitionContext.TransitedFromStep)
            {
                // Forward transition, do Analysis if setup has changed

                // Start the analysis...
                Task.Run(() => SaveResults());

            }

            return base.OnTransitedTo(transitionContext);
        }

        private async void SaveResults()
        {
            // Has the analsis already ran? Don't run it again unless the configuration changes
            ForwardButtonIsEnabled = false;
            try
            {
                await Analysis.SaveResultsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            ForwardButtonIsEnabled = true;
        }

        private Analysis? analysis;
        public bool forwardButtonIsEnabled;

        public Analysis Analysis { get => analysis; set => SetProperty(ref analysis, value); }
        public bool ForwardButtonIsEnabled { get => forwardButtonIsEnabled; set => SetProperty(ref forwardButtonIsEnabled, value); }
    }
}


