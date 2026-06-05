using System.Collections.Generic;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 编译时槽位分配器
/// </summary>
public sealed class VariableTableBuilder
{
    private readonly Dictionary<string, int> _localSlots = new();
    private readonly Dictionary<string, int> _captureSlots = new();
    private readonly Dictionary<string, int> _paramSlots = new();
    private readonly List<string> _globalNames = new();
    private readonly List<string> _builtinNames = new();

    public int LocalCount => _localSlots.Count;
    public int CaptureCount => _captureSlots.Count;
    public int GlobalCount => _globalNames.Count;
    public int BuiltinCount => _builtinNames.Count;

    /// <summary>分配局部变量槽位</summary>
    public int AllocLocal(string name, bool isParameter = false)
    {
        if (_localSlots.TryGetValue(name, out int slot))
            return slot;

        slot = _localSlots.Count;
        _localSlots[name] = slot;
        if (isParameter)
            _paramSlots[name] = slot;
        return slot;
    }

    /// <summary>分配捕获变量槽位</summary>
    public int AllocCapture(string name)
    {
        if (_captureSlots.TryGetValue(name, out int slot))
            return slot;

        slot = _captureSlots.Count;
        _captureSlots[name] = slot;
        return slot;
    }

    /// <summary>注册全局变量引用</summary>
    public int AllocGlobal(string name)
    {
        int globalSlot = GlobalSlotRegistry.Register(name);

        for (int i = 0; i < _globalNames.Count; i++)
        {
            if (_globalNames[i] == name)
                return i;
        }

        int index = _globalNames.Count;
        _globalNames.Add(name);
        return index;
    }

    /// <summary>注册内置函数引用</summary>
    public int AllocBuiltin(string name)
    {
        for (int i = 0; i < _builtinNames.Count; i++)
        {
            if (_builtinNames[i] == name)
                return i;
        }

        int index = _builtinNames.Count;
        _builtinNames.Add(name);
        return index;
    }

    /// <summary>查找局部变量槽位</summary>
    public bool TryGetLocalSlot(string name, out int slot)
        => _localSlots.TryGetValue(name, out slot);

    /// <summary>查找捕获变量槽位</summary>
    public bool TryGetCaptureSlot(string name, out int slot)
        => _captureSlots.TryGetValue(name, out slot);

    /// <summary>
    /// 移除指定名称的局部变量槽位（用于 for 循环迭代间变量复用）
    /// </summary>
    public void RemoveLocal(string name)
    {
        _localSlots.Remove(name);
    }

    /// <summary>构建最终的 VariableTable</summary>
    public VariableTable Build()
    {
        // 去重：捕获变量优先，从局部映射中移除（被 ReplaceBindingInScope 移到捕获区的变量）
        var localNames = new Dictionary<string, int>(_localSlots);
        foreach (var key in _captureSlots.Keys)
        {
            localNames.Remove(key);
        }
        return new VariableTable
        {
            LocalCount = _localSlots.Count,  // 保持原始计数（未使用的局部槽位安全浪费）
            CaptureCount = CaptureCount,
            GlobalCount = GlobalCount,
            BuiltinCount = BuiltinCount,
            ParamSlots = new Dictionary<string, int>(_paramSlots),
            GlobalNames = _globalNames.ToArray(),
            BuiltinNames = _builtinNames.ToArray(),
            LocalNames = localNames,
            CaptureNames = new Dictionary<string, int>(_captureSlots)
        };
        /*return new VariableTable
        {
            LocalCount = LocalCount,
            CaptureCount = CaptureCount,
            GlobalCount = GlobalCount,
            BuiltinCount = BuiltinCount,
            ParamSlots = new Dictionary<string, int>(_paramSlots),
            GlobalNames = _globalNames.ToArray(),
            BuiltinNames = _builtinNames.ToArray(),
            LocalNames = new Dictionary<string, int>(_localSlots),
            CaptureNames = new Dictionary<string, int>(_captureSlots)
        };*/
    }
}