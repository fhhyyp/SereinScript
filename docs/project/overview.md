# 项目介绍

## SereinScript 是什么？

**SereinScript** 是一门基于 .NET (CLR) 平台的**表达式驱动**动态脚本语言。它的设计目标是为 .NET 应用程序提供轻量、灵活、可嵌入的脚本能力，让用户能够在不重新编译宿主程序的情况下，通过脚本来控制和扩展应用行为。

## 设计理念

SereinScript 的核心理念是「**一切皆为表达式**」（Expression-Oriented）。在这门语言中，不存在传统的「语句」与「表达式」二分 —— `if`、`for`、`when` 等控制流结构都是表达式，它们会求值为一个结果。这使得代码可以非常自然地组合与嵌套。

## 核心特性

| 特性 | 说明 |
|------|------|
| **表达式驱动** | 控制流（if/for/when）均为表达式，可直接参与计算 |
| **Lambda 函数** | 支持箭头函数 `(x, y) => x + y`，一等公民 |
| **闭包** | 自动捕获自由变量，支持高阶函数 |
| **不可变/可变变量** | `let` 声明不可变，`var` 声明可变 |
| **模式匹配** | `when` 表达式支持多条件分支匹配 |
| **模块系统** | `import { member } from "path"` 导入机制 |
| **CLR 互操作** | 无缝调用 .NET 对象的方法和属性 |
| **字节码编译** | 源码编译为字节码，支持编译缓存与持久化（`.ssc`） |
| **异步支持** | 原生 `await` 异步调用 CLR 的 `Task`/`Task<T>` |
| **源码生成器** | Roslyn 增量生成器，编译期自动生成原型绑定代码 |
| **LSP 服务器** | 提供代码补全、悬停提示、跳转定义、查找引用等 IDE 功能 |

## 技术栈

- **运行时**: .NET 10.0
- **语言**: C#（Latest）
- **生成器**: Roslyn Incremental Source Generator（`netstandard2.0`）
- **LSP**: OmniSharp.Extensions.LanguageServer
- **编辑器**: VS Code 扩展

## 与其他方案对比

| 方面 | SereinScript | Lua + NLua | IronPython | 
|------|-------------|------------|------------|
| **运行时体积** | 轻量（仅依赖 .NET） | 需捆绑 Lua 原生库 | 需 IronPython 程序集 |
| **CLR 互操作** | 深度集成，零摩擦 | 需手动封装 | 良好但较重 |
| **编译期检查** | Roslyn 源码生成器 | 无 | 无 |
| **IDE 支持** | 自带 LSP 服务器 | 依赖第三方 | 依赖 PTVS |
| **学习成本** | JavaScript-like，低 | Lua 语法需学习 | Python 语法，中等 |
| **异步支持** | 原生支持 async/await | 需手动处理 | 有限支持 |

## 适用场景

- **游戏脚本系统**：轻量高性能，适合嵌入游戏引擎
- **自动化运维**：通过脚本化来编排运维流程
- **插件系统**：允许用户通过脚本扩展应用功能
- **配置即代码**：复杂的业务规则可以通过脚本表达
- **教育用途**：编译器/解释器学习的完整参考实现

## 项目仓库结构

```
SereinScript/
├── ScriptLang/                # 核心库（词法、语法、运行时、字节码VM）
│   ├── Lexer/                 #   词法分析器
│   ├── Parser/                #   语法分析器（Pratt Parser）
│   ├── Runtime/               #   运行时（Value系统、作用域）
│   │   └── ByteCode/          #   字节码编译器、VM、序列化
│   ├── Prototype/             #   原型系统（Array/String/Object扩展）
│   └── System/                #   内置系统模块
├── ScriptLang.Generator/      # Roslyn 源码生成器
│   ├── Models/                #   缓存模型
│   ├── Extensions/            #   扩展方法
│   └── ScriptPrototypeToolkits/ # 原型生成逻辑
├── ScriptLang.Lsp/            # LSP 语言服务器
│   ├── Handlers/              #   补全/悬停/定义/引用/符号处理器
│   ├── Analysis/              #   语义分析（作用域、符号表）
│   ├── Workspace/             #   工作区文档管理
│   └── lsp/                   #   VS Code 扩展打包目录
├── ScriptLang.Demo/           # CLI 演示项目
│   └── Samples/               #   示例脚本集
├── ScriptAvaloniaApp/         # Avalonia 桌面应用
└── docs/                      # 文档
    ├── project/               #   项目介绍、架构设计
    ├── guide/                 #   使用指南、开发指南
    ├── lsp/                   #   LSP 设计文档
    └── bytecode-persistence/  #   字节码持久化设计文档
```

## 许可证与社区

本项目为开源项目。详细信息请参见仓库根目录的 LICENSE 文件（如有）。

欢迎通过 Issue 和 Pull Request 参与贡献！
