using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.Localization;

public interface ILocalizationService
{
    string this[string name] { get; }

    ValueTask InitializeAsync(CancellationToken cancellationToken = default);

    ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default);

    CultureInfo CurrentCulture { get; }
}
