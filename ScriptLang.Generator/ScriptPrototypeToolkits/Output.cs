
#nullable disable

using System;

namespace ScriptLang
{
    /// <summary> 定义原型扩展的属性 </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    internal sealed class PrototypeExtensionAttribute : Attribute
    {
    }

    /// <summary> 定义原型方法的属性 </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class PrototypePropertyAttribute : Attribute
    {
        /// <summary>重新定义属性名称</summary>
#nullable enable
        public string? Name = default;
    }
    /// <summary> 定义原型方法的属性 </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class PrototypeFunctionAttribute : Attribute
    {
        /// <summary>重新定义方法名称</summary>
#nullable enable
        public string? Name = default;
    }

}
