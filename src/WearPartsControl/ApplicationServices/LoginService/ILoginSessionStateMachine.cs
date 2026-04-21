using WearPartsControl.ApplicationServices.AppSettings;
using AppSettingsModel = WearPartsControl.ApplicationServices.AppSettings.AppSettings;

namespace WearPartsControl.ApplicationServices.LoginService;

public interface ILoginSessionStateMachine
{
    event EventHandler? StateChanged;

    LoginSessionState Current { get; }

    void UpdateSettings(AppSettingsModel settings);
}