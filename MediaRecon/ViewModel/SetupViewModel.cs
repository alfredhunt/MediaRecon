using MethodTimer;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using MvvmWizard.Classes;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text.Json;

namespace MediaRecon.ViewModel
{
    internal class SetupViewModel : StepViewModelBase
    {
        public bool ForwardButtonIsEnabled { get; set; } = true; // test value right now
        private ObservableCollection<string> sourceFolders = new ObservableCollection<string>();
        public ObservableCollection<string> SourceFolders { get => sourceFolders; set => SetProperty(ref sourceFolders, value); }
        private int selectedSourceFolderIndex;
        public int SelectedSourceFolderIndex { get => selectedSourceFolderIndex; set => SetProperty(ref selectedSourceFolderIndex, value); }
        private ICommand removeSourceFolder;
        private ICommand addSourceFolderCommand;
        public ICommand RemoveSourceFolder => removeSourceFolder ??= new RelayCommand(PerformRemoveSourceFolder);
        public ICommand AddSourceFolderCommand => addSourceFolderCommand ??= new RelayCommand(ExecuteAddSourceFolderCommand);

        public SetupViewModel()
        {
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


            transitionContext.SharedContext["Folders"] = SourceFolders.AsEnumerable();

            // Save data here
            await Task.Delay(0);
        }
        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            // Load data here
            string json = Properties.Settings.Default.SourceFolders;
            if (!string.IsNullOrEmpty(json))
            {
                SourceFolders = JsonSerializer.Deserialize<ObservableCollection<string>>(json);
            }
            


            return base.OnTransitedTo(transitionContext);
        }
        private void PerformRemoveSourceFolder()
        {
            SourceFolders.RemoveAt(SelectedSourceFolderIndex);
            SelectedSourceFolderIndex = -1;
            PersistSettings();
        }

        private void ExecuteAddSourceFolderCommand()
        {
            // https://stackoverflow.com/questions/11624298/how-to-use-openfiledialog-to-select-a-folder
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                sourceFolders.Add(dialog.FileName);
                PersistSettings();
            }
        }

        private void PersistSettings()
        {
            string json = JsonSerializer.Serialize(sourceFolders);
            Properties.Settings.Default.SourceFolders = json;
            Properties.Settings.Default.Save();
        }



    }
}
