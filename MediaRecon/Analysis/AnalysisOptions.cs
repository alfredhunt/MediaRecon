using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace ApexBytez.MediaRecon.Analysis
{
    public class AnalysisOptions : ObservableObject
    {
        private ObservableCollection<string> sourceFolders = new ObservableCollection<string>();
        private string destinationFolder = String.Empty;
        private DeleteStrategy deleteStrategy;
        private MoveStrategy moveStrategy;
        private SortingStrategy sortingStrategy = SortingStrategy.YearAndMonth;
        private RunStrategy runStrategy;
        public ObservableCollection<string> SourceFolders { get => sourceFolders; set => SetProperty(ref sourceFolders, value); }
        public string DestinationDirectory { get => destinationFolder; set => SetProperty(ref destinationFolder, value); }
        public DeleteStrategy DeleteStrategy { get => deleteStrategy; set => SetProperty(ref deleteStrategy, value); }
        public MoveStrategy MoveStrategy { get => moveStrategy; set => SetProperty(ref moveStrategy, value); }
        public SortingStrategy SortingStrategy { get => sortingStrategy; set => SetProperty(ref sortingStrategy, value); }
        public RunStrategy RunStrategy { get => runStrategy; set => SetProperty(ref runStrategy, value); }
    }
}
