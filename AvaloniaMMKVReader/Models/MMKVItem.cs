namespace AvaloniaMMKVReader.Models;

/// <summary>
/// MMKV 键值对数据项
/// </summary>
public class MMKVItem
{
    public int Index { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int RawLength { get; set; }
}

/// <summary>
/// MMKV 数据类型
/// </summary>
public enum MMKVDataType
{
    Auto,
    String,
    Int32,
    Int64,
    Float,
    Double,
    Bool,
    Bytes
}

