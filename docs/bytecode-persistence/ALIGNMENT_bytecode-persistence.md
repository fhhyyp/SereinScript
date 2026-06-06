# 需求对齐：编译后字节码持久化

## 1. 原始需求

为 ScriptLang 项目实现"编译后持久化保存"功能：

1. 将编译后的 `ByteCodeChunk` 序列化到文件（二进制格式）
2. 从文件反序列化回 `ByteCodeChunk`，并能被 VM 正确执行
3. 支持跨版本兼容（字段增删时的迁移策略）
4. 考虑性能：序列化大小、速度、加载效率

最终目标：
1. 编译产物可直接运行，无需再次编译
2. 编译调试脚本输出内容，与执行编译文件输出内容保持一致

---

## 2. 项目理解

### 2.1 当前架构

```
Source Code → Lexer → Parser → AST → Compiler → ByteCodeChunk → VM → Value
                                                    ↑
                                              本次新增：持久化层
                                          (序列化到文件 / 反序列化加载)
```

### 2.2 需要序列化的核心组件

#### ByteCodeChunk（字节码块）
- `List<Instruction> Code` — 指令列表
- `VariableTable? VariableTable` — 变量槽位布局
- `List<object?> _constants` — 常量表（含紧凑编码）
- `List<ByteCodeChunk> _closures` — 嵌套闭包块（递归结构）
- `Dictionary<object, int> _constantMap` — ❌ 不需要（运行时去重索引，可重建）

#### Instruction（指令）
```csharp
public sealed record Instruction(OpCode OpCode, object? Operand = null);
```
- `OpCode` 是 `byte` 枚举，直接可序列化
- `Operand` 类型：
  - `int` — 槽位索引、常量索引、参数数量、跳转目标 IP
  - `null` — 无操作数指令 (Nop, Pop, Return 等)
  - `(int, List<string>, List<(string, int)>)` — CreateClosure 元组

#### VariableTable（变量表）
- 计数：`LocalCount`, `CaptureCount`, `GlobalCount`, `BuiltinCount`
- 映射：`ParamSlots` (Dictionary), `LocalNames` (Dictionary), `CaptureNames` (Dictionary)
- 数组：`GlobalNames` (string[]), `BuiltinNames` (string[])

#### 常量表中的数据类型
| 类型 | 说明 | 可序列化 |
|------|------|----------|
| `null` | 紧凑编码索引 0 | ✅ |
| `bool` | 紧凑编码索引 1-2 | ✅ |
| `int` (-127..128) | 紧凑编码索引 3-259 | ✅ |
| `string` | 属性名、成员名等 | ✅ |
| `long`, `float`, `double`, `decimal` | 数值字面量 | ✅ |
| `List<object?>` | Import 数据 [filePath, member, alias, ...] | ✅ |
| CLR 对象引用 | ❌ 不出现在常量表中 | N/A |

### 2.3 不需要序列化的组件（运行时状态）
- `VM` 内部状态（操作数栈、帧栈、迭代器栈）
- `CallFrame` 实例
- `GlobalSlotRegistry` 运行时值（但注册表映射需要重建）
- `Scope` 实例
- `ScriptEngine` 实例

### 2.4 现有序列化基础设施
- `VariableTable` 已标记 `[Serializable]`
- `ByteCodeChunk` 已有内部反序列化构造函数：
  ```csharp
  internal ByteCodeChunk(
      List<Instruction> code,
      List<object?> constants,
      List<ByteCodeChunk> closures,
      VariableTable? variableTable)
  ```
- `ByteCodeChunk` 已有只读访问器：`Constants`, `Closures`

---

## 3. 任务边界

### 范围内
1. `ByteCodeChunk` 的二进制序列化/反序列化
2. `VariableTable` 的序列化/反序列化
3. `Instruction` 的序列化/反序列化（含 CreateClosure 元组）
4. 嵌套闭包的递归序列化
5. 文件格式定义（魔术数字 + 版本号 + 数据段）
6. 反序列化后与运行时重新关联（GlobalSlotRegistry、BuiltinFunctions）
7. ScriptEngine 的 "加载编译文件并执行" API
8. 脚本编译并保存到文件的 CLI / API
9. 单元测试验证

### 范围外
- 增量编译缓存
- 热更新/热重载
- 加密/混淆
- 网络传输优化

---

## 4. 风险与假设

### 风险
| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| CreateClosure 操作数的元组序列化 | 闭包功能异常 | 自定义元组序列化格式，含参数列表和捕获映射 |
| 常量表跨版本类型变化 | 反序列化失败 | 版本号 + 类型标记 |
| GlobalSlotRegistry 重建不完整 | 全局变量访问异常 | 序列化 VariableTable 中的 GlobalNames，加载时重新注册 |
| 嵌套闭包深递归 | 栈溢出 | 使用迭代而非递归序列化，或限制嵌套深度 |

### 假设
1. 序列化发生在同一平台（Little Endian，与 .NET 一致）
2. 常量表中的对象类型限于上述类型
3. `Instruction` 操作数类型不会出现新的复杂类型
4. 编译产物文件大小 < 100MB（单文件）

---

## 5. 关键设计问题

### Q1: 序列化格式选择？

候选方案：
- **自定义二进制格式**：最小体积，最快速度，完全控制
- **MessagePack**：第三方依赖，格式标准，有 Schema 支持
- **Protobuf**：需要 .proto 定义，引入代码生成复杂度

**推荐：自定义二进制格式**。理由：
1. 零外部依赖
2. Instruction 操作数包含 `object?`，通用序列化器无法直接处理元组
3. 常量表有紧凑编码特性，自定义格式可以利用此特性减小体积
4. 字节码格式相对固定，不需要通用序列化器的灵活性

### Q2: 常量如何处理？
- 紧凑编码的 null/bool/int 不需要显式存储在常量段
- 只序列化动态常量列表（`_constants`）
- 加载时重建 `_constantMap` 去重索引

### Q3: 闭包中的 ByteCodeChunk 如何序列化？
- 嵌套闭包块递归序列化，作为子段嵌入
- `CreateClosure` 指令的操作数中包含 `chunkIndex`，这是相对于父 Chunk 的闭包列表索引
- 序列化时保持索引关系不变即可

### Q4: CreateClosure 的元组操作数如何序列化？
- 操作数类型：`(int chunkIndex, List<string> parameters, List<(string name, int outerCaptureSlot)> captureMappings)`
- 需要自定义序列化：写 chunkIndex、参数数量+名称列表、捕获映射数量+每对 name/slot

### Q5: 反序列化后如何与运行时关联？
1. 读取 VariableTable 的 `GlobalNames` → 重新注册到 `GlobalSlotRegistry`
2. 读取 VariableTable 的 `BuiltinNames` → VM 静态初始化已有对应的 `_builtinSlots` 和 `_builtinValues`
3. 无需额外操作——VM 的 `InitFrameSlots` 已经处理这些关联

---

## 6. 待确认问题

1. **文件扩展名**：建议使用 `.ssc` (SereinScript Compiled)？
2. **是否需要在 ScriptEngine 中添加 `LoadCompiled(string path)` API？**
3. **是否需要在 Demo 项目中添加编译保存的命令？**
4. **版本号策略**：主版本不兼容 + 次版本兼容？

---

> 创建时间: 2026-06-06 | 状态: 待确认
