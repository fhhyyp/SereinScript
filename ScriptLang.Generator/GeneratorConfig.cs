#nullable disable

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang.Generator
{
    /// <summary>
    /// 代码生成器生成配置
    /// </summary>
    internal static class GeneratorConfig
    {
        /// <summary>
        /// 调试控件代码生成器
        /// </summary>
        public static bool IsDebugControlToolkitsGenerator { get; } = false;

        /// <summary>
        /// 调试通知属性代码生成器
        /// </summary>
        public static bool IsDebugHereinNotifyGenerator { get; } = false;

        /// <summary>
        /// 调试DTO生成器
        /// </summary>
        public static bool IsDebugLitheDtoGenerator { get; } = false;

        /// <summary>
        /// 调试视图定位器生成器
        /// </summary>
        public static bool IsDebugViewLocatorGenerator { get; } = false;

        /// <summary>
        /// 调试脚本原型生成器
        /// </summary>
        public static bool IsDebugScriptPrototypeToolkits { get; } = false;


        internal static SymbolDisplayFormat GlobalFullTypeFormat { get; } =
             new SymbolDisplayFormat(
                 globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
                 typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                 genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                 miscellaneousOptions:
                     SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
             );

        internal static SymbolDisplayFormat FullTypeFormat { get; } =
             new SymbolDisplayFormat(
                 globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                 typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                 genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                 miscellaneousOptions:
                     SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
             );

        /// <summary>
        /// 引用的命名空间
        /// </summary>
        public static List<string> DefaultUsings{ get;  } = new List<string>();

        /// <summary>
        /// 重置默认使用的命名空间
        /// </summary>
        public static void ResetDefaultUsing()
        {
            // DefaultUsings.Add($"System");
            // DefaultUsings.Add($"System.Linq");
            // DefaultUsings.Add($"System.Threading");
            // DefaultUsings.Add($"System.Threading.Tasks");
            // DefaultUsings.Add($"System.Collections.Concurrent");
            // DefaultUsings.Add($"System.Collections.Generic");
        }
        static GeneratorConfig()
        {
            ResetDefaultUsing();
        }
    }
}
