# SereinScript 系统模块文档

> 使用 `import { <模块名> } from "system"` 导入，命名风格为 JavaScript 风格（camelCase）

---

## 1. file — 文件系统模块

| 方法/属性 | 参数 | 返回值 | 说明 |
|-----------|------|--------|------|
| `file.read(path)` | `path: string` | `string` | 同步读取文件（UTF-8） |
| `file.readAsync(path)` | `path: string` | `Task<string>` | 异步读取文件（UTF-8） |
| `file.write(path, content)` | `path: string`, `content: string` | `void` | 同步写入文件（UTF-8） |
| `file.writeAsync(path, content)` | `path: string`, `content: string` | `Task` | 异步写入文件（UTF-8） |
| `file.readDir(path)` | `path: string` | `string[]` | 读取目录内容（文件和子目录路径数组） |
| `file.exists(path)` | `path: string` | `bool` | 判断文件或目录是否存在 |
| `file.mkDir(path)` | `path: string` | `void` | 创建目录（含中间目录） |
| `file.remove(path)` | `path: string` | `void` | 删除文件或目录（目录递归删除） |
| `file.stat(path)` | `path: string` | `object` | 获取文件信息：`size`/`created`/`modified`/`isDirectory`/`isFile` |
| `file.chDir(path)` | `path: string` | `void` | 更改当前工作目录 |
| `file.cwd` | — | `string` | 获取当前工作目录（属性） |

**示例**：
```js
import { file } from "system"

let content = file.read("D:\\data.txt")
file.write("D:\\output.txt", content)
print(file.cwd)
let info = file.stat("D:\\data.txt")
print(info.size, info.modified)
```

---

## 2. console — 控制台模块

| 方法/属性 | 参数 | 返回值 | 说明 |
|-----------|------|--------|------|
| `console.log(value)` | `value: any` | `void` | 输出到标准输出 |
| `console.error(value)` | `value: any` | `void` | 输出到标准错误 |
| `console.readLine()` | — | `Task<string>` | 从控制台异步读取一行 |
| `console.clear()` | — | `void` | 清空控制台 |
| `console.time(label)` | `label: string` | `void` | 开始计时 |
| `console.timeEnd(label)` | `label: string` | `void` | 结束计时并打印耗时 |
| `console.colors` | — | `object` | ANSI 颜色常量：`reset`/`red`/`green`/`yellow`/`blue`/`magenta`/`cyan`/`white` |

**示例**：
```js
import { console } from "system"

console.log("Hello World")
console.time("test")
// ... some code ...
console.timeEnd("test")  // 输出: test: 42ms
print(console.colors.red + "Red text" + console.colors.reset)
```

---

## 3. path — 路径处理模块

| 方法/属性 | 参数 | 返回值 | 说明 |
|-----------|------|--------|------|
| `path.join(paths)` | `paths: string[]` | `string` | 连接路径片段 |
| `path.resolve(path)` | `path: string` | `string` | 解析为绝对路径 |
| `path.dirname(path)` | `path: string` | `string` | 获取目录名 |
| `path.basename(path, ext?)` | `path: string`, `ext?: string` | `string` | 获取文件名（可选移除扩展名） |
| `path.extname(path)` | `path: string` | `string` | 获取扩展名（含 `.`） |
| `path.normalize(path)` | `path: string` | `string` | 规范化路径 |
| `path.separator` | — | `string` | 目录分隔符（`\` 或 `/`） |
| `path.delimiter` | — | `string` | 路径分隔符（`;` 或 `:`） |

**示例**：
```js
import { path } from "system"

let full = path.join(["D:", "data", "file.txt"])  // "D:\data\file.txt"
print(path.basename(full))          // "file.txt"
print(path.basename(full, ".txt"))  // "file"
print(path.extname(full))           // ".txt"
print(path.dirname(full))           // "D:\data"
```

---

## 4. json — JSON 处理模块

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `json.stringify(value, indent?)` | `value: any`, `indent?: bool` | `string` | 序列化为 JSON 字符串 |
| `json.parse(json)` | `json: string` | `any` | 解析 JSON 字符串为脚本值 |

**示例**：
```js
import { json } from "system"

let obj = { name = "test", value = 42 }
let str = json.stringify(obj, true)  // 带缩进
print(str)
let parsed = json.parse(str)
print(parsed.name, parsed.value)
```

---

## 5. network — 网络模块

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `network.httpGet(url, options)` | `url: string`, `options: object` | `Task<object>` | 发送 HTTP GET，返回 `{status, statusText, headers, data}` |
| `network.httpPost(url, data, contentType?)` | `url: string`, `data: any`, `contentType?: string` | `Task<object>` | 发送 HTTP POST，返回 `{status, statusText, data}` |

**示例**：
```js
import { network } from "system"

