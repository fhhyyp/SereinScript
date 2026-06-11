# 验收报告：字节码持久化

## 测试结果

| 测试脚本 | 类型 | 指令数 | .ssc 大小 | 结果 |
|----------|------|--------|-----------|------|
| 1.1-基础运算 | 基础运算 | 32 | 293 bytes | ✅ 通过 |
| 1.2-变量声明 | 变量/赋值 | 12 | ~180 bytes | ✅ 通过 |
| 1.4-函数 | 函数定义/调用 | ~30 | ~300 bytes | ✅ 通过 |
| 3.1-闭包 | 嵌套闭包 | 182 | 3594 bytes | ✅ 通过 |
| 3.3-递归 | 递归函数 | ~40 | ~500 bytes | ✅ 通过 |

## 测试覆盖

- ✅ 基础运算（字面量、算术）
- ✅ 变量声明与赋值（let/var、原地运算优化）
- ✅ 函数定义与调用
- ✅ 闭包（捕获变量、嵌套闭包、多级柯里化）
- ✅ 递归（自引用、相互递归）
- ✅ 内置函数（print、字符串方法）
- ✅ 字符串操作
- ✅ 条件表达式（if/when）
- ✅ 循环（for...in）

## API 验证

- ✅ `ByteCodeChunk.Save(chunk, path)` — 保存到文件
- ✅ `ByteCodeChunk.Save(chunk, stream)` — 保存到流
- ✅ `ByteCodeChunk.Load(path)` — 从文件加载
- ✅ `ByteCodeChunk.Load(stream)` — 从流加载
- ✅ `ScriptEngine.CreateTask(chunk)` — 从已加载 Chunk 执行

## Demo CLI 命令

```
ScriptLang.Demo <script>            直接执行脚本
ScriptLang.Demo --compare <script>  编译执行 vs 编译→保存→加载→执行 对比
ScriptLang.Demo --save <script>     编译并保存为 .ssc 文件
ScriptLang.Demo --load <ssc>        加载 .ssc 文件并执行
```

---

> 创建时间: 2026-06-06
