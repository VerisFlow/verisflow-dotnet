using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TraceLogic.Core;
using VerisFlow.Mcp.Client;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Extensions;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Interaction logic for MainWindow.xaml, serving as the main entry window for managing MCP client lifecycle, tool execution, and authentication.
/// </summary>
public partial class MainWindow : Window, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Service instance managing communication with the MCP relay server.
    /// </summary>
    private McpClientService? _clientService;

    /// <summary>
    /// MSAL client application used for interactive and silent token acquisition.
    /// </summary>
    private readonly IPublicClientApplication _msalClient;

    /// <summary>
    /// OAuth scopes requested during authentication.
    /// </summary>
    private readonly string[] _scopes;

    /// <summary>
    /// Target relay URL for the development environment.
    /// </summary>
    private readonly string _devRelayUrl;

    /// <summary>
    /// Target relay URL for the production environment.
    /// </summary>
    private readonly string _prodRelayUrl;

    /// <summary>
    /// Dependency injection service provider instance.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Indicates whether the MCP client is currently connected.
    /// </summary>
    private bool _isConnected;

    /// <summary>
    /// Indicates whether the active target environment is production.
    /// </summary>
    private bool _isProd;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class, configuring application services, MSAL authentication, and environment settings.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(configure =>
        {
            configure.AddDebug().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            configure.AddProvider(new WpfLoggerProvider(message =>
            {
                Dispatcher.Invoke(() =>
                {
                    TxtLog.AppendText($"{DateTime.Now:HH:mm:ss} {message}\n");
                    TxtLog.ScrollToEnd();
                });
            }));
        });

        services.AddVenusAutomation(config);
        services.AddTraceLogic();

        services.AddSingleton<IMcpToolRegistry, DefaultMcpToolRegistry>();
        services.AddSingleton<IMcpToolDispatcher, McpToolDispatcher>();

        // Register all IMcpToolHandler implementations via assembly scanning extension
        services.AddMcpToolHandlers();

        _serviceProvider = services.BuildServiceProvider();

        _devRelayUrl = config["McpConfig:DevRelayUrl"] ?? "https://localhost:7216/mcphub";
        _prodRelayUrl = config["McpConfig:ProdRelayUrl"]
            ?? throw new InvalidOperationException("Configuration 'McpConfig:ProdRelayUrl' is required. Please set a valid production relay URL.");

        string defaultEnv = config["McpConfig:Environment"] ?? "Dev";
        string clientId = config["McpConfig:ClientId"] ?? throw new InvalidOperationException("ClientId is missing");
        string tenantId = config["McpConfig:TenantId"] ?? throw new InvalidOperationException("TenantId is missing");

        _scopes = new[] { $"api://{clientId}/access_as_user" };

        _msalClient = PublicClientApplicationBuilder.Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .WithDefaultRedirectUri()
            .Build();

        _isProd = defaultEnv.Equals("Prod", StringComparison.OrdinalIgnoreCase);

        DisplayVersion();
        ApplyToggleVisual(animated: false);

        InitializeMcpClient();
    }

    /// <summary>
    /// Reads and displays the current application version from assembly attributes.
    /// </summary>
    private void DisplayVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var versionAttribute = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;

        if (versionAttribute != null)
        {
            string fullVersion = versionAttribute.InformationalVersion;
            int plusIndex = fullVersion.IndexOf('+');
            TxtVersion.Text = $"v{(plusIndex > 0 ? fullVersion.Substring(0, plusIndex) : fullVersion)}";
        }
    }

    /// <summary>
    /// Handles the environment toggle switch click event to alternate between Dev and Prod configurations.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The mouse button event arguments.</param>
    private void ToggleSwitch_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isConnected) return;

        _isProd = !_isProd;
        ApplyToggleVisual(animated: true);
        InitializeMcpClient();
    }

    /// <summary>
    /// Updates the visual representation of the environment toggle switch.
    /// </summary>
    /// <param name="animated">Determines whether to apply transition animations.</param>
    private void ApplyToggleVisual(bool animated)
    {
        double targetX = _isProd ? 20 : 0;
        Color trackColor = _isProd
            ? Color.FromRgb(0, 122, 204)
            : Color.FromRgb(60, 60, 60);

        TxtDev.Foreground = _isProd ? new SolidColorBrush(Color.FromRgb(136, 136, 136)) : Brushes.White;
        TxtProd.Foreground = _isProd ? Brushes.White : new SolidColorBrush(Color.FromRgb(136, 136, 136));

        if (animated)
        {
            var thumbAnim = new DoubleAnimation(targetX, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ThumbTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, thumbAnim);

            var trackAnim = new ColorAnimation(trackColor, TimeSpan.FromMilliseconds(180));
            ToggleTrack.Background = new SolidColorBrush(
                _isProd ? Color.FromRgb(60, 60, 60) : Color.FromRgb(0, 122, 204));
            ToggleTrack.Background.BeginAnimation(SolidColorBrush.ColorProperty, trackAnim);
        }
        else
        {
            ThumbTranslate.X = targetX;
            ToggleTrack.Background = new SolidColorBrush(trackColor);
        }
    }

    /// <summary>
    /// Instantiates and configures the <see cref="McpClientService"/> based on the active target URL and registered services.
    /// </summary>
    private void InitializeMcpClient()
    {
        string selectedUrl = _isProd ? _prodRelayUrl : _devRelayUrl;

        var toolDispatcher = _serviceProvider.GetRequiredService<IMcpToolDispatcher>();
        var logger = _serviceProvider.GetRequiredService<ILogger<McpClientService>>();

        _clientService = new McpClientService(selectedUrl, toolDispatcher, logger, accessTokenProvider: GetAccessTokenAsync);
    }

    /// <summary>
    /// Acquires an OAuth access token, attempting silent authentication first before falling back to interactive prompt.
    /// </summary>
    /// <returns>A task returning the access token string if successful; otherwise, <c>null</c>.</returns>
    private async Task<string?> GetAccessTokenAsync()
    {
        var accounts = await _msalClient.GetAccountsAsync();

        try
        {
            // Attempt to acquire access token silently from cache
            var result = await _msalClient.AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                .ExecuteAsync();
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            try
            {
                // Fall back to interactive UI authentication if silent acquisition fails
                var result = await _msalClient.AcquireTokenInteractive(_scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync();
                return result.AccessToken;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    TxtLog.AppendText($"{DateTime.Now:HH:mm:ss} [Auth Error] {ex.Message}\n");
                    TxtLog.ScrollToEnd();
                });
                return null;
            }
        }
    }

    /// <summary>
    /// Handles connection state toggling when clicking the Connect/Disconnect button.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The routed event arguments.</param>
    private async void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        BtnToggle.IsEnabled = false;
        ToggleTrack.IsEnabled = false;

        if (!_isConnected)
        {
            BtnToggle.Content = "Connecting...";
            TxtStatus.Text = "Status: Connecting...";
            TxtStatus.Foreground = Brushes.Orange;

            var pulse = new DoubleAnimation(1.0, 0.4, TimeSpan.FromMilliseconds(600))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            TxtStatus.BeginAnimation(OpacityProperty, pulse);

            try
            {
                await _clientService!.StartAsync();

                TxtStatus.BeginAnimation(OpacityProperty, null);
                TxtStatus.Opacity = 1;

                BtnToggle.Content = "Disconnect";
                TxtStatus.Text = "Status: Online";
                TxtStatus.Foreground = Brushes.LightGreen;
                _isConnected = true;
            }
            catch (Exception ex)
            {
                TxtStatus.BeginAnimation(OpacityProperty, null);
                TxtStatus.Opacity = 1;

                TxtLog.AppendText($"{DateTime.Now:HH:mm:ss} [Connection Error] {ex.Message}\n");
                BtnToggle.Content = "Connect to Relay";
                TxtStatus.Text = "Status: Offline";
                TxtStatus.Foreground = Brushes.LightGray;
                ToggleTrack.IsEnabled = true;
            }
        }
        else
        {
            BtnToggle.Content = "Disconnecting...";

            await _clientService!.StopAsync();

            BtnToggle.Content = "Connect to Relay";
            TxtStatus.Text = "Status: Offline";
            TxtStatus.Foreground = Brushes.LightGray;
            _isConnected = false;
            ToggleTrack.IsEnabled = true;
        }

        BtnToggle.IsEnabled = true;
    }

    /// <summary>
    /// Overrides window closing logic to release resources asynchronously before closing.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected override async void OnClosed(EventArgs e)
    {
        if (_clientService != null)
        {
            await _clientService.DisposeAsync();
        }
        base.OnClosed(e);
    }

    /// <summary>
    /// Suppresses garbage collection for managed resource cleanup.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes asynchronous resources, including the active MCP client connection.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_clientService != null)
        {
            await _clientService.DisposeAsync();
            _clientService = null;
        }

        GC.SuppressFinalize(this);
    }
}