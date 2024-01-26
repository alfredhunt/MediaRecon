using MvvmWizard.Classes;
using System.Threading.Tasks;

namespace MediaRecon.ViewModel
{
    internal class WelcomeViewModel : StepViewModelBase
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

            // Save data here
            await Task.Delay(0);
        }

        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            // Load data here
            return base.OnTransitedTo(transitionContext);
        }
    }
}
