using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 跨模块全局变量槽位注册表（编译时 + 运行时共享）
/// 编译时注册变量名并分配唯一槽位，运行时提供值数组给 VM
/// </summary>
public static class GlobalSlotRegistry
{
    // 变量名 → 槽位索引
    private static readonly Dictionary<string, int> _nameToSlot = [];

    // 槽位索引 → 变量名（序列化用）
    private static readonly List<string> _slotNames = [];

    // 运行时值数组（槽位索引 → 值）
    private static Value[] _values = [];

    /// <summary>全局变量总数</summary>
    public static int Count => _slotNames.Count;

    /// <summary>获取所有变量名（按槽位顺序，序列化用）</summary>
    public static IReadOnlyList<string> GetNames() => _slotNames.AsReadOnly();

    /// <summary>
    /// 注册全局变量（编译时调用）
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>分配的槽位索引</returns>
    public static int Register(string name)
    {
        if (_nameToSlot.TryGetValue(name, out int existingSlot))
            return existingSlot;

        int slot = _slotNames.Count;
        _nameToSlot[name] = slot;
        _slotNames.Add(name);
        return slot;
    }

    /// <summary>
    /// 获取变量的槽位索引
    /// </summary>
    public static int GetSlot(string name)
    {
        return _nameToSlot.TryGetValue(name, out int slot)
            ? slot
            : throw new KeyNotFoundException($"全局变量 '{name}' 未注册");
    }

    /// <summary>
    /// 检查变量是否已注册
    /// </summary>
    public static bool IsRegistered(string name) => _nameToSlot.ContainsKey(name);

    /// <summary>
    /// 初始化运行时值数组（VM 启动时调用）
    /// </summary>
    public static void InitializeValues()
    {
        _values = new Value[Count];
        for (int i = 0; i < _values.Length; i++)
            _values[i] = Value.Null;
    }

    /// <summary>
    /// 获取运行时值数组的引用（VM 直接访问，避免拷贝）
    /// </summary>
    public static Value[] GetValues()
    {
        if (_values.Length != Count)
            InitializeValues();
        return _values;
    }

    /// <summary>
    /// 设置全局变量的运行时值
    /// </summary>
    public static void SetValue(int slot, Value value)
    {
        if ((uint)slot >= (uint)_values.Length)
            InitializeValues();
        _values[slot] = value;
    }

    /// <summary>
    /// 获取全局变量的运行时值
    /// </summary>
    public static Value GetValue(int slot)
    {
        return (uint)slot < (uint)_values.Length
            ? _values[slot]
            : Value.Null;
    }

    /// <summary>
    /// 重置（引擎重启时调用）
    /// </summary>
    public static void Reset()
    {
        _nameToSlot.Clear();
        _slotNames.Clear();
        _values = [];
    }
}
