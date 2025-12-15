using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AvaloniaMMKVReader.Models;

namespace AvaloniaMMKVReader.Services;

/// <summary>
/// MMKV 文件解析器
/// MMKV 使用 Protocol Buffer 格式存储数据
/// </summary>
public class MMKVParser
{
    private byte[] _data = Array.Empty<byte>();
    private int _position;
    private int _dataLength;

    /// <summary>
    /// 解析 MMKV 文件
    /// </summary>
    public List<MMKVItem> Parse(string dataFilePath, string? crcFilePath = null, MMKVDataType preferredType = MMKVDataType.Auto)
    {
        var items = new List<MMKVItem>();

        if (!File.Exists(dataFilePath))
            throw new FileNotFoundException("MMKV 数据文件不存在", dataFilePath);

        _data = File.ReadAllBytes(dataFilePath);
        _position = 0;

        // 验证 CRC（如果提供了 CRC 文件）
        if (!string.IsNullOrEmpty(crcFilePath) && File.Exists(crcFilePath))
        {
            // CRC 文件包含校验信息，这里简单跳过验证
            // 实际上可以用来验证数据完整性
        }

        // MMKV 文件头：前 4 字节是数据长度
        if (_data.Length < 4)
            throw new InvalidDataException("MMKV 文件格式无效：文件太小");

        _dataLength = ReadInt32LittleEndian();
        
        // 数据长度不能超过文件实际长度
        if (_dataLength > _data.Length - 4)
            _dataLength = _data.Length - 4;

        int index = 0;
        while (_position < 4 + _dataLength && _position < _data.Length)
        {
            try
            {
                var item = ReadKeyValue(index++, preferredType);
                if (item != null)
                {
                    items.Add(item);
                }
            }
            catch
            {
                // 解析错误时跳过，继续尝试下一个
                break;
            }
        }

        return items;
    }

    /// <summary>
    /// 直接从字节数组解析
    /// </summary>
    public List<MMKVItem> ParseBytes(byte[] data, MMKVDataType preferredType = MMKVDataType.Auto)
    {
        var items = new List<MMKVItem>();
        _data = data;
        _position = 0;

        if (_data.Length < 4)
            return items;

        _dataLength = ReadInt32LittleEndian();
        if (_dataLength > _data.Length - 4)
            _dataLength = _data.Length - 4;

        int index = 0;
        while (_position < 4 + _dataLength && _position < _data.Length)
        {
            try
            {
                var item = ReadKeyValue(index++, preferredType);
                if (item != null)
                {
                    items.Add(item);
                }
            }
            catch
            {
                break;
            }
        }

        return items;
    }

    private MMKVItem? ReadKeyValue(int index, MMKVDataType preferredType)
    {
        // 读取 key
        int keyLength = ReadVarint32();
        if (keyLength <= 0 || _position + keyLength > _data.Length)
            return null;

        string key = Encoding.UTF8.GetString(_data, _position, keyLength);
        _position += keyLength;

        // 读取 value
        int valueLength = ReadVarint32();
        if (valueLength < 0 || _position + valueLength > _data.Length)
            return null;

        byte[] valueBytes = new byte[valueLength];
        Array.Copy(_data, _position, valueBytes, 0, valueLength);
        _position += valueLength;

        // 解析 value
        var (value, type) = ParseValue(valueBytes, preferredType);

        return new MMKVItem
        {
            Index = index,
            Key = key,
            Value = value,
            Type = type,
            RawLength = valueLength
        };
    }

    private (string value, string type) ParseValue(byte[] valueBytes, MMKVDataType preferredType)
    {
        if (valueBytes.Length == 0)
            return ("(empty)", "Empty");

        try
        {
            switch (preferredType)
            {
                case MMKVDataType.String:
                    return (ParseAsString(valueBytes), "String");
                
                case MMKVDataType.Int32:
                    return (ParseAsInt32(valueBytes).ToString(), "Int32");
                
                case MMKVDataType.Int64:
                    return (ParseAsInt64(valueBytes).ToString(), "Int64");
                
                case MMKVDataType.Float:
                    return (ParseAsFloat(valueBytes).ToString("G"), "Float");
                
                case MMKVDataType.Double:
                    return (ParseAsDouble(valueBytes).ToString("G"), "Double");
                
                case MMKVDataType.Bool:
                    return (ParseAsBool(valueBytes).ToString(), "Bool");
                
                case MMKVDataType.Bytes:
                    return (ParseAsHex(valueBytes), "Bytes");
                
                case MMKVDataType.Auto:
                default:
                    return AutoDetectAndParse(valueBytes);
            }
        }
        catch
        {
            return (ParseAsHex(valueBytes), "Bytes");
        }
    }

