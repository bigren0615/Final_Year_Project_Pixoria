using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Final_Year_Project.Services
{
    public class EnumService
    {
        public string GetEnumDisplayName(Enum enumValue)
        {
            var field = enumValue.GetType().GetField(enumValue.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? enumValue.ToString();
        }

        public List<SelectListItem> GetEnumSelectListItems<TEnum>() where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum))
                       .Cast<TEnum>()
                       .Select(e => new SelectListItem
                       {
                           Text = GetEnumDisplayName(e),
                           Value = e.ToString()
                       })
                       .ToList();
        }

        public TEnum ToEnum<TEnum>(string value, TEnum defaultValue = default!) where TEnum : struct, Enum
        {
            if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
                return result;

            return defaultValue;
        }

        public string ToStringValue(Enum enumValue)
        {
            return enumValue.ToString();
        }


    }
}
