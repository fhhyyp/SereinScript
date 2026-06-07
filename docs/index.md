# SereinScript 文档

欢迎查阅 SereinScript 文档。本文档目录面向不同角色的读者组织。

---

## 📖 文档导航

### 项目总览

| 文档 | 说明 | 适合读者 |
|------|------|---------|
| [项目介绍](project/overview.md) | 项目背景、特性、技术栈、适用场景 | 所有人 |
| [项目架构](project/architecture.md) | 整体架构、模块详解、数据流 | 开发者、架构师 |

### 使用指南

| 文档 | 说明 | 适合读者 |
|------|------|---------|
| [如何使用](guide/getting-started.md) | 安装、配置、嵌入、命令行参考 | 使用者、集成开发者 |
| [语言参考手册](SereinScript-Language-Reference.md) | 完整语法、内置函数、CLR 互操作 | 脚本编写者 |

### 开发指南

| 文档 | 说明 | 适合读者 |
|------|------|---------|
| [如何二次开发](guide/development.md) | 环境搭建、扩展指南、调试、实践 | 贡献者、二次开发者 |

### 技术设计文档

| 文档 | 说明 |
|------|------|
| [词法分析器设计](dev-lexer.md) | Lexer 实现细节与扩展方式 |
| [语法分析器设计](dev-parser.md) | Pratt Parser 架构与优先级系统 |
| [字节码编译器设计](dev-compiler.md) | AST → 字节码编译流程 |
| [虚拟机设计](dev-vm.md) | 基于栈的字节码 VM |
| [运行时环境设计](dev-runtime-environment.md) | 值类型系统、作用域、原型 |
| [系统模块](system-modules.md) | 内置系统模块接口规范 |
| [LSP 服务器设计](lsp/DESIGN_lsp.md) | 语言服务器架构与功能 |
| [字节码持久化](bytecode-persistence/) | `.ssc` 格式设计与序列化 |

---

## 🎯 快速定位

### 我想...

| 需求 | 请看 |
|------|------|
| 了解这是什么项目 | [项目介绍](project/overview.md) |
| 了解架构如何设计 | [项目架构](project/architecture.md) |
| 运行第一个脚本 | [如何使用 → 快速开始](guide/getting-started.md#快速开始) |
| 在 .NET 项目中嵌入脚本 | [如何使用 → 编程方式嵌入](guide/getting-started.md#编程方式嵌入) |
| 学习脚本语法 | [语言参考手册](SereinScript-Language-Reference.md) |
| 搭建开发环境 | [如何二次开发 → 开发环境搭建](guide/development.md#开发环境搭建) |
| 给脚本语言添加新语法 | [如何二次开发 → 场景一](guide/development.md#场景一添加新的语法结构) |
| 添加新的内置函数 | [如何二次开发 → 场景二](guide/development.md#场景二添加新的内置函数) |
| 添加新的系统模块 | [如何二次开发 → 场景三](guide/development.md#场景三添加新的系统模块) |
| 给类型添加原型方法 | [如何二次开发 → 场景四](guide/development.md#场景四为值类型添加原型方法) |
| 扩展 LSP 功能 | [如何二次开发 → 场景五](guide/development.md#场景五扩展-lsp-功能) |
| 调试脚本执行或 VM | [如何二次开发 → 调试指南](guide/development.md#调试指南) |
| 了解字节码格式 | [字节码持久化](bytecode-persistence/) |
| 查阅系统模块 API | [系统模块](system-modules.md) |
