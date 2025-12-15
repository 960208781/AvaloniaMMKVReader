using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaMMKVReader.ViewModels;

namespace AvaloniaMMKVReader.Views;

public partial class MainView : UserControl
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainView()
    {
        InitializeComponent();
        
        // 按钮事件
        Loaded += MainView_Loaded;
    }

    private void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        // 在控件加载后绑定按钮事件
        var selectDataFileBtn = this.FindControl<Button>("SelectDataFileBtn");
        var selectCrcFileBtn = this.FindControl<Button>("SelectCrcFileBtn");
        var reselectDataFileBtn = this.FindControl<Button>("ReselectDataFileBtn");

        if (selectDataFileBtn != null)
            selectDataFileBtn.Click += SelectDataFileBtn_Click;
        if (selectCrcFileBtn != null)
            selectCrcFileBtn.Click += SelectCrcFileBtn_Click;
        if (reselectDataFileBtn != null)
            reselectDataFileBtn.Click += SelectDataFileBtn_Click;
    }

    private async void SelectDataFileBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider != null)
            {
                await ViewModel.SelectDataFileAsync(topLevel.StorageProvider);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"选择文件错误: {ex.Message}");
        }
    }

    private async void SelectCrcFileBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider != null)
            {
                await ViewModel.SelectCrcFileAsync(topLevel.StorageProvider);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"选择 CRC 文件错误: {ex.Message}");
        }
    }
}
