using Microsoft.Extensions.Localization;

namespace LaPrimitiva.App.Localization;

public sealed class MissingLocalizationResourceException(string resourceName)
    : InvalidOperationException($"Missing localization resource: {resourceName}")
{
    public string ResourceName { get; } = resourceName;
}

public sealed class RequiredStringLocalizer(IStringLocalizer inner) : IStringLocalizer
{
    public LocalizedString this[string name] => Require(inner[name]);
    public LocalizedString this[string name, params object[] arguments] => Require(inner[name, arguments]);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        inner.GetAllStrings(includeParentCultures);

    private static LocalizedString Require(LocalizedString value)
    {
        if (value.ResourceNotFound)
        {
            throw new MissingLocalizationResourceException(value.Name);
        }

        return value;
    }
}
