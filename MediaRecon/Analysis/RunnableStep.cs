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
        private int progressBarValue;
        private int progressBarMaximum;
        private double progressPercentage;
        private TimeSpan runTime;
        private bool running;
        private RelayCommand cancelCommand;
        protected TimeSpan elapsedTime;
        protected IProgress<TimeSpan> iprogress;
        protected DateTime startTime;

        public int ProgressBarValue { get => progressBarValue; set => SetProperty(ref progressBarValue, value); }
        public int ProgressBarMaximum { get => progressBarMaximum; set => SetProperty(ref progressBarMaximum, value); }
        public double ProgressPercentage { get => progressPercentage; set => SetProperty(ref progressPercentage, value); }
        public TimeSpan RunTime { get => runTime; set => SetProperty(ref runTime, value); }
        public bool Running { get => running; set => SetProperty(ref running, value); }
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
            await RunAsync(RunAsyncStep);
        }
        protected Task RunAsync(Func<Task> asyncTask)
        {
            return Task.Run(async () =>
            {
                cancellationTokenSource = new CancellationTokenSource();
                cancellationToken = cancellationTokenSource.Token;
                ProgressBarValue = 0;
                ProgressBarMaximum = Properties.Settings.Default.ProgressBarMaximum;
                RunTime = TimeSpan.Zero;
                Running = true;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CancelCommand.NotifyCanExecuteChanged();
                });

                startTime = DateTime.Now;
                var previous = startTime;
                var updateDisposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(50))
                    .TimeInterval()
                    .Subscribe(async x =>
                    {
                        var now = DateTime.Now;
                        var delta = (now - previous).TotalMilliseconds;
                        previous = now;
                        //Debug.WriteLine("UpdateProgress : {0}", delta);
                        
                        await UpdateProgress();
                    });

                try
                {
                    // TODO: pass the cancellation token in so it's clear that it's being used.
                    await asyncTask();
                    ResultsLabel = "Done!";
                }
                catch (OperationCanceledException ex)
                {
                    Debug.WriteLine(ex.Message);
                    ResultsLabel = "Cancelled";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    ResultsLabel = "Error";
                }
                finally
                {
                    //observableTimer.Dispose();
                }

                updateDisposable.Dispose();
                await UpdateProgress();

                // TODO: probably need to work on the statefulness of this processing and the UI elements
                //  visibility.
                Running = false;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CancelCommand.NotifyCanExecuteChanged();
                });
                Debug.Assert(!CanCancel);
                CanCancel = false;
                ShowResultsLabel = true;
            });

            
        }

        protected abstract Task UpdateProgress();
    }
}
