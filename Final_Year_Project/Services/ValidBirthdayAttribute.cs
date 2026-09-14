using System;
using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Services
{
    public class ValidBirthdayAttribute : ValidationAttribute
    {
        private readonly int _minYear;
        private readonly int _minAge;
        private readonly int _maxAge;

        /// <summary>
        /// Validates a birthday within a reasonable age range.
        /// </summary>
        /// <param name="minYear">Minimum acceptable birth year (default: 1900)</param>
        /// <param name="minAge">Minimum allowed age in years (default: 1)</param>
        /// <param name="maxAge">Maximum allowed age in years (default: 120)</param>
        public ValidBirthdayAttribute(int minYear = 1900, int minAge = 1, int maxAge = 120)
        {
            _minYear = minYear;
            _minAge = minAge;
            _maxAge = maxAge;
            ErrorMessage = "Please enter a valid birthday date.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
                return ValidationResult.Success;

            if (value is not DateOnly dob)
                return new ValidationResult("Invalid date format.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (dob.Year < _minYear)
                return new ValidationResult($"Birthday cannot be before year {_minYear}.");

            if (dob > today)
                return new ValidationResult("Birthday cannot be in the future.");

            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age))
                age--;

            if (age < _minAge)
                return new ValidationResult($"You must be at least {_minAge} year{(_minAge > 1 ? "s" : "")} old.");

            if (age > _maxAge)
                return new ValidationResult($"Age cannot be greater than {_maxAge} years.");

            return ValidationResult.Success;
        }
    }
}