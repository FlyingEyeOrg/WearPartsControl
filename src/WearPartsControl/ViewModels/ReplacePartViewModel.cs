using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Windows.Media;
using WearPartsControl.ApplicationServices.PlcService;

namespace WearPartsControl.ViewModels;

public sealed class ReplacePartViewModel : ObservableObject
{
    private readonly IPlcConnectionStatusService _plcConnectionStatusService;
    private string _plcConnectionStatusText = "未初始化";
    private Brush _plcConnectionStatusBackground = Brushes.Gray;

    public ReplacePartViewModel(IPlcConnectionStatusService plcConnectionStatusService)
    {
        _plcConnectionStatusService = plcConnectionStatusService;
        _plcConnectionStatusService.PropertyChanged += OnPlcConnectionStatusChanged;
        Apply(_plcConnectionStatusService.Current);
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

    private void OnPlcConnectionStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IPlcConnectionStatusService.Current))
        {
            return;
        }

        Apply(_plcConnectionStatusService.Current);
    }

    private void Apply(PlcStartupConnectionResult result)
    {
        PlcConnectionStatusText = result.Message;
        PlcConnectionStatusBackground = result.Status switch
        {
            PlcStartupConnectionStatus.Connecting => Brushes.Goldenrod,
            PlcStartupConnectionStatus.Connected => Brushes.ForestGreen,
            PlcStartupConnectionStatus.NotConfigured => Brushes.DimGray,
            PlcStartupConnectionStatus.Uninitialized => Brushes.Gray,
            _ => Brushes.Firebrick
        };
    }
}