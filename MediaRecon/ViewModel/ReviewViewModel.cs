using ApexBytez.MediaRecon.View;
using MvvmWizard.Classes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class ReviewViewModel : StepViewModelBase
    {
        public override async Task OnTransitedFrom(TransitionContext transitionContext)
        {
            if (transitionContext.TransitToStep < transitionContext.TransitedFromStep)
            {
                // Moving back
                return;
            }

            if (transitionContext.IsSkipAction)
            {
                // Skip button clicked
                return;
            }

            // TODO: Save the results

            // Save data here
            await Task.Delay(0);
        }

        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            Analysis = transitionContext.SharedContext["Analysis"] as Analysis;

            // Load data here
            return base.OnTransitedTo(transitionContext);
        }

        private Analysis? analysis;
        public Analysis Analysis { get => analysis; set => SetProperty(ref analysis, value); }

    }
}
