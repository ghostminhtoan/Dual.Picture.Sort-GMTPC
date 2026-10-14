using System.Collections.Generic;
using System.Threading.Tasks;
using DualPictureSort.Models;

namespace DualPictureSort.Services;

public record SortOperation(string OriginalPath, string DestinationPath, bool IsMove);

public interface IImageSortService
{
    Task<List<PictureItem>> GetImagesAsync(string directoryPath, string? excludeSubdirectory = null);
    Task<string> MoveImageAsync(string sourceFilePath, string targetDirectory);
    Task<string> CopyImageAsync(string sourceFilePath, string targetDirectory);
    Task<bool> RevertOperationAsync(SortOperation operation);
}

