using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace Final_Year_Project.Services
{
    public class LocalStorageService
    {
        private readonly string _rootPath;

        public LocalStorageService(IWebHostEnvironment env)
        {
            _rootPath = Path.Combine(env.WebRootPath, "uploads");
            if (!Directory.Exists(_rootPath))
                Directory.CreateDirectory(_rootPath);
        }

        /// <summary>
        /// Saves a file to local storage.
        /// </summary>
        /// <param name="file">The uploaded file</param>
        /// <param name="folderName">Folder name under /uploads</param>
        /// <param name="allowedContentTypes">Optional: restrict allowed MIME types</param>
        /// <param name="useOriginalFileName">Default false: generate random file name</param>
        public async Task<string?> SaveAsync(
            IFormFile file,
            string folderName,
            string[]? allowedContentTypes = null,
            bool useOriginalFileName = false)
        {
            if (file == null || file.Length == 0)
                return null;

            var safeFolder = SanitizeFolderName(folderName);
            var folderPath = Path.Combine(_rootPath, safeFolder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // Validate MIME type if allowedContentTypes provided
            if (allowedContentTypes != null && !allowedContentTypes.Contains(file.ContentType))
                return null;

            var ext = Path.GetExtension(file.FileName);
            var fileName = useOriginalFileName
                ? Path.GetFileName(file.FileName)
                : $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(folderPath, fileName);

            try
            {
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                return fileName;
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// Rebuild full URL for serving files in wwwroot/uploads
        /// </summary>
        public string BuildFileUrl(string folderName, string fileName)
        {
            var safeFolder = SanitizeFolderName(folderName);
            return $"/uploads/{safeFolder}/{fileName}";
        }

        /// <summary>
        /// Retrieves a file from a folder for download or display.
        /// </summary>
        public FileStreamResult? Get(string folderName, string fileName, string contentType)
        {
            var folderPath = Path.Combine(_rootPath, SanitizeFolderName(folderName));
            var filePath = Path.Combine(folderPath, fileName);

            if (!File.Exists(filePath))
                return null;

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return new FileStreamResult(stream, contentType);
        }

        /// <summary>
        /// Deletes a file from a folder.
        /// </summary>
        public bool Delete(string folderName, string fileName)
        {
            var folderPath = Path.Combine(_rootPath, SanitizeFolderName(folderName));
            var filePath = Path.Combine(folderPath, fileName);

            if (!File.Exists(filePath))
                return false;

            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Prevent directory traversal by sanitizing folder names.
        /// </summary>
        private string SanitizeFolderName(string folderName)
        {
            return Path.GetFileName(folderName);
        }

        /// <summary>
        /// Returns a default list of allowed MIME types for Editor.js attaches
        /// </summary>
        public static readonly string[] DefaultAllowedContentTypes = new[]
        {
            "image/jpeg", "image/png", "image/gif", "image/webp", "image/vnd.adobe.photoshop",
            "audio/mpeg", "audio/wav", "audio/flac",
            "video/mp4", "video/avi", "video/mov",
            "application/pdf", "application/zip", "application/x-zip-compressed",
            "text/plain", "application/octet-stream"
        };
    }
}
