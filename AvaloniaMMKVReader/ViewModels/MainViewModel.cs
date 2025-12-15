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
    
    // 存储文件数据（用于 Android 等平台无法直接访问文件路径的情况）
    private byte[]? _dataFileBytes;
    private byte[]? _crcFileBytes;

    [ObservableProperty]
    private string _statusMessage = "点击按钮选择 MMKV 文件";

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
                if (HasDataFile && _dataFileBytes != null)
                {
                    _ = ParseDataAsync();
                }
            }
        }
    }

    /// <summary>
    /// 处理拖放的文件（桌面端）
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
                _crcFileBytes = await File.ReadAllBytesAsync(path);
                HasCrcFile = true;
            }
            else
            {
                // 数据文件（无后缀或其他后缀）
                DataFilePath = path;
                _dataFileBytes = await File.ReadAllBytesAsync(path);
                HasDataFile = true;
            }
        }

        UpdateStatus();

        if (HasDataFile && _dataFileBytes != null)
        {
            await ParseDataAsync();
        }
    }

    /// <summary>
    /// 选择数据文件
    /// </summary>
    public async Task SelectDataFileAsync(IStorageProvider storageProvider)
    {
        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择 MMKV 数据文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                DataFilePath = file.Name; // 使用文件名而不是路径
                
                // 通过流读取文件内容（支持 Android content:// URI）
                await using var stream = await file.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                _dataFileBytes = memoryStream.ToArray();
                
                HasDataFile = true;
                UpdateStatus();
                await ParseDataAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"选择文件失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 选择 CRC 文件
    /// </summary>
    public async Task SelectCrcFileAsync(IStorageProvider storageProvider)
    {
        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择 CRC 文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("CRC 文件") { Patterns = new[] { "*.crc" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                CrcFilePath = file.Name;
                
                // 通过流读取文件内容
                await using var stream = await file.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                _crcFileBytes = memoryStream.ToArray();
                
                HasCrcFile = true;
                UpdateStatus();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"选择 CRC 文件失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        DataFilePath = string.Empty;
        CrcFilePath = string.Empty;
        HasDataFile = false;
        HasCrcFile = false;
        _dataFileBytes = null;
        _crcFileBytes = null;
        Items.Clear();
        FileInfo = string.Empty;
        StatusMessage = "点击按钮选择 MMKV 文件";
    }

    private async Task ParseDataAsync()
    {
        if (!HasDataFile || _dataFileBytes == null)
            return;

        IsLoading = true;
        StatusMessage = "正在解析...";

        try
        {
            await Task.Run(() =>
            {
                var results = _parser.ParseBytes(_dataFileBytes, SelectedDataType);
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Items.Clear();
                    foreach (var item in results)
                    {
                        Items.Add(item);
                    }

                    var fileSize = _dataFileBytes.Length;
                    FileInfo = $"大小: {FormatFileSize(fileSize)} | {Items.Count} 条";
                    StatusMessage = $"解析完成，共 {Items.Count} 条记录";
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

    // 保留旧方法用于兼容桌面端文件路径方式
    private async Task ParseFileAsync()
    {
        if (!HasDataFile || string.IsNullOrEmpty(DataFilePath))
            return;

        // 如果已有字节数据，直接解析
        if (_dataFileBytes != null)
        {
            await ParseDataAsync();
            return;
        }

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
                    FileInfo = $"大小: {FormatFileSize(fileSize)} | {Items.Count} 条";
                    StatusMessage = $"解析完成，共 {Items.Count} 条记录";
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
            parts.Add($"数据: {DataFilePath}");
        
        if (HasCrcFile)
            parts.Add($"CRC: {CrcFilePath}");

        if (parts.Count > 0)
            StatusMessage = string.Join(" | ", parts);
        else
            StatusMessage = "点击按钮选择 MMKV 文件";
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
