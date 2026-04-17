using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using WearPartsControl.ApplicationServices.PlcService;

namespace WearPartsControl.ViewModels;

public sealed class ReplacePartViewModel : ObservableObject
{
    private readonly IPlcStartupConnectionService _plcStartupConnectionService;
    private bool _isInitialized;
    private string _plcConnectionStatusText = "未初始化";
    private Brush _plcConnectionStatusBackground = Brushes.Gray;

    public ReplacePartViewModel(IPlcStartupConnectionService plcStartupConnectionService)
    {
        _plcStartupConnectionService = plcStartupConnectionService;
    }

    public string PlcConnectionStatusText
    {
        get => _plcConnectionStatusText;
        private set => SetProperty(ref _plcConnectionStatusText, value);
    }

    public Brush PlcConnectionStatusBackground
    {
        get => _plcConnectionStatusBackground;
        private set => SetProperty(ref _plcConnectionStatusBackground, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        PlcConnectionStatusText = "连接中";
        PlcConnectionStatusBackground = Brushes.Goldenrod;

        var result = await _plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(true);
        Apply(result);
    }

    private void Apply(PlcStartupConnectionResult result)
    {
        PlcConnectionStatusText = result.Message;
        PlcConnectionStatusBackground = result.Status switch
        {
            PlcStartupConnectionStatus.Connected => Brushes.ForestGreen,
            PlcStartupConnectionStatus.NotConfigured => Brushes.DimGray,
            _ => Brushes.Firebrick
        };
    }
}