const vscode = require('vscode');
const path = require('path');
const { LanguageClient, TransportKind } = require('vscode-languageclient/node');

let client;

function activate(context) {
    const config = vscode.workspace.getConfiguration('sereinscript');
    let serverPath = config.get('server.path');
    
    if (!serverPath) {
        serverPath = context.asAbsolutePath(path.join('server', 'ScriptLang.Lsp.dll'));
    }
    
    // 创建输出通道用于显示 LSP 日志
    const outputChannel = vscode.window.createOutputChannel('SereinScript LSP');
    outputChannel.appendLine(`Starting SereinScript LSP server from: ${serverPath}`);
    
    const serverOptions = {
        command: 'dotnet',
        args: [serverPath],
        transport: TransportKind.stdio,
        options: {
            env: {
                ...process.env,
                // 可以添加环境变量来控制日志级别
                "LSP_DEBUG": "true"
            }
        }
    };
    
    const clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'sereinscript' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.script')
        },
        outputChannel: outputChannel,  // 重要：将输出通道传递给客户端
        traceOutputChannel: outputChannel  // 显示协议跟踪信息
    };
    
    client = new LanguageClient(
        'sereinscript-lsp',
        'SereinScript Language Server',
        serverOptions,
        clientOptions
    );
    
    // 启动客户端
    client.start().then(() => {
        outputChannel.appendLine('SereinScript LSP server started successfully');
        console.log('SereinScript LSP server started successfully');
    }).catch(err => {
        const errorMsg = `Failed to start SereinScript LSP server: ${err}`;
        outputChannel.appendLine(errorMsg);
        console.error(errorMsg);
        vscode.window.showErrorMessage(errorMsg);
    });
    
    // 注册命令来显示日志
    context.subscriptions.push(
        vscode.commands.registerCommand('sereinscript.showLogs', () => {
            outputChannel.show();
        })
    );
}

function deactivate() {
    if (!client) {
        return undefined;
    }
    return client.stop();
}

module.exports = {
    activate,
    deactivate
};