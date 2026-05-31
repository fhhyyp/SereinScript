# VSCode 扩展编译指南

本文档详细记录了编译和打包 SereinScript VSCode 扩展的完整步骤。

## 准备工作

1. **确保项目结构完整**
   - SereinScript.LSP 项目已构建
   - vscode-extension 目录结构已创建

2. **安装必要的依赖**
   - Node.js 和 npm
   - TypeScript
   - VSCE (VSCode Extension Manager)

## 编译步骤

### 1. 构建 LSP 服务器

首先需要构建 C# LSP 服务器，确保所有依赖项都已正确编译。

```powershell
# 在 SereinScript.LSP 目录下执行
dotnet build
```

### 2. 创建并配置 server 文件夹

将编译后的 LSP 服务器文件复制到 vscode-extension 目录下的 server 文件夹中。

```powershell
# 创建 server 文件夹
mkdir -p server

# 复制编译后的文件
Copy-Item -Path "..\bin\Debug\net10.0\*" -Destination ".\server" -Recurse -Force
```

### 3. 安装 VSCode 扩展依赖

在 vscode-extension 目录下安装必要的 npm 依赖。

```powershell
# 安装基本依赖
npm install

# 安装 Node.js 类型定义
npm install --save-dev @types/node
```

### 4. 安装 VSCE 工具

VSCE (VSCode Extension Manager) 用于打包扩展为 VSIX 文件。

```powershell
# 全局安装 @vscode/vsce (vsce 的新名称)
npm install -g @vscode/vsce --force
```

### 5. 编译 TypeScript 代码

编译 extension.ts 文件为 JavaScript。

```powershell
# 编译 TypeScript 代码
npm run compile

# 或者直接使用 tsc 命令
npx tsc -p ./
```

### 6. 检查编译输出

确保 out 目录已生成 extension.js 文件。

```powershell
# 检查 out 目录
ls out
```

### 7. 配置 .vscodeignore 文件

确保 .vscodeignore 文件不包含 out 目录，否则打包时会忽略编译后的文件。

```powershell
# 查看 .vscodeignore 文件内容
cat .vscodeignore

# 确保不包含 out 目录
```

### 8. 打包扩展

使用 VSCE 工具将扩展打包为 VSIX 文件。

```powershell
# 打包扩展
npx @vscode/vsce package

# 或者使用全局安装的 vsce
vsce package
```

### 9. 验证打包结果

打包成功后，会在当前目录生成一个 .vsix 文件，例如 `sereinscript-vscode-0.1.0.vsix`。

```powershell
# 查看生成的 VSIX 文件
ls *.vsix
```

## 常见问题及解决方案

### 1. 找不到模块 'path' 或其对应的类型声明

**解决方案**：安装 Node.js 类型定义

```powershell
npm install --save-dev @types/node
```

### 2. 找不到名称 'process'

**解决方案**：同样是因为缺少 Node.js 类型定义，执行上述命令即可。

### 3. Extension entrypoint(s) missing

**解决方案**：确保 out/extension.js 文件存在，并且 .vscodeignore 文件没有忽略 out 目录。

### 4. vsce 命令未找到

**解决方案**：使用 npx 运行 vsce 或安装全局的 @vscode/vsce。

```powershell
# 使用 npx
npx vsce package

# 或使用新的包名
npx @vscode/vsce package
```

### 5. A 'repository' field is missing from the 'package.json' manifest file

**解决方案**：这是一个警告，可以选择忽略并继续。在运行 vsce package 时输入 'y' 继续。

## 目录结构

编译完成后，vscode-extension 目录结构应该如下：

```
vscode-extension/
├── server/              # LSP 服务器文件
├── out/                 # 编译后的 TypeScript 代码
│   ├── extension.js     # 扩展入口文件
│   └── extension.js.map # 源码映射文件
├── src/                 # TypeScript 源代码
│   └── extension.ts     # 扩展主文件
├── syntaxes/            # 语法高亮文件
│   └── sereinscript.tmLanguage.json
├── .vscodeignore        # 忽略文件配置
├── package.json         # 扩展配置文件
├── tsconfig.json        # TypeScript 配置文件
└── sereinscript-vscode-0.1.0.vsix # 生成的 VSIX 文件
```

## 安装扩展

在 VSCode 中，可以通过以下方式安装生成的 VSIX 文件：

1. 打开 VSCode
2. 点击左侧边栏的扩展图标
3. 点击扩展视图右上角的三个点图标
4. 选择 "从 VSIX 安装..."
5. 选择生成的 .vsix 文件
6. 安装完成后重启 VSCode

## 总结

通过以上步骤，你可以成功编译和打包 SereinScript VSCode 扩展，为 SereinScript DSL 提供完整的 LSP 支持，包括语法检查、自动补全、文档符号和跳转定义等功能。
