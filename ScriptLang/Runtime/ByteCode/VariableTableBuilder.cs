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

    /// <summary>构建最终的 VariableTable</summary>
    public VariableTable Build()
    {
        return new VariableTable
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
        };
    }
}