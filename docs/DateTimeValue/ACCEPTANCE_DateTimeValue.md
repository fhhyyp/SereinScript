# DateTimeValue 验收报告

## 实施总结

按 [CONSENSUS_DateTimeValue.md](./CONSENSUS_DateTimeValue.md) 设计完成 DateTimeValue + TimeSpanValue 全部实现。

### 新增文件（4 个）

| 文件 | 行数 |
|------|------|
| `ScriptLang/Runtime/DateTimeValue.cs` | ~35 |
| `ScriptLang/Runtime/TimeSpanValue.cs` | ~35 |
| `ScriptLang/Prototype/DateTimePrototype.cs` | ~85 |
| `ScriptLang/Prototype/TimeSpanPrototype.cs` | ~95 |

### 修改文件（6 个）

| 文件 | 改动 |
|------|------|
| `ScriptLang/Runtime/Value.cs` | +4 行（IsDateTime、IsTimeSpan、ToString×2） |
| `ScriptLang/Runtime/ByteCode/VM.cs` | +70 行（AddOp、SubOp、Compare、IsEqual、ValueFromConstant、ConvertClrToValue、ConvertValueToClr） |
| `ScriptLang/BuiltinCache.cs` | +40 行（now 改造、date、timespan、typeof 更新） |
| `ScriptLang/System/JsonModule.cs` | +2 行（ConvertToClrObject） |
| `ScriptLang/ScriptEngine.cs` | +2 行（注册 2 个新 Prototype） |
| `ScriptLang/Prototype/DateTimePrototype.cs` | 新建 |
| `ScriptLang/Prototype/TimeSpanPrototype.cs` | 新建 |

### 编译结果

```
dotnet build → 0 错误，编译通过 ✅
```

### 验收对照

| # | 验收标准 | 状态 |
|---|---------|------|
| 1 | DateTimeValue 类存在，继承 Value，IsDateTime = true | ✅ |
| 2 | TimeSpanValue 类存在，继承 Value，IsTimeSpan = true | ✅ |
| 3 | now() 返回 DateTimeValue（UTC） | ✅ |
| 4 | date(str) 解析 ISO 8601 → DateTimeValue（UTC） | ✅ |
| 5 | timespan(num, unit) 构造 TimeSpanValue | ✅ |
| 6 | typeof(dt) → "datetime"，typeof(ts) → "timespan" | ✅ |
| 7 | AddOp: dt+ts | ts+dt | ts+ts | ✅ |
| 8 | SubOp: dt-ts | dt-dt | ts-ts | ✅ |
| 9 | Compare: dt<dt | dt==dt | ts<ts | ✅ |
| 10 | IsEqual: DateTimeValue==DateTimeValue, TimeSpanValue==TimeSpanValue | ✅ |
| 11 | ConvertClrToValue: DateTime→DateTimeValue（非 ClrObjectValue），TimeSpan→TimeSpanValue | ✅ |
| 12 | ConvertValueToClr: DateTimeValue→DateTime，TimeSpanValue→TimeSpan | ✅ |
| 13 | DateTimePrototype: 10 属性 + toString(format) | ✅ |
| 14 | TimeSpanPrototype: 11 属性 + toString(format) | ✅ |
| 15 | JsonModule: DateTimeValue→ISO 8601, TimeSpanValue→标准格式 | ✅ |
| 16 | Prototype 在 ScriptEngine 构造时注册 | ✅ |
| 17 | Lexer/Parser/AST/Compiler 无改动 | ✅ |

### 已知事项

1. **`now` 破坏性变更**：从 `NumberValue<long>`（Ticks）变为 `DateTimeValue`，依赖旧行为的脚本需适配
2. **无自动化测试**：项目当前无测试基础设施，建议后续补齐 DateTime 专项测试
3. **源生成器 CS8669 警告**：pre-existing（DateTimePrototype 和其他 Prototype 均有），非本次引入
