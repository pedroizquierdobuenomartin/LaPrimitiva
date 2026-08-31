using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace LaPrimitiva.App.Localization;

public static class LocalizationConfiguration
{
    public const string SupportedCultureName = "es-ES";

    public static RequestLocalizationOptions CreateRequestLocalizationOptions()
    {
        var supportedCulture = CultureInfo.GetCultureInfo(SupportedCultureName);

        return new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(supportedCulture, supportedCulture),
            SupportedCultures = [supportedCulture],
            SupportedUICultures = [supportedCulture],
            ApplyCurrentCultureToResponseHeaders = true
        };
    }
}
