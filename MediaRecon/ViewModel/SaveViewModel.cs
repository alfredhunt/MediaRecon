
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
    internal class SaveViewModel : WizardStepViewModel
    {
        private SaveResultsStep saveResults;
        public SaveResultsStep SaveResults { get => saveResults; private set => SetProperty(ref saveResults, value); }

        public override async Task OnTransitedFrom(TransitionContext transitionContext)
        {
            if (transitionContext.TransitToStep < transitionContext.TransitedFromStep)
            {
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
                if (!transitionContext.SharedContext.ContainsKey("ReconciliationStatistics"))
                {
                    // Save the analysis results
                    Task.Run(async () =>
                    {
                        // Has the analsis already ran? Don't run it again unless the configuration changes
                        SaveResults = new SaveResultsStep(analysisOptions, analysisResults);
                        try
                        {
                            DisabledNavigation();
                            await SaveResults.RunAsync();
                            transitionContext.SharedContext["ReconciliationStatistics"] = SaveResults;
                            // TODO: Save the stats to the DB to show on the welcome page!
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                        finally
                        {
                            EnableNavigation();
                        }
                    });
                }

            }

            return base.OnTransitedTo(transitionContext);
        }


    }
}


