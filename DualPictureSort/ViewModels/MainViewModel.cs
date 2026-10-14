using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DualPictureSort.Models;
using DualPictureSort.Services;

namespace DualPictureSort.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IImageSortService _sortService;
    private readonly Stack<SortOperation> _undoStack = new();
    private CancellationTokenSource? _thumbnailCts;

    public Func<string, Task<string?>>? FolderPicker { get; set; }

    [ObservableProperty]
    private string _sourceFolderPath = string.Empty;

    [ObservableProperty]
    private string _targetFolderPath = string.Empty;

    [ObservableProperty]
    private bool _autoDeriveTargetFolder = true;

    [ObservableProperty]
    private bool _isMoveMode = true; // true = Move, false = Copy

    [ObservableProperty]
    private bool _autoMoveOnClick = false;

    [ObservableProperty]
    private string _sourceSearchText = string.Empty;

    [ObservableProperty]
    private string _sortedSearchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng. Chọn thư mục ảnh gốc để bắt đầu.";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private PictureItem? _selectedSourceItem;

    [ObservableProperty]
    private PictureItem? _selectedSortedItem;

    [ObservableProperty]
    private PictureItem? _currentPreviewItem;

    [ObservableProperty]
    private Bitmap? _previewBitmap;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private int _sourceCount;

    [ObservableProperty]
    private int _sortedCount;

    public ObservableCollection<PictureItem> SourceImages { get; } = [];
    public ObservableCollection<PictureItem> SortedImages { get; } = [];

    private readonly List<PictureItem> _allSourceImages = [];
    private readonly List<PictureItem> _allSortedImages = [];

    public MainViewModel() : this(new ImageSortService())
    {
    }

    public MainViewModel(IImageSortService sortService)
    {
        _sortService = sortService;
    }

    partial void OnSourceFolderPathChanged(string value)
    {
        if (AutoDeriveTargetFolder && !string.IsNullOrWhiteSpace(value))
        {
            var folderName = Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            TargetFolderPath = Path.Combine(value, $"{folderName}-sorted");
        }

        _ = ReloadAllFoldersAsync();
    }

    partial void OnTargetFolderPathChanged(string value)
    {
        _ = ReloadSortedFolderAsync();
    }

    partial void OnSourceSearchTextChanged(string value)
    {
        ApplySourceFilter();
    }

    partial void OnSortedSearchTextChanged(string value)
    {
        ApplySortedFilter();
    }

    partial void OnSelectedSourceItemChanged(PictureItem? value)
    {
        if (value != null)
        {
            CurrentPreviewItem = value;
            _ = LoadPreviewBitmapAsync(value.FilePath);

            if (AutoMoveOnClick)
            {
                _ = MoveSourceToSortedAsync(value);
            }
        }
    }

    partial void OnSelectedSortedItemChanged(PictureItem? value)
    {
        if (value != null)
        {
            CurrentPreviewItem = value;
            _ = LoadPreviewBitmapAsync(value.FilePath);
        }
    }

    private async Task LoadPreviewBitmapAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            PreviewBitmap = null;
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                using var stream = File.OpenRead(filePath);
                var bmp = new Bitmap(stream);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    PreviewBitmap = bmp;
                });
            });
        }
        catch
        {
            PreviewBitmap = null;
        }
    }

    [RelayCommand]
    public async Task PickSourceFolderAsync()
    {
        if (FolderPicker == null) return;
        var folder = await FolderPicker("Chọn thư mục ảnh gốc");
        if (!string.IsNullOrEmpty(folder))
        {
            SourceFolderPath = folder;
        }
    }

    [RelayCommand]
    public async Task PickTargetFolderAsync()
    {
        if (FolderPicker == null) return;
        var folder = await FolderPicker("Chọn thư mục đã lọc (Sorted)");
        if (!string.IsNullOrEmpty(folder))
        {
            AutoDeriveTargetFolder = false;
            TargetFolderPath = folder;
        }
    }

    [RelayCommand]
    public async Task ReloadAllFoldersAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceFolderPath) || !Directory.Exists(SourceFolderPath))
        {
            SourceImages.Clear();
            _allSourceImages.Clear();
            SourceCount = 0;
            return;
        }

        IsLoading = true;
        StatusMessage = "Đang quét danh sách ảnh...";

        try
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts = new CancellationTokenSource();
            var ct = _thumbnailCts.Token;

            // Load source images
            var sourceList = await _sortService.GetImagesAsync(SourceFolderPath, TargetFolderPath);
            _allSourceImages.Clear();
            _allSourceImages.AddRange(sourceList);
            ApplySourceFilter();

            // Load sorted images
            await ReloadSortedFolderInternalAsync();

            StatusMessage = $"Đã tải {SourceCount} ảnh gốc, {SortedCount} ảnh đã lọc.";

            // Start lazy background thumbnail loading
            _ = LoadThumbnailsBackgroundAsync(_allSourceImages.Concat(_allSortedImages).ToList(), ct);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khi tải thư mục: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ReloadSortedFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetFolderPath) || !Directory.Exists(TargetFolderPath))
        {
            SortedImages.Clear();
            _allSortedImages.Clear();
            SortedCount = 0;
            return;
        }

        await ReloadSortedFolderInternalAsync();
    }

    private async Task ReloadSortedFolderInternalAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetFolderPath) || !Directory.Exists(TargetFolderPath))
        {
            SortedImages.Clear();
            _allSortedImages.Clear();
            SortedCount = 0;
            return;
        }

        var sortedList = await _sortService.GetImagesAsync(TargetFolderPath);
        _allSortedImages.Clear();
        _allSortedImages.AddRange(sortedList);
        ApplySortedFilter();
    }

    private async Task LoadThumbnailsBackgroundAsync(List<PictureItem> items, CancellationToken ct)
    {
        using var semaphore = new SemaphoreSlim(4);
        var tasks = items.Select(async item =>
        {
            if (ct.IsCancellationRequested) return;
            await semaphore.WaitAsync(ct);
            try
            {
                if (!ct.IsCancellationRequested)
                {
                    await item.LoadThumbnailAsync();
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
    }

    private void ApplySourceFilter()
    {
        SourceImages.Clear();
        var query = _allSourceImages.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SourceSearchText))
        {
            query = query.Where(x => x.FileName.Contains(SourceSearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            SourceImages.Add(item);
        }
        SourceCount = SourceImages.Count;
    }

    private void ApplySortedFilter()
    {
        SortedImages.Clear();
        var query = _allSortedImages.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SortedSearchText))
        {
            query = query.Where(x => x.FileName.Contains(SortedSearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            SortedImages.Add(item);
        }
        SortedCount = SortedImages.Count;
    }

    [RelayCommand]
    public async Task MoveSourceToSortedAsync(PictureItem? item)
    {
        item ??= SelectedSourceItem;
        if (item == null || string.IsNullOrWhiteSpace(TargetFolderPath))
            return;

        try
        {
            var originalPath = item.FilePath;
            string destPath;

            if (IsMoveMode)
            {
                destPath = await _sortService.MoveImageAsync(originalPath, TargetFolderPath);
                _undoStack.Push(new SortOperation(originalPath, destPath, IsMove: true));

                _allSourceImages.Remove(item);
                SourceImages.Remove(item);
                SourceCount = SourceImages.Count;
            }
            else
            {
                destPath = await _sortService.CopyImageAsync(originalPath, TargetFolderPath);
                _undoStack.Push(new SortOperation(originalPath, destPath, IsMove: false));
            }

            var newItem = new PictureItem(destPath);
            _allSortedImages.Insert(0, newItem);
            SortedImages.Insert(0, newItem);
            SortedCount = SortedImages.Count;

            _ = newItem.LoadThumbnailAsync();

            CanUndo = _undoStack.Count > 0;
            StatusMessage = $"{(IsMoveMode ? "Đã chuyển" : "Đã sao chép")}: {item.FileName} -> {Path.GetFileName(TargetFolderPath)}";

            // Automatically select next available source item if possible
            if (SourceImages.Count > 0)
            {
                SelectedSourceItem = SourceImages.FirstOrDefault();
            }
            else
            {
                SelectedSourceItem = null;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khi chuyển ảnh: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task MoveSortedBackToSourceAsync(PictureItem? item)
    {
        item ??= SelectedSortedItem;
        if (item == null || string.IsNullOrWhiteSpace(SourceFolderPath))
            return;

        try
        {
            var originalPath = item.FilePath;
            var destPath = await _sortService.MoveImageAsync(originalPath, SourceFolderPath);

            _allSortedImages.Remove(item);
            SortedImages.Remove(item);
            SortedCount = SortedImages.Count;

            var newItem = new PictureItem(destPath);
            _allSourceImages.Add(newItem);
            SourceImages.Add(newItem);
            SourceCount = SourceImages.Count;

            _ = newItem.LoadThumbnailAsync();

            StatusMessage = $"Đã trả lại: {item.FileName} -> {Path.GetFileName(SourceFolderPath)}";

            if (SortedImages.Count > 0)
            {
                SelectedSortedItem = SortedImages.FirstOrDefault();
            }
            else
            {
                SelectedSortedItem = null;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khi trả lại ảnh: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task UndoAsync()
    {
        if (_undoStack.Count == 0) return;

        var lastOp = _undoStack.Pop();
        CanUndo = _undoStack.Count > 0;

        try
        {
            var success = await _sortService.RevertOperationAsync(lastOp);
            if (success)
            {
                StatusMessage = $"Đã hoàn tác thao tác với '{Path.GetFileName(lastOp.OriginalPath)}'.";
                await ReloadAllFoldersAsync();
            }
            else
            {
                StatusMessage = "Không thể hoàn tác thao tác trước.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi hoàn tác: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenSourceFolder()
    {
        if (Directory.Exists(SourceFolderPath))
        {
            OpenFileExplorer(SourceFolderPath);
        }
    }

    [RelayCommand]
    public void OpenTargetFolder()
    {
        if (Directory.Exists(TargetFolderPath))
        {
            OpenFileExplorer(TargetFolderPath);
        }
    }

    private static void OpenFileExplorer(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", path);
            }
        }
        catch
        {
            // Silently ignore if cannot launch OS explorer
        }
    }
}
