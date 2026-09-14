using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Final_Year_Project.Services
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class AllowedValuesAttribute : ValidationAttribute
    {
        private readonly HashSet<string> _allowedValues = new();

        public AllowedValuesAttribute(params object[] allowed)
        {
            foreach (var item in allowed)
            {
                if (item is Type enumType && enumType.IsEnum)
                {
                    foreach (var name in Enum.GetNames(enumType))
                    {
                        _allowedValues.Add(name.ToLowerInvariant());
                    }
                }
                else
                {
                    _allowedValues.Add(item.ToString()!.ToLowerInvariant());
                }
            }
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
                return ValidationResult.Success;

            var strValue = value.ToString()?.ToLowerInvariant();
            if (!_allowedValues.Contains(strValue ?? string.Empty))
            {
                var allowedList = string.Join(", ", _allowedValues);
                return new ValidationResult($"Invalid {validationContext.DisplayName}. Allowed values: {allowedList}");
            }

            return ValidationResult.Success;
        }
    }
}
