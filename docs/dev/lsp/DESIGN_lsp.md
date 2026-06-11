# SereinScript LSP Server — 架构设计

## 1. 语言分析

### 1.1 语法结构

SereinScript 是**基于表达式的语言**（Everything is an Expression），采用 Pratt Parser（优先级 0-10）。

```
关键字: let, var, if/then/else, when, for/in, return, import/from
运算符: + - * / %  |  == != < <= > >=  |  && ||  |  ! -  |  =  |  ?.
分隔符: ( ) { } [ ] , . : ;  =>
```

**BNF 概要**：
```
program      → declaration*
declaration  → letDecl | varDecl | importStmt | returnStmt | expr
letDecl      → "let" IDENT "=" expr
varDecl      → "var" IDENT "=" expr
importStmt   → "import" "{" member ("," member)* "}" "from" STRING
lambda       → "(" param* ")" "=>" expr
block        → "{" statement* "}"
ifExpr       → "if" expr "then" expr "else" expr
whenExpr     → "when" expr "{" (pattern "=>" body)+ "}"
forExpr      → "for" IDENT "in" expr expr
arrayLit     → "[" expr ("," expr)* "]"
objectLit    → "{" key "=" expr ("," key "=" expr)* "}"
```

### 1.2 AST 结构（22 种节点）

所有节点是 `record` 类型，继承 `Expr(SourceSpan)`。每个节点携带精确的源码位置。

| 类别 | AST 节点 | 说明 |
|------|----------|------|
| 顶层 | `ProgramExpr` | 语句列表 |
| 声明 | `LetExpr`, `VarExpr`, `AssignExpr` | 变量声明与赋值 |
| 模块 | `ImportStmt` | import { member } from "path" |
| 字面量 | `LiteralExpr`, `IdentifierExpr` | 常量和标识符引用 |
| 运算符 | `BinaryExpr`, `UnaryExpr`, `ConditionalExpr` | 二元/一元/三元运算 |
| 控制流 | `IfExpr`, `WhenExpr`, `ForExpr`, `ReturnExpr` | 条件/模式匹配/循环/返回 |
| 函数 | `LambdaExpr`, `CallExpr`, `BlockExpr` | Lambda/调用/代码块 |
| 数据结构 | `ArrayLiteralExpr`, `ObjectLiteralExpr` | 数组/对象字面量 |
| 成员访问 | `MemberAccessExpr`, `MemberAssignExpr`, `IndexAccessExpr`, `IndexAssignExpr` | 属性/索引读写 |
| 错误 | `ErrorExpr` | 解析错误占位 |

### 1.3 符号系统

| 符号类型 | 声明方式 | 作用域 | 可变性 |
|----------|----------|--------|--------|
| LetVariable | `let x = value` | 当前块 | 不可变 |
| VarVariable | `var x = value` | 当前块 | 可变 |
| Parameter | `(x) => body` | Lambda 体 | 不可变 |
| ImportSymbol | `import { x } from "path"` | 全局 | 不可变 |
| BuiltinFunction | 内置（print, len 等） | 全局 | 不可变 |

### 1.4 类型系统

**动态类型**：编译时无类型标注，LSP 不做类型推导。符号补全基于名称匹配。

### 1.5 作用域规则

```
全局作用域 (import + builtin)
  └── 块作用域 { let x = 1; { let y = 2; x + y } }
        └── Lambda 作用域 (x) => { x + 1 }
              └── 嵌套块/Lambda...
```

- `BlockExpr` 创建新作用域
- `LambdaExpr` 创建新作用域（参数绑定 + 闭包捕获）
- `ForExpr` 创建新作用域（循环变量绑定）
- 作用域查找：当前 → 父作用域 → 全局

## 2. LSP 总体架构

```
┌─────────────────────────────────────┐
│          VSCode Extension           │
│  (sereinscript-lsp client)          │
└──────────────┬──────────────────────┘
               │ LSP (stdin/stdout or TCP)
┌──────────────▼──────────────────────┐
│       ScriptLang.Lsp (Server)       │
│  ┌──────────────────────────────┐   │
│  │     LanguageServer           │   │
│  │  (OmniSharp LSP Host)        │   │
│  │                              │   │
│  │  Handlers:                   │   │
│  │  ┌──────────────────────┐    │   │
│  │  │ CompletionHandler    │    │   │
│  │  │ HoverHandler         │    │   │
│  │  │ DefinitionHandler    │    │   │
│  │  │ ReferencesHandler    │    │   │
│  │  │ DocumentSymbolHandler│    │   │
│  │  └──────────────────────┘    │   │
│  └──────────────────────────────┘   │
│  ┌──────────────────────────────┐   │
│  │       Workspace              │   │
│  │  ┌──────────┐ ┌────────────┐ │   │
│  │  │Document  │ │Document    │ │   │
│  │  │(URI → AST│ │(URI → AST  │ │   │
│  │  │ +Symbols)│ │ +Symbols)  │ │   │
│  │  └──────────┘ └────────────┘ │   │
│  └──────────────────────────────┘   │
│  ┌──────────────────────────────┐   │
│  │     Analysis                 │   │
│  │  SymbolTable, ScopeResolver  │   │
│  └──────────────────────────────┘   │
└─────────────────────────────────────┘
```

