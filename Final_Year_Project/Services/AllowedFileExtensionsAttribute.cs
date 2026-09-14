using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Services
{
    [AttributeUsage(AttributeTargets.Property)]
    public class AllowedFileExtensionsAttribute : ValidationAttribute
    {
        private readonly string[] _extensions;
        private readonly long _maxFileSizeBytes;

        public AllowedFileExtensionsAttribute(string[] extensions, int maxFileSizeMB)
        {
            _extensions = extensions;
            _maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not IFormFile file)
                return ValidationResult.Success;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!_extensions.Contains(extension))
                return new ValidationResult($"Invalid file type. Only {string.Join(", ", _extensions)} are allowed.");

            if (file.Length > _maxFileSizeBytes)
                return new ValidationResult($"File size cannot exceed {_maxFileSizeBytes / (1024 * 1024)} MB.");

            return ValidationResult.Success;
        }
    }
}
