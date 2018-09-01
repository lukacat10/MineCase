using Microsoft.Extensions.Configuration;
using Orleans;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using Microsoft.Extensions.Logging;
using MineCase.Gateway.Network;
using System.Reflection;
using System.Threading.Tasks;
using Orleans.Runtime;
using Polly;

namespace MineCase.Gateway
{
    partial class Program
    {
        public static IConfiguration Configuration { get; private set; }

        private static IClusterClient _clusterClient;
        private static readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);
        private static ILogger _logger;

        private const int _initializeAttemptsBeforeFailing = 5;
        private static int _attempt = 0;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += (s, e) => _exitEvent.Set();
            Configuration = LoadConfiguration();
            Startup();
            _exitEvent.WaitOne();
            _clusterClient?.Dispose();
        }

        private static void ConfigureApplicationParts(IClientBuilder builder)
        {
            builder.ConfigureApplicationParts(mgr =>
            {
            });
        }

        private static async void Startup()
        {
            _clusterClient = new ClientBuilder()
                .UseLocalhostClustering()
                .ConfigureServices(ConfigureServices)
                .ConfigureLogging(ConfigureLogging)
                .ConfigureApplicationParts(parts =>
                {
                    foreach (var assembly in SelectAssemblies())
                        parts.AddApplicationPart(assembly);
                }).Build();

            var serviceProvider = _clusterClient.ServiceProvider;
            _logger = _clusterClient.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

            await Connect();

            var connectionRouter = _clusterClient.ServiceProvider.GetRequiredService<ConnectionRouter>();
            await connectionRouter.Startup(default(CancellationToken));
        }

        private static async Task Connect()
        {
            _logger.LogInformation("Connecting to cluster...");
            await _clusterClient.Connect(RetryFilter);
            _logger.LogInformation("Connected to cluster.");
        }

        private static async Task<bool> RetryFilter(Exception exception)
        {
            if (exception.GetType() != typeof(SiloUnavailableException))
            {
                _logger.LogError($"Cluster client failed to connect to cluster with unexpected error.  Exception: {exception}");
                return false;
            }
            _attempt++;
            _logger.LogWarning($"Cluster client attempt {_attempt} of {_initializeAttemptsBeforeFailing} failed to connect to cluster.  Exception: {exception}");
            if (_attempt > _initializeAttemptsBeforeFailing)
            {
                return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(4));
            return true;
        }
    }
}