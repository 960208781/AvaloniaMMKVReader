using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaMMKVReader.ViewModels;

namespace AvaloniaMMKVReader.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        // 设置拖放事件
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        
        // 按钮事件
        SelectDataFileBtn.Click += SelectDataFileBtn_Click;
        SelectCrcFileBtn.Click += SelectCrcFileBtn_Click;
        AddCrcFileBtn.Click += SelectCrcFileBtn_Click;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // 检查是否包含文件
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel == null) return;

        try
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var paths = files
                    .Select(f => f.Path.LocalPath)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
                
                if (paths.Length > 0)
                {
                    await ViewModel.HandleDroppedFilesAsync(paths);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"拖放处理错误: {ex.Message}");
        }
    }

    private async void SelectDataFileBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        await ViewModel.SelectDataFileAsync(StorageProvider);
    }

    private async void SelectCrcFileBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        await ViewModel.SelectCrcFileAsync(StorageProvider);
    }
}
