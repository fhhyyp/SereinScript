namespace ScriptLang.Runtime;

/// <summary>
/// 模块成员注入器：将模块 exports 的指定成员注入到当前作用域
/// </summary>
public static class ModuleInjector
{
    /// <summary>
    /// 将模块成员注入到当前作用域
    /// </summary>
    /// <param name="exports">模块 exports 对象</param>
    /// <param name="members">要导入的成员列表</param>
    /// <param name="scope">当前作用域</param>
    /// <exception cref="RuntimeException">当成员不存在于 exports 时抛出</exception>
    public static void InjectMembers(ObjectValue exports, List<string> members, Scope scope)
    {
        foreach (var memberName in members)
        {
            // 检查 exports 是否包含该成员
            if (!exports.Properties.TryGetValue(memberName, out var memberValue))
            {
                throw new RuntimeException($"模块未导出成员 '{memberName}'");
            }

            // 将成员值定义到当前作用域
            scope.Define(memberName, memberValue);
        }
    }

    /// <summary>
    /// 将整个模块作为命名空间注入（所有成员以模块名为前缀）
    /// </summary>
    /// <param name="exports">模块 exports 对象</param>
    /// <param name="namespaceName">命名空间名称</param>
    /// <param name="scope">当前作用域</param>
    public static void InjectNamespace(ObjectValue exports, string namespaceName, Scope scope)
    {
        scope.Define(namespaceName, exports);
    }

    /// <summary>
    /// 注入所有成员（不带前缀，类似于 import * from "module"）
    /// </summary>
    /// <param name="exports">模块 exports 对象</param>
    /// <param name="scope">当前作用域</param>
    public static void InjectAll(ObjectValue exports, Scope scope)
    {
        foreach (var kvp in exports.Properties)
        {
            scope.Define(kvp.Key, kvp.Value);
        }
    }
}