let res = network.httpGet("https://api.example.com/data", {
    headers = { Authorization = "Bearer token123" }
})
print(res.status, res.data)

let postRes = network.httpPost("https://api.example.com/submit", {
    name = "test",
    value = 42
})
print(postRes.status)
```

---

## 6. timer — 定时器模块

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `timer.sleep(ms)` | `ms: int` | `Task` | 异步延迟指定毫秒 |
| `timer.setTimeout(callback, ms)` | `callback: Function`, `ms: int` | `ClrObject` | 延迟执行回调（一次性） |
| `timer.setInterval(callback, ms)` | `callback: Function`, `ms: int` | `ClrObject` | 周期性执行回调 |
| `timer.clearTimer(timer)` | `timer: ClrObject` | `void` | 清除定时器 |

**示例**：
```js
import { timer } from "system"

timer.setTimeout(() => {
    print("延迟 1 秒后执行")
}, 1000)

let id = timer.setInterval(() => {
    print("每 500ms 执行")
}, 500)

timer.sleep(3000)
timer.clearTimer(id)  // 停止定时器
```

---

## 7. crypto — 加密模块

| 方法/属性 | 参数 | 返回值 | 说明 |
|-----------|------|--------|------|
| `crypto.hash(algorithm, data)` | `algorithm: string`, `data: string` | `string` | 计算哈希值，支持 `md5`/`sha1`/`sha256`/`sha512` |
| `crypto.hmac(algorithm, data, key)` | `algorithm: string`, `data: string`, `key: string` | `string` | 计算 HMAC |
| `crypto.randomBytes(length)` | `length: int` | `string` | 生成随机字节（Base64） |
| `crypto.randomString(length)` | `length: int` | `string` | 生成随机字符串 |
| `crypto.uuid()` | — | `string` | 生成 UUID v4 |
| `crypto.algorithms` | — | `object` | 支持的算法常量：`md5`/`sha1`/`sha256`/`sha512` |

**示例**：
```js
import { crypto } from "system"

print(crypto.hash("sha256", "hello"))
print(crypto.hmac("sha256", "message", "secret-key"))
print(crypto.uuid())           // "550e8400-e29b-41d4-a716-446655440000"
print(crypto.randomString(16)) // 16 位随机字符串
```

---

## 8. process — 进程模块

| 方法/属性 | 参数 | 返回值 | 说明 |
|-----------|------|--------|------|
| `process.argv` | — | `{args, execPath}` | 命令行参数（属性） |
| `process.pid` | — | `int` | 当前进程 ID（属性） |
| `process.cwd` | — | `string` | 当前工作目录（属性） |
| `process.chDir(path)` | `path: string` | `void` | 更改工作目录 |
| `process.env()` | — | `object` | 获取环境变量对象 |
| `process.execute(command)` | `command: string` | `Task<int>` | 执行命令，返回退出码 |
| `process.exit(code?)` | `code?: int` | `void` | 退出进程（默认 0） |
| `process.uptime` | — | `double` | 进程运行秒数（属性） |

**示例**：
```js
import { process } from "system"

print("PID:", process.pid)
print("CWD:", process.cwd)
print("Uptime:", process.uptime)

let env = process.env()
print(env.Path)

let code = process.execute("dir")
print("Exit code:", code)
```

---

## 导入语法速查

```js
// 单模块导入
import { file } from "system"
import { console } from "system"
import { path } from "system"
import { json } from "system"
import { network } from "system"
import { timer } from "system"
import { crypto } from "system"
import { process } from "system"

// 多模块同时导入
import { file, path, json } from "system"
```

## 模块总览

| 模块 | 类型 | 方法数 | 属性数 | 用途 |
|------|------|--------|--------|------|
| `file` | 文件系统 | 11 | 1 | 文件读写、目录操作 |
| `console` | 控制台 | 6 | 1 | 日志、计时、颜色 |
| `path` | 路径处理 | 6 | 2 | 路径拼接、解析 |
| `json` | JSON | 2 | 0 | JSON 序列化/反序列化 |
| `network` | 网络 | 2 | 0 | HTTP GET/POST |
| `timer` | 定时器 | 4 | 0 | 延迟、定时、周期执行 |
| `crypto` | 加密 | 5 | 1 | 哈希、HMAC、随机数、UUID |
| `process` | 进程 | 5 | 4 | 命令行、环境、执行外部命令 |
