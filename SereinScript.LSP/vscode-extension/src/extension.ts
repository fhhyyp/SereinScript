import * as vscode from 'vscode';
import * as path from 'path';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: vscode.ExtensionContext) {
    // 服务器路径
    const serverPath = path.join(context.extensionPath, 'server', 'SereinScript.LSP.exe');
    console.log('Extension activate() start');
    vscode.window.showInformationMessage(`Server path: ${serverPath}`);
    // 服务器选项
    const serverOptions: ServerOptions = {
        run: {
            command: serverPath,
            transport: TransportKind.stdio
        },
        debug: {
            command: serverPath,
            transport: TransportKind.stdio,
            options: {
                env: {
                    ...process.env,
                    DEBUG: 'true'
                }
            }
        }
    };

    // 客户端选项
    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'sereinscript' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.script')
        }
    };

    // 创建客户端并启动
    client = new LanguageClient(
        'sereinscript',
        'SereinScript Language Server',
        serverOptions,
        clientOptions
    );

    // 启动客户端
    client.start();
}

export function deactivate() {
    if (!client) {
        return undefined;
    }
    return client.stop();
}