using System;
using System.Collections.Generic;
using Xamarin.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Examples.XamarinFormsApp
{
    public partial class MainPage : ContentPage
    {
        private const int ApplicationId = 2000;
        private static ActivitySource Source = new ActivitySource("MyMobileTest");
        private static IConfiguration configuration;
        static IDictionary<string, string> DefaultConfigurationStrings { get; } =
             new Dictionary<string, string>()
             {
                 ["AllowedHosts"] = "*",
                 ["diagnostics.enabled"] = "false",
                 ["env"] = "XAMARIN-LogTest-Environment",
                 ["diagnostics.sources"] = "*",
                 ["diagnostics.exporter.opentelemetry.url"] = "https://sl-sy-otel2.creativesoftware.com:4318",
                 ["diagnostics.servicename"] = "MobileSimulator",
             };

        public MainPage()
        {
            InitializeComponent();
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    await SendLogs();
                }
                catch (Exception e)
                {
                }
            });
        }

        private async Task SendLogs()
        {
            DefaultConfigurationStrings["diagnostics.exporter.opentelemetry.url"] = urlPicker.SelectedItem.ToString();

            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(DefaultConfigurationStrings)
                .Build();

            // This is needed because console applications does not have an hosting environment per default.
            // In a hosted application(.net core web app, for instance, telemetry is started automatically on the hosted process)
            Start(ApplicationId, "MobileSimulator", configuration);

            string rootId;
            var openTelemetryEndpoint = configuration.GetValue<string>("diagnostics.exporter.opentelemetry.url");
            var telemetryUri = new Uri(openTelemetryEndpoint, UriKind.Absolute);

            // Start a user-defined activity that will become the root activity for this sample
            using (var activity = Source.StartActivity("RestClientDemo"))
            {

                // Execute an http call to RestServer.  
                // When http client is invoked, HttpClientInstrumentation will create a new activity with our custom activity as a parent.
                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri($"http://{telemetryUri.Host}:5001/");
                var logger = GetLogger("TracingAndLogging");

                var message = $"Sending request to the weather service at {httpClient.BaseAddress}";
                Console.WriteLine(message);
                logger.Log(LogLevel.Information, message);

                var result = await httpClient.GetAsync("/fromyr?lat=69.68333&lon=18.91667");

                if (activity != null)
                {
                    rootId = activity.RootId;
                    message = $"Trace can be viewed at https://{telemetryUri.Host}/jaeger/trace/{rootId}";
                    Console.WriteLine(message);
                    logger.Log(LogLevel.Information, message);
                }
            }
        }

        private IServiceProvider Provider;

        private void Start(int applicationId, string serviceName, IConfiguration configuration)
        {
            SelfLogBase.Listener = new Action<SelfLogEventArgs>(Log);

            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(configuration)
                .AddLogging(
                    builder =>
                    {
                        builder
                            // TODO: How to make sure that we get this correctly from configuration object?
                            // TODO: check if config section exists
                            .AddConfiguration(configuration.GetSection("Logging"))
                            .AddOpenTelemetry(
                                options =>
                                {
                                    var config = Provider.GetRequiredService<IConfiguration>();
                                    options.AddOtlpExporter(
                                        o =>
                                        {
                                            var openTelemetryEndpoint =
                                                config.GetValue<string>("diagnostics.exporter.opentelemetry.url");
                                            o.Endpoint = new Uri(openTelemetryEndpoint, UriKind.Absolute);
                                        });
                                    options.IncludeFormattedMessage = true;
                                    options.IncludeScopes = true;

                                    var tags = new Dictionary<string, object>
                                    {
                                        { "env", config.GetValue("env", "NOT_SET") },
                                        { "aid", applicationId },
                                    };

                                    var resourceBuilder = ResourceBuilder.CreateDefault()
                                        .AddAttributes(tags)
                                        .AddEnvironmentVariableDetector()
                                        .AddTelemetrySdk()
                                        .AddService(serviceName);
                                    options.SetResourceBuilder(resourceBuilder);
                                });
                    });


            Provider = serviceCollection.BuildServiceProvider();
        }

        private ILogger GetLogger(string categoryName)
        {
            var loggerFactory = Provider.GetService<ILoggerFactory>();
            return loggerFactory.CreateLogger(categoryName);
        }

        private static void Log(SelfLogEventArgs logEvent)
        {
            var log = $"Source: {logEvent.EventSource}\n EventId: {logEvent.EventId}\n Level: {logEvent.Level}\n {logEvent.Message}\n\n";
            Console.WriteLine(log);
        }
    }
}
