using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace LaPrimitiva.App.Localization;

public sealed class RequiredStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ResourceManagerStringLocalizerFactory _inner;

    public RequiredStringLocalizerFactory(
        IOptions<LocalizationOptions> localizationOptions,
        ILoggerFactory loggerFactory)
    {
        _inner = new ResourceManagerStringLocalizerFactory(localizationOptions, loggerFactory);
    }

    public IStringLocalizer Create(Type resourceSource) =>
        new RequiredStringLocalizer(_inner.Create(resourceSource));

    public IStringLocalizer Create(string baseName, string location) =>
        new RequiredStringLocalizer(_inner.Create(baseName, location));
}
