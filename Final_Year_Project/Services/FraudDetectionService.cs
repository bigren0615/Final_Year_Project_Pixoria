using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Year_Project.Services
{
    /// <summary>
    /// Configuration for fraud detection service
    /// </summary>
    public class FraudDetectionOptions
    {
        public string ServiceUrl { get; set; } = "http://127.0.0.1:5001";
        public int TimeoutSeconds { get; set; } = 120;
        public bool Enabled { get; set; } = true;
        public float AiDetectionThreshold { get; set; } = 0.5f;
        public float PlagiarismThreshold { get; set; } = 0.85f;
    }

    /// <summary>
    /// Response from fraud detection service
    /// </summary>
    public class FraudDetectionResponse
    {
        public bool Success { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("image_id")]
        public string? ImageId { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("fraud_risk")]
        public string FraudRisk { get; set; } = "low";
        
        [System.Text.Json.Serialization.JsonPropertyName("fraud_reasons")]
        public List<string>? FraudReasons { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("ai_detection")]
        public AiDetectionResult? AiDetection { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("plagiarism_detection")]
        public PlagiarismDetectionResult? PlagiarismDetection { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("from_cache")]
        public bool FromCache { get; set; }
        
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AiDetectionResult
    {
        public bool Success { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("is_ai_generated")]
        public bool IsAiGenerated { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("ai_probability")]
        public float AiProbability { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("human_probability")]
        public float HumanProbability { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("confidence")]
        public float Confidence { get; set; }
        
        public string? Error { get; set; }
    }

    public class PlagiarismDetectionResult
    {
        public bool Success { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("is_plagiarized")]
        public bool IsPlagiarized { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("similarity_threshold")]
        public float SimilarityThreshold { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("matches")]
        public List<PlagiarismMatch>? Matches { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("total_compared")]
        public int TotalCompared { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("image_id")]
        public string? ImageId { get; set; }
        
        public string? Error { get; set; }
    }

    public class PlagiarismMatch
    {
        [System.Text.Json.Serialization.JsonPropertyName("image_id")]
        public string? ImageId { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("similarity")]
        public float Similarity { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("is_match")]
        public bool IsMatch { get; set; }
    }

    public class ImageComparisonResult
    {
        public bool Success { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("similarity")]
        public float Similarity { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("is_similar")]
        public bool IsSimilar { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("threshold")]
        public float Threshold { get; set; }
        
        public string? Error { get; set; }
    }

    public class ServiceHealthStatus
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "unknown";
        
        [System.Text.Json.Serialization.JsonPropertyName("clip_loaded")]
        public bool ClipLoaded { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("device")]
        public string Device { get; set; } = "cpu";
        
        [System.Text.Json.Serialization.JsonPropertyName("stored_embeddings")]
        public int StoredEmbeddings { get; set; }
    }

    /// <summary>
    /// Interface for fraud detection service
    /// </summary>
    public interface IFraudDetectionService
    {
        Task<FraudDetectionResponse> DetectFraudAsync(Stream imageStream, string fileName, string imageId, bool useCache = true);
        Task<AiDetectionResult> DetectAiGeneratedAsync(Stream imageStream, string fileName);
        Task<PlagiarismDetectionResult> CheckPlagiarismAsync(Stream imageStream, string fileName, string imageId, List<string>? compareIds = null);
        Task<ImageComparisonResult> CompareImagesAsync(Stream image1Stream, string file1Name, Stream image2Stream, string file2Name);
        Task<bool> StoreEmbeddingAsync(Stream imageStream, string fileName, string imageId);
        Task<List<string>> ListStoredEmbeddingsAsync();
        Task<bool> DeleteEmbeddingAsync(string imageId);
        Task<ServiceHealthStatus> GetHealthStatusAsync();
        Task<bool> IsHealthyAsync();
    }

    public class FraudDetectionService : IFraudDetectionService
    {
        private readonly HttpClient _httpClient;
        private readonly FraudDetectionOptions _options;
        private readonly ILogger<FraudDetectionService> _logger;

        public FraudDetectionService(
            IHttpClientFactory httpClientFactory,
            IOptions<FraudDetectionOptions> options,
            ILogger<FraudDetectionService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _options = options.Value;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            _httpClient.BaseAddress = new Uri(_options.ServiceUrl);
        }

        public async Task<FraudDetectionResponse> DetectFraudAsync(Stream imageStream, string fileName, string imageId, bool useCache = true)
        {
            if (!_options.Enabled)
            {
                _logger.LogWarning("Fraud detection service is disabled");
                return new FraudDetectionResponse { Success = false, Error = "Service is disabled" };
            }

            try
            {
                using var content = new MultipartFormDataContent();
                using var streamContent = new StreamContent(imageStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "file", fileName);
                content.Add(new StringContent(imageId), "image_id");
                content.Add(new StringContent(useCache ? "true" : "false"), "use_cache");

                _logger.LogInformation("Detecting fraud for image: {FileName} (ID: {ImageId})", fileName, imageId);

                var response = await _httpClient.PostAsync("/detect-fraud", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Fraud detection failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return new FraudDetectionResponse
                    {
                        Success = false,
                        Error = $"Service returned {response.StatusCode}"
                    };
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<FraudDetectionResponse>(jsonContent, options);

                if (result != null)
                {
                    result.Timestamp = DateTime.UtcNow;
                    _logger.LogInformation("Fraud detection complete for {FileName}: Risk={Risk}", fileName, result.FraudRisk);
                }

                return result ?? new FraudDetectionResponse { Success = false, Error = "Invalid response format" };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while detecting fraud for {FileName}", fileName);
                return new FraudDetectionResponse
                {
                    Success = false,
                    Error = $"Service communication failed: {ex.Message}"
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while detecting fraud for {FileName}", fileName);
                return new FraudDetectionResponse
                {
                    Success = false,
                    Error = "Request timed out"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while detecting fraud for {FileName}", fileName);
                return new FraudDetectionResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<AiDetectionResult> DetectAiGeneratedAsync(Stream imageStream, string fileName)
        {
            if (!_options.Enabled)
            {
                return new AiDetectionResult { Success = false, Error = "Service is disabled" };
            }

            try
            {
                using var content = new MultipartFormDataContent();
                using var streamContent = new StreamContent(imageStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "file", fileName);

                var response = await _httpClient.PostAsync("/detect-ai", content);

                if (!response.IsSuccessStatusCode)
                {
                    return new AiDetectionResult
                    {
                        Success = false,
                        Error = $"Service returned {response.StatusCode}"
                    };
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<AiDetectionResult>(jsonContent, options) ?? 
                       new AiDetectionResult { Success = false, Error = "Invalid response" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting AI-generated content for {FileName}", fileName);
                return new AiDetectionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<PlagiarismDetectionResult> CheckPlagiarismAsync(Stream imageStream, string fileName, string imageId, List<string>? compareIds = null)
        {
            if (!_options.Enabled)
            {
                return new PlagiarismDetectionResult { Success = false, Error = "Service is disabled" };
            }

            try
            {
                using var content = new MultipartFormDataContent();
                using var streamContent = new StreamContent(imageStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "file", fileName);
                content.Add(new StringContent(imageId), "image_id");

                if (compareIds != null && compareIds.Count > 0)
                {
                    content.Add(new StringContent(JsonSerializer.Serialize(compareIds)), "compare_ids");
                }

                var response = await _httpClient.PostAsync("/check-plagiarism", content);

                if (!response.IsSuccessStatusCode)
                {
                    return new PlagiarismDetectionResult
                    {
                        Success = false,
                        Error = $"Service returned {response.StatusCode}"
                    };
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<PlagiarismDetectionResult>(jsonContent, options) ?? 
                       new PlagiarismDetectionResult { Success = false, Error = "Invalid response" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking plagiarism for {FileName}", fileName);
                return new PlagiarismDetectionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<ImageComparisonResult> CompareImagesAsync(Stream image1Stream, string file1Name, Stream image2Stream, string file2Name)
        {
            if (!_options.Enabled)
            {
                return new ImageComparisonResult { Success = false, Error = "Service is disabled" };
            }

            try
            {
                using var content = new MultipartFormDataContent();
                using var stream1Content = new StreamContent(image1Stream);
                using var stream2Content = new StreamContent(image2Stream);
                
                stream1Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                stream2Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                
                content.Add(stream1Content, "file1", file1Name);
                content.Add(stream2Content, "file2", file2Name);

                var response = await _httpClient.PostAsync("/compare-images", content);

                if (!response.IsSuccessStatusCode)
                {
                    return new ImageComparisonResult
                    {
                        Success = false,
                        Error = $"Service returned {response.StatusCode}"
                    };
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<ImageComparisonResult>(jsonContent, options) ?? 
                       new ImageComparisonResult { Success = false, Error = "Invalid response" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing images");
                return new ImageComparisonResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<bool> StoreEmbeddingAsync(Stream imageStream, string fileName, string imageId)
        {
            if (!_options.Enabled) return false;

            try
            {
                using var content = new MultipartFormDataContent();
                using var streamContent = new StreamContent(imageStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "file", fileName);
                content.Add(new StringContent(imageId), "image_id");

                var response = await _httpClient.PostAsync("/store-embedding", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing embedding for {ImageId}", imageId);
                return false;
            }
        }

        public async Task<List<string>> ListStoredEmbeddingsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/stored-embeddings");
                if (!response.IsSuccessStatusCode) return new List<string>();

                var jsonContent = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonContent);
                var embeddings = doc.RootElement.GetProperty("embeddings");
                var result = new List<string>();
                foreach (var emb in embeddings.EnumerateArray())
                {
                    var val = emb.GetString();
                    if (val != null) result.Add(val);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing stored embeddings");
                return new List<string>();
            }
        }

        public async Task<bool> DeleteEmbeddingAsync(string imageId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/delete-embedding/{imageId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting embedding {ImageId}", imageId);
                return false;
            }
        }

        public async Task<ServiceHealthStatus> GetHealthStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                if (!response.IsSuccessStatusCode)
                {
                    return new ServiceHealthStatus { Status = "error" };
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<ServiceHealthStatus>(jsonContent, options) ?? 
                       new ServiceHealthStatus { Status = "unknown" };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed");
                return new ServiceHealthStatus { Status = "unreachable" };
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Health check returned false due to exception");
                return false;
            }
        }
    }

    /// <summary>
    /// Mock implementation for when service is unavailable
    /// </summary>
    public class MockFraudDetectionService : IFraudDetectionService
    {
        private readonly ILogger<MockFraudDetectionService> _logger;

        public MockFraudDetectionService(ILogger<MockFraudDetectionService> logger)
        {
            _logger = logger;
        }

        public Task<FraudDetectionResponse> DetectFraudAsync(Stream imageStream, string fileName, string imageId, bool useCache = true)
        {
            _logger.LogWarning("Using mock fraud detection service");
            return Task.FromResult(new FraudDetectionResponse { Success = false, Error = "Fraud detection service unavailable" });
        }

        public Task<AiDetectionResult> DetectAiGeneratedAsync(Stream imageStream, string fileName)
        {
            return Task.FromResult(new AiDetectionResult { Success = false, Error = "Service unavailable" });
        }

        public Task<PlagiarismDetectionResult> CheckPlagiarismAsync(Stream imageStream, string fileName, string imageId, List<string>? compareIds = null)
        {
            return Task.FromResult(new PlagiarismDetectionResult { Success = false, Error = "Service unavailable" });
        }

        public Task<ImageComparisonResult> CompareImagesAsync(Stream image1Stream, string file1Name, Stream image2Stream, string file2Name)
        {
            return Task.FromResult(new ImageComparisonResult { Success = false, Error = "Service unavailable" });
        }

        public Task<bool> StoreEmbeddingAsync(Stream imageStream, string fileName, string imageId)
        {
            return Task.FromResult(false);
        }

        public Task<List<string>> ListStoredEmbeddingsAsync()
        {
            return Task.FromResult(new List<string>());
        }

        public Task<bool> DeleteEmbeddingAsync(string imageId)
        {
            return Task.FromResult(false);
        }

        public Task<ServiceHealthStatus> GetHealthStatusAsync()
        {
            return Task.FromResult(new ServiceHealthStatus { Status = "unavailable" });
        }

        public Task<bool> IsHealthyAsync()
        {
            return Task.FromResult(false);
        }
    }
}
