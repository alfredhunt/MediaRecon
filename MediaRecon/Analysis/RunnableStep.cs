using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ApexBytez.MediaRecon.Analysis
{
    internal abstract class RunnableStep : ObservableObject
    {
        private CancellationTokenSource cancellationTokenSource;
        protected CancellationToken cancellationToken;
        private bool canCancel;
        private bool showResultLabel;
        private string resultsLabel;
        private double progressBarValue;
        private double progressBarMaximum;
        private TimeSpan analysisTime;
        private bool analysisInProgress;
        private RelayCommand cancelCommand;

        public double ProgressBarValue { get => progressBarValue; set => SetProperty(ref progressBarValue, value); }
        public double ProgressBarMaximum { get => progressBarMaximum; set => SetProperty(ref progressBarMaximum, value); }
        public TimeSpan RunTime { get => analysisTime; set => SetProperty(ref analysisTime, value); }
        public bool Running { get => analysisInProgress; set => SetProperty(ref analysisInProgress, value); }
        public bool CanCancel { get => canCancel; set => SetProperty(ref canCancel, value); }
        public bool ShowResultsLabel { get => showResultLabel; set => SetProperty(ref showResultLabel, value); }
        public string ResultsLabel { get => resultsLabel; set => SetProperty(ref resultsLabel, value); }
        public RelayCommand CancelCommand => cancelCommand ??= new RelayCommand(Cancel, CanExecuteCancel);
        public void Cancel()
        {
            cancellationTokenSource.Cancel();
        }
        public bool CanExecuteCancel()
        {
            return CanCancel = Running &&
                cancellationTokenSource != null &&
                !cancellationTokenSource.IsCancellationRequested;
        }
        protected abstract Task RunAsyncStep();
        public async Task RunAsync()
        {
            await Run(RunAsyncStep);
        }
        protected async Task Run(Func<Task> asyncTask)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            ProgressBarValue = 0;
            ProgressBarMaximum = Properties.Settings.Default.ProgressBarMaximum;
            RunTime = TimeSpan.Zero;
            Running = true;
            

            Application.Current.Dispatcher.Invoke(() =>
            {
                CancelCommand.NotifyCanExecuteChanged();
            });

            TimeSpan elapsedTime = TimeSpan.FromSeconds(0);
            var observableTimer = Observable.Interval(TimeSpan.FromMilliseconds(42))
                .TimeInterval()
                .Subscribe(x =>
                {
                    elapsedTime = elapsedTime.Add(x.Interval);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        RunTime = elapsedTime;
                    });
                });

            try
            {
                await asyncTask();
                ResultsLabel = "Done!";
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
                ResultsLabel = "Cancelled";
            }
            finally
            {
                observableTimer.Dispose();
            }

            // TODO: probably need to work on the statefulness of this processing and the UI elements
            //  visibility.
            Running = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                CancelCommand.NotifyCanExecuteChanged();
            });
            Debug.Assert(!CanCancel);
            CanCancel = false;
            ShowResultsLabel = true;
        }
    }
}
