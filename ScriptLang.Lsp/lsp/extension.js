const vscode = require('vscode');
const path = require('path');
const { LanguageClient, TransportKind } = require('vscode-languageclient/node');

let client;

function activate(context) {
    // 获取配置的服务器路径
    const config = vscode.workspace.getConfiguration('sereinscript');
    let serverPath = config.get('server.path');
    
    // 如果没有配置路径，使用扩展内置的服务器
    if (!serverPath) {
        serverPath = context.asAbsolutePath(path.join('server', 'ScriptLang.Lsp.dll'));
    }
    
    console.log(`Starting SereinScript LSP server from: ${serverPath}`);
    
    // 服务器选项
    const serverOptions = {
        command: 'dotnet',
        args: [serverPath],
        transport: TransportKind.stdio
    };
    
    // 客户端选项
    const clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'sereinscript' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.script')
        }
    };
    
    // 创建语言客户端
    client = new LanguageClient(
        'sereinscript-lsp',
        'SereinScript Language Server',
        serverOptions,
        clientOptions
    );
    
    // 启动客户端
    client.start().then(() => {
        console.log('SereinScript LSP server started successfully');
    }).catch(err => {
        console.error('Failed to start SereinScript LSP server:', err);
        vscode.window.showErrorMessage('Failed to start SereinScript LSP server');
    });
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