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
            RenamedFiles = transitionContext.SharedContext["RenamedFiles"] as IEnumerable<ConflictedFiles>;
            reconciled = transitionContext.SharedContext["Reconciled"] as IEnumerable<IFolderViewItem>;

            Debug.Assert(RenamedFiles != null);
            Debug.Assert(reconciled != null);

            // Load data here
            return base.OnTransitedTo(transitionContext);
        }
        private IEnumerable<IFolderViewItem> reconciled = new List<IFolderViewItem>();

        private IEnumerable<ConflictedFiles> renamedFiles = new List<ConflictedFiles>();

        public IEnumerable<ConflictedFiles> RenamedFiles { get => renamedFiles; set => SetProperty(ref renamedFiles, value); }
    }
}
