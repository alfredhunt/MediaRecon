using FluentValidation;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace ApexBytez.MediaRecon.Analysis
{
    public class AnalysisOptions : ObservableObject, IDataErrorInfo
    {
        private ObservableCollection<string> sourceFolders = new ObservableCollection<string>();
        private string destinationFolder = String.Empty;
        private DeleteStrategy deleteStrategy;
        private MoveStrategy moveStrategy;
        private SortingStrategy sortingStrategy = SortingStrategy.YearAndMonth;
        private RunStrategy runStrategy;
        private readonly AnalysisOptionsValidator validator;

        public ObservableCollection<string> SourceFolders { get => sourceFolders; set => SetProperty(ref sourceFolders, value); }
        public string DestinationDirectory { get => destinationFolder; set => SetProperty(ref destinationFolder, value); }
        public DeleteStrategy DeleteStrategy { get => deleteStrategy; set => SetProperty(ref deleteStrategy, value); }
        public MoveStrategy MoveStrategy { get => moveStrategy; set => SetProperty(ref moveStrategy, value); }
        public SortingStrategy SortingStrategy { get => sortingStrategy; set => SetProperty(ref sortingStrategy, value); }
        public RunStrategy RunStrategy { get => runStrategy; set => SetProperty(ref runStrategy, value); }

        public AnalysisOptions()
        {
            validator = new AnalysisOptionsValidator();
            SourceFolders.CollectionChanged += SourceFolders_CollectionChanged;
        }

        private void SourceFolders_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SourceFolders));
        }

        public string this[string columnName]
        {
            get
            {
                var firstOrDefault = validator.Validate(this).Errors.FirstOrDefault(lol => lol.PropertyName == columnName);
                if (firstOrDefault != null)
                    // THIS is stupid copied code from internet. Fix it.
                    return validator != null ? firstOrDefault.ErrorMessage : "";
                return "";
            }
        }

        public string Error
        {
            get
            {
                if (validator != null)
                {
                    var results = validator.Validate(this);
                    if (results != null && results.Errors.Any())
                    {
                        var errors = string.Join(Environment.NewLine, results.Errors.Select(x => x.ErrorMessage).ToArray());
                        Debug.WriteLine(errors);
                        return errors;
                    }
                }
                return string.Empty;
            }
        }
    }

    public class AnalysisOptionsValidator : AbstractValidator<AnalysisOptions>
    {
        public AnalysisOptionsValidator()
        {
            RuleFor(options => options.DestinationDirectory).NotNull();
            RuleFor(options => options.SourceFolders).NotEmpty();
        }
    }
}
