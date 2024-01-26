using MediaRecon.View;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MvvmWizard.Classes;
using MediaRecon.ViewModel;

namespace MediaRecon
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Services = ConfigureServices();

            WizardSettings.Instance.ViewResolver = viewType => Services.GetService(viewType);

            this.InitializeComponent();
        }

        /// <summary>
        /// Gets the current <see cref="App"/> instance in use
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Configures the services for the application.
        /// </summary>
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<WelcomeView, WelcomeView>();
            services.AddSingleton<SetupView, SetupView>();
            services.AddSingleton<AnalysisView, AnalysisView>();
            services.AddSingleton<ReviewView, ReviewView>();

            services.AddTransient<WelcomeViewModel>();
            services.AddTransient<SetupViewModel>();
            services.AddTransient<AnalysisViewModel>();
            services.AddTransient<ReviewViewModel>();

            return services.BuildServiceProvider();
        }
    }

}
