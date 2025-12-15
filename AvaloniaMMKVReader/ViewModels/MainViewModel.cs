using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaMMKVReader.Models;
using AvaloniaMMKVReader.Services;

namespace AvaloniaMMKVReader.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly MMKVParser _parser = new();

    [ObservableProperty]
    private string _statusMessage = "拖放 MMKV 数据文件和 CRC 文件到此处，或点击按钮选择文件";

    [ObservableProperty]
    private string _dataFilePath = string.Empty;

    [ObservableProperty]
    private string _crcFilePath = string.Empty;

    [ObservableProperty]
    private bool _hasDataFile;

    [ObservableProperty]
    private bool _hasCrcFile;

    [ObservableProperty]
    private ObservableCollection<MMKVItem> _items = new();

    [ObservableProperty]
    private MMKVDataType _selectedDataType = MMKVDataType.Auto;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _fileInfo = string.Empty;

    public string[] DataTypeOptions => Enum.GetNames<MMKVDataType>();

    public string SelectedDataTypeString
    {
        get => SelectedDataType.ToString();
        set
        {
            if (Enum.TryParse<MMKVDataType>(value, out var type))
            {
                SelectedDataType = type;
                // 重新解析
                if (HasDataFile)
                {
                    _ = ParseFileAsync();
                }
            }
        }
    }

    /// <summary>
    /// 处理拖放的文件
    /// </summary>
    public async Task HandleDroppedFilesAsync(string[] filePaths)
    {
        foreach (var path in filePaths)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var fileName = Path.GetFileName(path).ToLowerInvariant();

            if (extension == ".crc" || fileName.EndsWith(".crc"))
            {
                CrcFilePath = path;
                HasCrcFile = true;
            }
            else
            {
                // 数据文件（无后缀或其他后缀）
                DataFilePath = path;
                HasDataFile = true;
            }
        }

        UpdateStatus();

        if (HasDataFile)
        {
            await ParseFileAsync();
        }
    }

    /// <summary>
    /// 选择数据文件
    /// </summary>
    public async Task SelectDataFileAsync(IStorageProvider storageProvider)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 MMKV 数据文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("MMKV 文件") { Patterns = new[] { "*" } },
                new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            DataFilePath = files[0].Path.LocalPath;
            HasDataFile = true;
            UpdateStatus();
            await ParseFileAsync();
        }
    }

    /// <summary>
    /// 选择 CRC 文件
    /// </summary>
    public async Task SelectCrcFileAsync(IStorageProvider storageProvider)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 CRC 文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CRC 文件") { Patterns = new[] { "*.crc" } },
                new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            CrcFilePath = files[0].Path.LocalPath;
            HasCrcFile = true;
            UpdateStatus();
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        DataFilePath = string.Empty;
        CrcFilePath = string.Empty;
        HasDataFile = false;
        HasCrcFile = false;
        Items.Clear();
        FileInfo = string.Empty;
        StatusMessage = "拖放 MMKV 数据文件和 CRC 文件到此处，或点击按钮选择文件";
    }

    private async Task ParseFileAsync()
    {
        if (!HasDataFile || string.IsNullOrEmpty(DataFilePath))
            return;

        IsLoading = true;
        StatusMessage = "正在解析...";

        try
        {
            await Task.Run(() =>
            {
                var results = _parser.Parse(DataFilePath, CrcFilePath, SelectedDataType);
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Items.Clear();
                    foreach (var item in results)
                    {
                        Items.Add(item);
                    }

                    var fileSize = new FileInfo(DataFilePath).Length;
                    FileInfo = $"文件大小: {FormatFileSize(fileSize)} | 共 {Items.Count} 条记录";
                    StatusMessage = $"解析完成，共找到 {Items.Count} 条记录";
                });
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"解析失败: {ex.Message}";
            FileInfo = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateStatus()
    {
        var parts = new System.Collections.Generic.List<string>();
        
        if (HasDataFile)
            parts.Add($"数据文件: {Path.GetFileName(DataFilePath)}");
        
        if (HasCrcFile)
            parts.Add($"CRC文件: {Path.GetFileName(CrcFilePath)}");

        if (parts.Count > 0)
            StatusMessage = string.Join(" | ", parts);
        else
            StatusMessage = "拖放 MMKV 数据文件和 CRC 文件到此处，或点击按钮选择文件";
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