    private (string value, string type) AutoDetectAndParse(byte[] valueBytes)
    {
        // 尝试按照优先级解析
        
        // 1. 检查是否是布尔值（1字节，0或1）
        if (valueBytes.Length == 1)
        {
            if (valueBytes[0] == 0 || valueBytes[0] == 1)
                return (valueBytes[0] == 1 ? "True" : "False", "Bool");
        }

        // 2. 尝试作为 UTF-8 字符串解析
        try
        {
            // 先检查是否是 Protocol Buffer 格式的字符串（带长度前缀）
            int pos = 0;
            int strLen = ReadVarint32FromBytes(valueBytes, ref pos);
            if (strLen > 0 && pos + strLen == valueBytes.Length)
            {
                string str = Encoding.UTF8.GetString(valueBytes, pos, strLen);
                if (IsValidUtf8String(str))
                    return (str, "String");
            }
        }
        catch { }

        // 3. 尝试直接作为字符串
        try
        {
            string directStr = Encoding.UTF8.GetString(valueBytes);
            if (IsValidUtf8String(directStr) && !HasControlChars(directStr))
                return (directStr, "String");
        }
        catch { }

        // 4. 检查是否是数字
        if (valueBytes.Length == 4)
        {
            // 可能是 Int32 或 Float
            int intVal = BitConverter.ToInt32(valueBytes, 0);
            float floatVal = BitConverter.ToSingle(valueBytes, 0);
            
            // 如果 float 值看起来合理，返回两者
            if (Math.Abs(intVal) < 1000000000)
                return ($"{intVal}", "Int32");
        }

        if (valueBytes.Length == 8)
        {
            // 可能是 Int64 或 Double
            long longVal = BitConverter.ToInt64(valueBytes, 0);
            if (Math.Abs(longVal) < 1000000000000000)
                return ($"{longVal}", "Int64");
        }

        // 5. 尝试 Varint
        try
        {
            int varPos = 0;
            long varint = ReadVarint64FromBytes(valueBytes, ref varPos);
            if (varPos == valueBytes.Length)
                return ($"{varint}", "Varint");
        }
        catch { }

        // 6. 默认返回十六进制
        return (ParseAsHex(valueBytes), "Bytes");
    }

    private string ParseAsString(byte[] bytes)
    {
        // 尝试带长度前缀的字符串
        try
        {
            int pos = 0;
            int len = ReadVarint32FromBytes(bytes, ref pos);
            if (len > 0 && pos + len <= bytes.Length)
                return Encoding.UTF8.GetString(bytes, pos, len);
        }
        catch { }

        // 直接解析
        return Encoding.UTF8.GetString(bytes);
    }

    private int ParseAsInt32(byte[] bytes)
    {
        if (bytes.Length >= 4)
            return BitConverter.ToInt32(bytes, 0);
        
        // 尝试 Varint
        int pos = 0;
        return ReadVarint32FromBytes(bytes, ref pos);
    }

    private long ParseAsInt64(byte[] bytes)
    {
        if (bytes.Length >= 8)
            return BitConverter.ToInt64(bytes, 0);
        
        // 尝试 Varint
        int pos = 0;
        return ReadVarint64FromBytes(bytes, ref pos);
    }

    private float ParseAsFloat(byte[] bytes)
    {
        if (bytes.Length >= 4)
            return BitConverter.ToSingle(bytes, 0);
        return 0;
    }

    private double ParseAsDouble(byte[] bytes)
    {
        if (bytes.Length >= 8)
            return BitConverter.ToDouble(bytes, 0);
        if (bytes.Length >= 4)
            return BitConverter.ToSingle(bytes, 0);
        return 0;
    }

    private bool ParseAsBool(byte[] bytes)
    {
        if (bytes.Length >= 1)
            return bytes[0] != 0;
        return false;
    }

    private string ParseAsHex(byte[] bytes)
    {
        if (bytes.Length > 100)
            return BitConverter.ToString(bytes, 0, 100).Replace("-", " ") + "...";
        return BitConverter.ToString(bytes).Replace("-", " ");
    }

    private int ReadInt32LittleEndian()
    {
        if (_position + 4 > _data.Length)
            return 0;
        
        int value = BitConverter.ToInt32(_data, _position);
        _position += 4;
        return value;
    }

    private int ReadVarint32()
    {
        int result = 0;
        int shift = 0;
        
        while (_position < _data.Length)
        {
            byte b = _data[_position++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
            if (shift >= 32)
                throw new InvalidDataException("Varint too long");
        }
        
        return result;
    }

    private int ReadVarint32FromBytes(byte[] bytes, ref int pos)
    {
        int result = 0;
        int shift = 0;
        
        while (pos < bytes.Length)
        {
            byte b = bytes[pos++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
            if (shift >= 32)
                throw new InvalidDataException("Varint too long");
        }
        
        return result;
    }

    private long ReadVarint64FromBytes(byte[] bytes, ref int pos)
    {
        long result = 0;
        int shift = 0;
        
        while (pos < bytes.Length)
        {
            byte b = bytes[pos++];
            result |= (long)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
            if (shift >= 64)
                throw new InvalidDataException("Varint too long");
        }
        
        return result;
    }

    private bool IsValidUtf8String(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;
        
        // 检查是否包含替换字符（表示解码失败）
        return !str.Contains('\uFFFD');
    }

    private bool HasControlChars(string str)
    {
        foreach (char c in str)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                return true;
        }
        return false;
    }
}

