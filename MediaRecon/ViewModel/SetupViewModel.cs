using MethodTimer;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using MvvmWizard.Classes;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ApexBytez.MediaRecon.Analysis;
using System.Windows;
using System;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class SetupViewModel : StepViewModelBase
    {
        public bool forwardButtonIsEnabled;
        private int selectedSourceFolderIndex;
        private ICommand removeSourceFolder;
        private ICommand addSourceFolderCommand;
        private ICommand chooseDestinationFolderCommand;
        private MediaAnalysis analysis;
        private AnalysisOptions analysisOptions = new AnalysisOptions();

        public bool ForwardButtonIsEnabled { get => forwardButtonIsEnabled; set => SetProperty(ref forwardButtonIsEnabled, value); }
        public int SelectedSourceFolderIndex { get => selectedSourceFolderIndex; set => SetProperty(ref selectedSourceFolderIndex, value); }
        public ICommand RemoveSourceFolder => removeSourceFolder ??= new RelayCommand(PerformRemoveSourceFolder);
        public ICommand AddSourceFolderCommand => addSourceFolderCommand ??= new RelayCommand(ExecuteAddSourceFolderCommand);
        public ICommand ChooseDestinationFolderCommand => chooseDestinationFolderCommand ??= new RelayCommand(SelectedReconciliationFolder);
        public MediaAnalysis Analysis { get => analysis; set => SetProperty(ref analysis, value); }
        public AnalysisOptions AnalysisOptions { get => analysisOptions; set => SetProperty(ref analysisOptions, value); }

        public SetupViewModel()
        {
            AnalysisOptions.SourceFolders.CollectionChanged += SourceFolders_CollectionChanged;

            if (Properties.Settings.Default.ReconciliationFolder.Any())
            {
                AnalysisOptions.DestinationDirectory = Properties.Settings.Default.ReconciliationFolder;
            }
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                AnalysisOptions.SourceFolders.RemoveAt(SelectedSourceFolderIndex);
                SelectedSourceFolderIndex = AnalysisOptions.SourceFolders.Count() - 1;
            });
            
        }

        private void ExecuteAddSourceFolderCommand()
        {
            // https://stackoverflow.com/questions/11624298/how-to-use-openfiledialog-to-select-a-folder
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
            dialog.IsFolderPicker = true;
            dialog.Multiselect = true;
            if (Properties.Settings.Default.LastFolderAccessed.Any())
            {
                dialog.InitialDirectory = Properties.Settings.Default.LastFolderAccessed;
            }

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                foreach (var fileName in dialog.FileNames)
                {
                    AnalysisOptions.SourceFolders.Add(fileName);
                }

                var parentDirectory = System.IO.Path.GetDirectoryName(AnalysisOptions.SourceFolders.First());
                Properties.Settings.Default.LastFolderAccessed = parentDirectory;
                Properties.Settings.Default.Save();
            }
        }

        private void SelectedReconciliationFolder()
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            // TODO: remember last directory viewed.
            dialog.DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
            dialog.IsFolderPicker = true;
            if (Properties.Settings.Default.ReconciliationFolder.Any())
            {
                dialog.InitialDirectory = Properties.Settings.Default.ReconciliationFolder;
            }
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                AnalysisOptions.DestinationDirectory = dialog.FileName;
                // TODO: We can probably use this in subsequent runs but 
                //  we'll have to store it and reload.

                Properties.Settings.Default.ReconciliationFolder = dialog.FileName;
                Properties.Settings.Default.Save();
            }
        }
    }
}
