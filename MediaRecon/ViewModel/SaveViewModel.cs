
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
using ApexBytez.MediaRecon.Analysis;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class SaveViewModel : StepViewModelBase
    {
        public bool forwardButtonIsEnabled;
        private SaveResults saveResults;

        public bool ForwardButtonIsEnabled { get => forwardButtonIsEnabled; set => SetProperty(ref forwardButtonIsEnabled, value); }
        public SaveResults SaveResults { get => saveResults; private set => SetProperty(ref saveResults, value); }

        public override async Task OnTransitedFrom(TransitionContext transitionContext)
        {
            if (transitionContext.TransitToStep < transitionContext.TransitedFromStep)
            {
                // Moving back
                // TODO: ask user if they want to cancel the operation
                // ... or maybe we just let it continue
                //if (Analysis.Running)
                //{
                //    try
                //    {
                //        Analysis.Cancel();
                //    }
                //    catch (Exception ex)
                //    {
                //        Debug.WriteLine(ex);
                //    }
                //}
                return;
            }

            if (transitionContext.IsSkipAction)
            {
                // Skip button clicked
                return;
            }

            // Save data here
            await Task.Delay(0);
        }

        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            var analysisOptions = transitionContext.SharedContext["AnalysisOptions"] as AnalysisOptions;
            var analysisResults = transitionContext.SharedContext["AnalysisResults"] as AnalysisResults;

            if (transitionContext.TransitToStep > transitionContext.TransitedFromStep)
            {
                // Forward transition, do Analysis if setup has changed

                // Save the analysis results
                Task.Run(async () =>
                {
                    // Has the analsis already ran? Don't run it again unless the configuration changes
                    ForwardButtonIsEnabled = false;
                    SaveResults = new SaveResults(analysisOptions, analysisResults);
                    try
                    {
                        await SaveResults.RunAsync();
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


    }
}


