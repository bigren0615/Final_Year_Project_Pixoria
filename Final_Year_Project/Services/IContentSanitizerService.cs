namespace Final_Year_Project.Services
{
    public interface IContentSanitizerService
    {
        /// <summary>
        /// Converts Markdown text to safe HTML (applies Markdown -> HTML -> sanitization).
        /// </summary>
        /// <param name="markdown">The raw Markdown content (nullable).</param>
        /// <returns>Sanitized HTML string.</returns>
        string ConvertAndSanitize(string? markdown);
    }
}
