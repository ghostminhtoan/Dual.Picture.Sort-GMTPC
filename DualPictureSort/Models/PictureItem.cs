using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DualPictureSort.Models;

public partial class PictureItem : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private string _fileSizeFormatted = string.Empty;

    [ObservableProperty]
    private DateTime _lastModified;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isLoadingThumbnail;

    [ObservableProperty]
    private string _dimensions = string.Empty;

    public PictureItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        
        try
        {
            var fileInfo = new FileInfo(filePath);
            FileSize = fileInfo.Length;
            FileSizeFormatted = FormatBytes(FileSize);
            LastModified = fileInfo.LastWriteTime;
        }
        catch
        {
            FileSize = 0;
            FileSizeFormatted = "0 KB";
            LastModified = DateTime.Now;
        }
    }

    public async Task LoadThumbnailAsync(int targetWidth = 160)
    {
        if (Thumbnail != null || IsLoadingThumbnail || !File.Exists(FilePath))
            return;

        IsLoadingThumbnail = true;

        try
        {
            await Task.Run(() =>
            {
                using var stream = File.OpenRead(FilePath);
                var bmp = Bitmap.DecodeToWidth(stream, targetWidth, BitmapInterpolationMode.MediumQuality);
                Dimensions = $"{bmp.PixelSize.Width}x{bmp.PixelSize.Height}";
                Thumbnail = bmp;
            });
        }
        catch
        {
            // If failed to decode, keep null
        }
        finally
        {
            IsLoadingThumbnail = false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }
}
