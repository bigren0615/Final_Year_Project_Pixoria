using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Year_Project.Services
{
    /// <summary>
    /// Configuration for image tagging service
    /// </summary>
    public class ImageTaggingOptions
    {
        public string ServiceUrl { get; set; } = "http://127.0.0.1:5000";  // Use explicit IP to avoid localhost resolution issues
        public int TimeoutSeconds { get; set; } = 60;  // Increased from 30 to 60 for CPU inference
        public bool Enabled { get; set; } = true;
        public float WD14ConfidenceThreshold { get; set; } = 0.5f;
        public float ClipConfidenceThreshold { get; set; } = 0.35f;
        public int MaxTagsReturned { get; set; } = 20;
        public bool UseCache { get; set; } = false;  // Disabled for debugging
    }

    /// <summary>
    /// Response from image tagging service
    /// </summary>
    public class ImageTagResponse
    {
        public bool Success { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("top_tags")]
        public List<string>? Tags { get; set; }
        
        public Dictionary<string, float> Confidence { get; set; } = new();
        
        [System.Text.Json.Serialization.JsonPropertyName("wd14_tags")]
        public Dictionary<string, float> WD14Tags { get; set; } = new();
        
        [System.Text.Json.Serialization.JsonPropertyName("clip_tags")]
        public Dictionary<string, float> ClipTags { get; set; } = new();
        
        public bool FromCache { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Service for analyzing images using WD14 + CLIP-L hybrid approach
    /// </summary>
    public interface IImageTaggingService
    {
        /// <summary>
        /// Analyze a single image file and extract tags
        /// </summary>
        Task<ImageTagResponse> AnalyzeImageAsync(Stream imageStream, string fileName, List<string>? candidateTags = null);

        /// <summary>
        /// Analyze an image from a file path
        /// </summary>
        Task<ImageTagResponse> AnalyzeImageAsync(string filePath);

        /// <summary>
        /// Analyze multiple images at once
        /// </summary>
        Task<List<ImageTagResponse>> AnalyzeImagesAsync(List<Stream> imageStreams, List<string> fileNames);

        /// <summary>
        /// Get service health status
        /// </summary>
        Task<bool> IsHealthyAsync();
    }

    public class ImageTaggingService : IImageTaggingService
    {
        private readonly HttpClient _httpClient;
        private readonly ImageTaggingOptions _options;
        private readonly ILogger<ImageTaggingService> _logger;

        /// <summary>
        /// Validate that a tag is a real tag, not a filename or UUID
        /// </summary>
        private static bool IsValidTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            tag = tag.Trim();

            // Reject if too short (likely corrupted)
            if (tag.Length < 2)
                return false;

            // Reject UUIDs (with optional file extension)
            // Pattern: 8-4-4-4-12 hex digits, optionally followed by .ext
            var uuidPattern = new System.Text.RegularExpressions.Regex(
                @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(\.\w+)?$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (uuidPattern.IsMatch(tag))
                return false;

            // Reject if has too many dots (likely filename)
            if (tag.Split('.').Length > 3)
                return false;

            // Reject if ends with common image/file extensions
            var invalidExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".pdf", ".exe", ".dll", ".zip" };
            var lowerTag = tag.ToLower();
            if (invalidExtensions.Any(ext => lowerTag.EndsWith(ext)))
                return false;

            // Reject if contains control characters
            if (tag.Contains("\n") || tag.Contains("\r") || tag.Contains("\t"))
                return false;

            return true;
        }

        public ImageTaggingService(
            IHttpClientFactory httpClientFactory,
            IOptions<ImageTaggingOptions> options,
            ILogger<ImageTaggingService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _options = options.Value;
            _logger = logger;

            // Configure HTTP client
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            _httpClient.BaseAddress = new Uri(_options.ServiceUrl);
        }

        /// <summary>
        /// Analyze a single image stream
        /// </summary>
        public async Task<ImageTagResponse> AnalyzeImageAsync(Stream imageStream, string fileName, List<string>? candidateTags = null)
        {
            if (!_options.Enabled)
            {
                _logger.LogWarning("Image tagging service is disabled");
                return new ImageTagResponse { Success = false, Error = "Service is disabled" };
            }

            try
            {
                using var content = new MultipartFormDataContent();
                
                // Add file to request
                using var streamContent = new StreamContent(imageStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "file", fileName);
                
                // Add cache parameter
                content.Add(new StringContent(_options.UseCache ? "true" : "false"), "use_cache");
                
                // Add candidate tags if provided
                if (candidateTags != null && candidateTags.Count > 0)
                {
                    var candidateTagsJson = System.Text.Json.JsonSerializer.Serialize(candidateTags);
                    content.Add(new StringContent(candidateTagsJson), "candidate_tags");
                    _logger.LogInformation($"Sending {candidateTags.Count} candidate tags to Python service");
                }

                _logger.LogInformation($"Analyzing image: {fileName}");

                var response = await _httpClient.PostAsync("/tag-image", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Image tagging failed: {response.StatusCode} - {errorContent}");
                    return new ImageTagResponse 
                    { 
                        Success = false, 
                        Error = $"Service returned {response.StatusCode}" 
                    };
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ImageTaggingService] ===== RAW PYTHON JSON RESPONSE =====");
                Console.WriteLine($"[ImageTaggingService] {jsonContent.Substring(0, Math.Min(1200, jsonContent.Length))}");
                Console.WriteLine($"[ImageTaggingService] ===== END JSON =====");
                _logger.LogInformation($"Python response JSON (first 800 chars): {jsonContent.Substring(0, Math.Min(800, jsonContent.Length))}");
                
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false
                };
                
                var result = JsonSerializer.Deserialize<ImageTagResponse>(jsonContent, options);
                Console.WriteLine($"[ImageTaggingService] Deserialization result - Tags count: {result?.Tags?.Count ?? -1}");

                if (result != null)
                {
                    _logger.LogInformation($"✓ Deserialization successful. Success={result.Success}, Tags count={result.Tags?.Count ?? 0}");
                    
                    // VALIDATION: Filter out invalid tags (UUIDs, filenames, etc.)
                    if (result.Tags?.Count > 0)
                    {
                        var originalCount = result.Tags.Count;
                        var validTags = result.Tags
                            .Where(tag => IsValidTag(tag))
                            .ToList();
                        
                        var invalidCount = originalCount - validTags.Count;
                        if (invalidCount > 0)
                        {
                            var invalidTags = result.Tags.Where(tag => !IsValidTag(tag)).ToList();
                            _logger.LogWarning($"⚠ Filtered out {invalidCount} invalid tags: {string.Join(", ", invalidTags)}");
                            Console.WriteLine($"[ImageTaggingService] ⚠ Filtered out {invalidCount} invalid tags: {string.Join(", ", invalidTags)}");
                        }
                        
                        result.Tags = validTags;
                        _logger.LogInformation($"✓ Tags after validation ({validTags.Count}/{originalCount} valid): {string.Join(", ", result.Tags.Take(10))}");
                        Console.WriteLine($"[ImageTaggingService] ✓ Valid tags: {string.Join(", ", validTags.Take(10))}");
                    }
                    else
                    {
                        _logger.LogWarning($"✗ Tags list is empty or null after deserialization!");
                        // Log all properties for debugging
                        _logger.LogInformation($"Response object: Success={result.Success}, FromCache={result.FromCache}, Error={result.Error}");
                        _logger.LogInformation($"WD14Tags count: {result.WD14Tags?.Count ?? 0}, ClipTags count: {result.ClipTags?.Count ?? 0}");
                    }
                    _logger.LogInformation($"Successfully analyzed {fileName}. Generated {result.Tags?.Count ?? 0} tags: {string.Join(", ", result.Tags?.Take(10) ?? new List<string>())}");
                    // Ensure Tags is never null for downstream code
                    if (result.Tags == null)
                        result.Tags = new List<string>();
                }
                else
                {
                    _logger.LogError($"✗ Deserialization returned null for response: {jsonContent.Substring(0, Math.Min(200, jsonContent.Length))}");
                }
                
                return result ?? new ImageTagResponse { Success = false, Error = "Invalid response format", Tags = new List<string>() };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"HTTP error while analyzing image {fileName}");
                return new ImageTagResponse 
                { 
                    Success = false, 
                    Error = $"Service communication failed: {ex.Message}",
                    Tags = new List<string>()
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, $"Timeout while analyzing image {fileName}");
                return new ImageTagResponse 
                { 
                    Success = false, 
                    Error = "Request timed out",
                    Tags = new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error while analyzing image {fileName}");
                return new ImageTagResponse 
                { 
                    Success = false, 
                    Error = ex.Message,
                    Tags = new List<string>()
                };
            }
        }

        /// <summary>
        /// Analyze image from file path
        /// </summary>
        public async Task<ImageTagResponse> AnalyzeImageAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new ImageTagResponse 
                    { 
                        Success = false, 
                        Error = $"File not found: {filePath}",
                        Tags = new List<string>()
                    };
                }

                using var fileStream = File.OpenRead(filePath);
                return await AnalyzeImageAsync(fileStream, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading file {filePath}");
                return new ImageTagResponse 
                { 
                    Success = false, 
                    Error = ex.Message 
                };
            }
        }

        /// <summary>
        /// Analyze multiple images
        /// </summary>
        public async Task<List<ImageTagResponse>> AnalyzeImagesAsync(List<Stream> imageStreams, List<string> fileNames)
        {
            if (imageStreams.Count != fileNames.Count)
            {
                throw new ArgumentException("Stream and filename counts must match");
            }

            var results = new List<ImageTagResponse>();
            
            foreach (var (stream, fileName) in imageStreams.Zip(fileNames))
            {
                var result = await AnalyzeImageAsync(stream, fileName);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Check if image tagging service is healthy
        /// </summary>
        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Image tagging service health check failed");
                return false;
            }
        }
    }

    /// <summary>
    /// Mock implementation for when service is unavailable
    /// </summary>
    public class MockImageTaggingService : IImageTaggingService
    {
        private readonly ILogger<MockImageTaggingService> _logger;

        public MockImageTaggingService(ILogger<MockImageTaggingService> logger)
        {
            _logger = logger;
        }

        public async Task<ImageTagResponse> AnalyzeImageAsync(Stream imageStream, string fileName, List<string>? candidateTags = null)
        {
            _logger.LogWarning("Using mock image tagging service");
            return await Task.FromResult(new ImageTagResponse 
            { 
                Success = false, 
                Error = "Image tagging service unavailable" 
            });
        }

        public async Task<ImageTagResponse> AnalyzeImageAsync(string filePath)
        {
            return await AnalyzeImageAsync(new MemoryStream(), Path.GetFileName(filePath));
        }

        public async Task<List<ImageTagResponse>> AnalyzeImagesAsync(List<Stream> imageStreams, List<string> fileNames)
        {
            return await Task.FromResult(imageStreams.Select(s => new ImageTagResponse 
            { 
                Success = false, 
                Error = "Image tagging service unavailable" 
            }).ToList());
        }

        public async Task<bool> IsHealthyAsync()
        {
            return await Task.FromResult(false);
        }
    }
}
