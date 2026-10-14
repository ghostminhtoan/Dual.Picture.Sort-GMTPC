using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DualPictureSort.Models;

namespace DualPictureSort.Services;

public class ImageSortService : IImageSortService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".avif", ".ico"
    };

    public Task<List<PictureItem>> GetImagesAsync(string directoryPath, string? excludeSubdirectory = null)
    {
        return Task.Run(() =>
        {
            var result = new List<PictureItem>();

            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                return result;

            string? normalizedExclude = null;
            if (!string.IsNullOrWhiteSpace(excludeSubdirectory))
            {
                normalizedExclude = Path.GetFullPath(excludeSubdirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            try
            {
                var dirInfo = new DirectoryInfo(directoryPath);
                var files = dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    if (SupportedExtensions.Contains(file.Extension))
                    {
                        result.Add(new PictureItem(file.FullName));
                    }
                }
            }
            catch (Exception)
            {
                // Silently handle read errors or permission issues
            }

            return result;
        });
    }

    public Task<string> MoveImageAsync(string sourceFilePath, string targetDirectory)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Tệp nguồn không tồn tại.", sourceFilePath);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var fileName = Path.GetFileName(sourceFilePath);
            var destPath = GetUniqueDestinationPath(targetDirectory, fileName);

            File.Move(sourceFilePath, destPath);
            return destPath;
        });
    }

    public Task<string> CopyImageAsync(string sourceFilePath, string targetDirectory)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Tệp nguồn không tồn tại.", sourceFilePath);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var fileName = Path.GetFileName(sourceFilePath);
            var destPath = GetUniqueDestinationPath(targetDirectory, fileName);

            File.Copy(sourceFilePath, destPath, overwrite: false);
            return destPath;
        });
    }

    public Task<bool> RevertOperationAsync(SortOperation operation)
    {
        return Task.Run(() =>
        {
            try
            {
                if (operation.IsMove)
                {
                    if (File.Exists(operation.DestinationPath))
                    {
                        var originalDir = Path.GetDirectoryName(operation.OriginalPath);
                        if (!string.IsNullOrEmpty(originalDir) && !Directory.Exists(originalDir))
                        {
                            Directory.CreateDirectory(originalDir);
                        }

                        // Move back
                        if (!File.Exists(operation.OriginalPath))
                        {
                            File.Move(operation.DestinationPath, operation.OriginalPath);
                        }
                        else
                        {
                            var uniquePath = GetUniqueDestinationPath(originalDir!, Path.GetFileName(operation.OriginalPath));
                            File.Move(operation.DestinationPath, uniquePath);
                        }
                        return true;
                    }
                }
                else
                {
                    // Was copy, delete copied file
                    if (File.Exists(operation.DestinationPath))
                    {
                        File.Delete(operation.DestinationPath);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        });
    }

    private static string GetUniqueDestinationPath(string targetDirectory, string fileName)
    {
        var destPath = Path.Combine(targetDirectory, fileName);
        if (!File.Exists(destPath))
            return destPath;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        int counter = 1;

        while (File.Exists(destPath))
        {
            destPath = Path.Combine(targetDirectory, $"{nameWithoutExt}_{counter}{ext}");
            counter++;
        }

        return destPath;
    }
}
