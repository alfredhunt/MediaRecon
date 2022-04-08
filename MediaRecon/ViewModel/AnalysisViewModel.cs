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
    internal class AnalysisViewModel : StepViewModelBase
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

            transitionContext.SharedContext["AnalysisResults"] = Analysis.AnalysisResults;

            // Save data here
            await Task.Delay(0);
        }

        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            

            if (transitionContext.TransitToStep > transitionContext.TransitedFromStep)
            {
                var options = transitionContext.SharedContext["AnalysisOptions"] as AnalysisOptions;

                // Forward transition, do Analysis if setup has changed

                // Start the analysis...
                Task.Run(async () =>
                {
                    // Has the analsis already ran? Don't run it again unless the configuration changes
                    ForwardButtonIsEnabled = false;
                    Analysis = new Analysis(options);
                    try
                    {
                        await Analysis.RunAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }

                    ForwardButtonIsEnabled = true;
                });

            }

            return base.OnTransitedTo(transitionContext);
        }

        private Analysis? analysis;
        public bool forwardButtonIsEnabled;
   
        public Analysis Analysis { get => analysis; set => SetProperty(ref analysis, value); }
        public bool ForwardButtonIsEnabled { get => forwardButtonIsEnabled; set => SetProperty(ref forwardButtonIsEnabled, value); }
    }
}
