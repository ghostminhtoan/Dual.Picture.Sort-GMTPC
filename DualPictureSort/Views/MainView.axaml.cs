using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DualPictureSort.ViewModels;

namespace DualPictureSort.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Focusable = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is MainViewModel vm)
        {
            vm.FolderPicker = async (title) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return null;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    return folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;
                }

                return null;
            };
        }
    }

    private void OnSourceListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedSourceItem != null)
        {
            _ = vm.MoveSourceToSortedAsync(vm.SelectedSourceItem);
        }
    }

    private void OnSortedListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedSortedItem != null)
        {
            _ = vm.MoveSortedBackToSourceAsync(vm.SelectedSortedItem);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not MainViewModel vm) return;

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Z)
        {
            _ = vm.UndoAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Right || e.Key == Key.Enter || e.Key == Key.Space)
        {
            if (vm.SelectedSourceItem != null)
            {
                _ = vm.MoveSourceToSortedAsync(vm.SelectedSourceItem);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Left || e.Key == Key.Back)
        {
            if (vm.SelectedSortedItem != null)
            {
                _ = vm.MoveSortedBackToSourceAsync(vm.SelectedSortedItem);
                e.Handled = true;
            }
        }
    }
}