### 2.1 项目结构

```
ScriptLang.Lsp/
├── ScriptLang.Lsp.csproj
├── Program.cs
├── LanguageServer.cs
├── Workspace/
│   ├── WorkspaceManager.cs
│   └── DocumentInfo.cs
├── Analysis/
│   ├── SymbolKind.cs
│   ├── SymbolInfo.cs
│   ├── SymbolTable.cs
│   └── ScopeResolver.cs
├── Handlers/
│   ├── CompletionHandler.cs
│   ├── HoverHandler.cs
│   ├── DefinitionHandler.cs
│   ├── ReferencesHandler.cs
│   └── DocumentSymbolHandler.cs
└── Utilities/
    └── PositionMapper.cs
```

## 3. SymbolTable 设计

```
SymbolTable (per document)
├── RootScope (global symbols)
│   ├── Symbol: "file"  | ImportSymbol  | SourceSpan
│   ├── Symbol: "print" | BuiltinSymbol | SourceSpan
│   └── Symbol: "x"     | LetSymbol     | SourceSpan
│
└── Scope tree (nested via BlockExpr/LambdaExpr)
    ├── BlockScope { line 5-8 }
    │   ├── Symbol: "y" | VarSymbol | SourceSpan
    │   └── Symbol: "z" | Parameter | SourceSpan
    │
    └── LambdaScope { line 7 }
        └── Symbol: "n" | Parameter | SourceSpan
```

- 每个 `SymbolInfo` 包含：Name, Kind, SourceSpan, ParentScope
- 每个 `Scope` 包含：Parent, Children, Symbols (Dictionary)
- `SymbolTable` 提供：`Lookup(name, scope)` — 沿作用域链向上查找

## 4. Workspace 设计

```
WorkspaceManager
├── _documents: ConcurrentDictionary<DocumentUri, DocumentInfo>
│
├── OpenDocument(uri, text)    → lex + parse → AST + Symbols
├── UpdateDocument(uri, text)  → re-parse → AST + Symbols
├── CloseDocument(uri)         → remove
├── GetDocument(uri)           → DocumentInfo?
│
└── DocumentInfo
    ├── Uri, Text, Version
    ├── Tokens: List<Token>
    ├── Ast: ProgramExpr?
    └── Symbols: SymbolTable?
```

## 5. Incremental Parse 设计

采用**全量重解析**策略（Simple，适用文件 < 10K 行）：

```
Document Change → full re-lex → full re-parse → rebuild symbols
```

理由：
- AST 节点是 `record`（不可变），重建成本低
- 脚本文件通常较小（< 5K 行）
- 避免增量解析的复杂性（状态管理、失效追踪）
- 2026 年的硬件性能足以在 1ms 内完成千行脚本的全量解析

## 6. 模块实现顺序

按依赖关系：

```
T1: ScriptLang.Lsp.csproj + Program.cs   (项目骨架)
T2: Utilities/PositionMapper.cs           (位置转换)
T3: Analysis/SymbolKind.cs + SymbolInfo.cs (符号类型定义)
T4: Analysis/ScopeResolver.cs             (作用域遍历)
T5: Analysis/SymbolTable.cs               (符号表构建)
T6: Workspace/DocumentInfo.cs             (文档信息)
T7: Workspace/WorkspaceManager.cs         (文档管理)
T8: LanguageServer.cs                     (服务器骨架 + 处理器注册)
T9: Handlers/CompletionHandler.cs         (代码补全)
T10: Handlers/HoverHandler.cs            (悬停信息)
T11: Handlers/DefinitionHandler.cs       (跳转定义)
T12: Handlers/ReferencesHandler.cs       (查找引用)
T13: Handlers/DocumentSymbolHandler.cs   (文档符号)
```

每个模块可独立编译，T9-T13 按用户要求逐个生成。
