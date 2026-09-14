using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ExtensionHelper;

public static class Extensions
{
    public static bool IsAjax(this HttpRequest request)
    {
        return request.Headers.XRequestedWith == "XMLHttpRequest";
    }

    public static bool IsValid(this ModelStateDictionary ms, string key)
    {
        return ms.GetFieldValidationState(key) == ModelValidationState.Valid;
    }

    // ------------------------------------------------------------------------
    // Date and Time Extension Methods
    // ------------------------------------------------------------------------

    public static DateOnly ToDateOnly(this DateTime dt)
    {
        return DateOnly.FromDateTime(dt);
    }

    public static TimeOnly ToTimeOnly(this DateTime dt)
    {
        return TimeOnly.FromDateTime(dt);
    }

    public static DateOnly Today(this DateOnly date)
    {
        return DateOnly.FromDateTime(DateTime.Today);
    }

    public static TimeOnly Now(this TimeOnly date)
    {
        return TimeOnly.FromDateTime(DateTime.Now);
    }
}
