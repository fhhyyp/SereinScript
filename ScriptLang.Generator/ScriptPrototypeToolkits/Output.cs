
#nullable disable

using System;
using System.Collections.Generic;
using System.Text;

/*public partial class Value;
public partial class ScriptEngine;*/

namespace ScriptLang
{
    /*/// <summary>
    /// 原型接口
    /// </summary>
    public interface IPrototype
    {
        /// <summary>
        /// 是否已加载
        /// </summary>
        bool IsLoad { get; }

        /// <summary>
        /// 初始化原型
        /// </summary>
        void Init();

        /// <summary>
        /// 判断值是否为目标类型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool IsTarget(Value value);

        /// <summary>
        /// 获取方法
        /// </summary>
        /// <param name="value">传入的值</param>
        /// <param name="methodName">方法名</param>
        /// <param name="engine">脚本引擎</param>
        /// <returns>方法值，如果不存在则返回 null</returns>
        Value? GetMethod(Value value, string methodName, ScriptEngine engine);
    }  */ 
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
        public string? Name;
    }
    /// <summary> 定义原型方法的属性 </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class PrototypeFunctionAttribute : Attribute
    {
        /// <summary>重新定义方法名称</summary>
        #nullable enable
        public string? Name;
    }

}
