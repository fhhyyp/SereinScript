namespace ScriptLang
{
    /// <summary>
    /// 执行模式
    /// </summary>
    public enum ExecutionMode
    {
        /// <summary>编译为字节码后执行</summary>
        Compiled,
        /// <summary>直接解释执行 AST</summary>
        Interpreted
    }


}
