using MvvmWizard.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class WelcomeViewModel : StepViewModelBase
    {
        private long runCount;
        private long numberOfFilesAnalayzed;
        private long amountOfDataAnalyzed;
        private long duplicateFilesFound;
        private long duplicateFilesRemoved;
        private long amountOfDuplicateDataRemoved;
        private Visibility statisticsPanelVisibility;

        public long RunCount { get => runCount; set => SetProperty(ref runCount, value); }
        public long NumberOfFilesAnalayzed { get => numberOfFilesAnalayzed; set => SetProperty(ref numberOfFilesAnalayzed, value); }
        public long AmountOfDataAnalyzed { get => amountOfDataAnalyzed; set => SetProperty(ref amountOfDataAnalyzed, value); }
        public long DuplicateFilesFound { get => duplicateFilesFound; set => SetProperty(ref duplicateFilesFound, value); }
        public long DuplicateFilesRemoved { get => duplicateFilesRemoved; set => SetProperty(ref duplicateFilesRemoved, value); }
        public long AmountOfDuplicateDataRemoved { get => amountOfDuplicateDataRemoved; set => SetProperty(ref amountOfDuplicateDataRemoved, value); }
        public Visibility StatisticsPanelVisibility { get => statisticsPanelVisibility; set => SetProperty(ref statisticsPanelVisibility, value); }

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

            transitionContext.SharedContext.Clear();

            // Save data here
            await Task.Delay(0);
        }

        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            // Load data here
            StatisticsPanelVisibility = Visibility.Hidden;
            LoadRunStatistics();
            
            return base.OnTransitedTo(transitionContext);
        }

        private void LoadRunStatistics()
        {
            // TODO: this should initially be synchronous and before here to reduce UI delay.
            Task.Run(async () =>
            {
                DB.Database database = new DB.Database();
                List<DB.RunStatistics>? runStatistics = await database.GetAllRunStatistics();

                if (runStatistics != null)
                {
                    RunCount = runStatistics.Count();
                    NumberOfFilesAnalayzed = runStatistics.Sum(x => x.NumberOfFilesAnalayzed);
                    AmountOfDataAnalyzed = runStatistics.Sum(x => x.AmountOfDataAnalyzed);
                    DuplicateFilesFound = runStatistics.Sum(x => x.DuplicateFilesFound);
                    DuplicateFilesRemoved = runStatistics.Sum(x => x.DuplicateFilesRemoved);
                    AmountOfDuplicateDataRemoved = runStatistics.Sum(x => x.AmountOfDuplicateDataRemoved);
                    StatisticsPanelVisibility = runStatistics.Any() ? Visibility.Visible : Visibility.Hidden;
                }
            });
        }
    }
}
