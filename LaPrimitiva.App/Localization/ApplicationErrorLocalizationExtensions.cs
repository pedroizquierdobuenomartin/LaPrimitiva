using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Localization;
using Microsoft.Extensions.Localization;

namespace LaPrimitiva.App.Localization;

public static class ApplicationErrorLocalizationExtensions
{
    public static string ToLocalizedMessage(
        this ApplicationError error,
        IStringLocalizer<ErrorResource> localizer) =>
        localizer[$"Error.{error.Code}"];
}
