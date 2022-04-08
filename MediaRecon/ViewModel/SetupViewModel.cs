using MethodTimer;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using MvvmWizard.Classes;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class SetupViewModel : StepViewModelBase
    {
        private AnalysisOptions analysisOptions = new AnalysisOptions();
        private int selectedSourceFolderIndex;
        private ICommand removeSourceFolder;
        private ICommand addSourceFolderCommand;
        private ICommand chooseDestinationFolderCommand;
        private Analysis analysis;
        public bool forwardButtonIsEnabled;

        public bool ForwardButtonIsEnabled { get => forwardButtonIsEnabled; set => SetProperty(ref forwardButtonIsEnabled, value); }
        public int SelectedSourceFolderIndex { get => selectedSourceFolderIndex; set => SetProperty(ref selectedSourceFolderIndex, value); }
        public ICommand RemoveSourceFolder => removeSourceFolder ??= new RelayCommand(PerformRemoveSourceFolder);
        public ICommand AddSourceFolderCommand => addSourceFolderCommand ??= new RelayCommand(ExecuteAddSourceFolderCommand);
        public ICommand ChooseDestinationFolderCommand => chooseDestinationFolderCommand ??= new RelayCommand(SelectedReconciliationFolder);
        public Analysis Analysis { get => analysis; set => SetProperty(ref analysis, value); }
        public AnalysisOptions AnalysisOptions { get => analysisOptions; set => SetProperty(ref analysisOptions, value); }

        public SetupViewModel()
        {
            AnalysisOptions.SourceFolders.CollectionChanged += SourceFolders_CollectionChanged;

            // DEBUG
            AnalysisOptions.SourceFolders.Add(@"F:\Pictures\Wedding");
            AnalysisOptions.SourceFolders.Add(@"F:\Pictures\OurWedding");
            AnalysisOptions.DestinationDirectory = @"F:\TestResults";
        }

        private void SourceFolders_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ForwardButtonIsEnabled = AnalysisOptions.SourceFolders.Any();
        }

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

            transitionContext.SharedContext["AnalysisOptions"] = AnalysisOptions;

            // Save data here
            await Task.Delay(0);
        }
        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            if (transitionContext.TransitToStep > transitionContext.TransitedFromStep)
            {
                // TODO: Might ultimately need to do something here, unsure.
                // ... Possibly on some kind of clear/new operation.
            }

            // Load data here
            return base.OnTransitedTo(transitionContext);
        }
        private void PerformRemoveSourceFolder()
        {
            AnalysisOptions.SourceFolders.RemoveAt(SelectedSourceFolderIndex);
            SelectedSourceFolderIndex = -1;
        }

        private void ExecuteAddSourceFolderCommand()
        {
            // https://stackoverflow.com/questions/11624298/how-to-use-openfiledialog-to-select-a-folder
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                AnalysisOptions.SourceFolders.Add(dialog.FileName);
            }
        }

        private void SelectedReconciliationFolder()
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            // TODO: remember last directory viewed.
            dialog.InitialDirectory = "C:\\";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                AnalysisOptions.DestinationDirectory = dialog.FileName;
            }
        }
    }
}
