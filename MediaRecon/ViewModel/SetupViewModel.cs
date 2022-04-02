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
        public SetupViewModel()
        {
            
        }

        private void SourceFolders_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ForwardButtonIsEnabled = Analysis.SourceFolders.Any();
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

            transitionContext.SharedContext["Analysis"] = Analysis;

            // Save data here
            await Task.Delay(0);
        }
        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            if (transitionContext.TransitToStep > transitionContext.TransitedFromStep)
            {
                ForwardButtonIsEnabled = false;

                Analysis = new Analysis();

                Analysis.SourceFolders.CollectionChanged += SourceFolders_CollectionChanged;

                Analysis.SourceFolders.Add(@"F:\Pictures\Wedding");
                Analysis.SourceFolders.Add(@"F:\Pictures\OurWedding");
                Analysis.DestinationDirectory = @"F:\";
            }

            // Load data here
            return base.OnTransitedTo(transitionContext);
        }
        private void PerformRemoveSourceFolder()
        {
            Analysis.SourceFolders.RemoveAt(SelectedSourceFolderIndex);
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
                Analysis.SourceFolders.Add(dialog.FileName);
            }
        }

        private int selectedSourceFolderIndex;
        private ICommand removeSourceFolder;
        private ICommand addSourceFolderCommand;
        private Analysis analysis;

        public bool forwardButtonIsEnabled;
        public bool ForwardButtonIsEnabled { get => forwardButtonIsEnabled; set => SetProperty(ref forwardButtonIsEnabled, value); }
        public int SelectedSourceFolderIndex { get => selectedSourceFolderIndex; set => SetProperty(ref selectedSourceFolderIndex, value); }
        public ICommand RemoveSourceFolder => removeSourceFolder ??= new RelayCommand(PerformRemoveSourceFolder);
        public ICommand AddSourceFolderCommand => addSourceFolderCommand ??= new RelayCommand(ExecuteAddSourceFolderCommand);
        public Analysis Analysis { get => analysis; set => SetProperty(ref analysis, value); }

        private RelayCommand selectedDestinationDirectoryCommand;
        public ICommand SelectedDestinationDirectoryCommand => selectedDestinationDirectoryCommand ??= new RelayCommand(SelectedDestinationDirectory);

        private void SelectedDestinationDirectory()
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            // TODO: remember last directory viewed.
            dialog.InitialDirectory = "C:\\";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Analysis.DestinationDirectory = dialog.FileName;
            }
        }
    }
}
