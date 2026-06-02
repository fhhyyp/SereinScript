using ScriptLang.Parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>字节码操作符</summary>
public enum OpCode : byte
{
    /// <summary>无操作</summary>
    Nop = 0x00,
    /// <summary>加载 null </summary>
    LoadNull = 0x01,
    /// <summary>加载 true </summary>
    LoadTrue = 0x02,
    /// <summary>加载 false </summary>
    LoadFalse = 0x03,
    /// <summary>加载常量（Let）</summary>
    LoadConst = 0x04,
    /// <summary>加载变量</summary>
    LoadVar = 0x05,

    /// <summary>加载全局变量</summary>
    LoadGlobal = 0x6,

    /// <summary>存储全局变量</summary>
    StoreGlobal = 0x7,

    /// <summary>存储变量</summary>
    StoreVar = 0x08,
    /// <summary>将栈顶元素弹出</summary>
    Pop = 0x09,
    /// <summary>将栈顶元素复制一份并压入栈顶</summary>
    Dup = 0x10,

    /// <summary>'+'操作符</summary>
    Add = 0x20,
    /// <summary>'-'操作符</summary>
    Sub = 0x21,
    /// <summary>'*'操作符</summary>
    Mul = 0x22,
    /// <summary>'/'操作符</summary>
    Div = 0x23,
    /// <summary>'%'操作符</summary>
    Mod = 0x24,
    /// <summary>负号</summary>
    Neg = 0x25,
    /// <summary>'!'操作符</summary>
    Not = 0x26,
    /// <summary>'=' 操作符</summary>
    Equal = 0x27,
    /// <summary>'!=' 操作符</summary>
    Ne = 0x28,
    /// <summary>'>' 操作符</summary>
    Gt = 0x29,
    /// <summary>'>=' 操作符</summary>
    Ge = 0x2A,
    /// <summary>'<' 操作符</summary>
    Lt = 0x2B,
    /// <summary>'<='操作符</summary>
    Le = 0x2C,
    /// <summary>'&&' 操作符</summary>
    And = 0x2D,
    /// <summary>'||' 操作符</summary>
    Or = 0x2E,

    /// <summary>跳转</summary>
    Jmp = 0x30,
    /// <summary>真分支跳转</summary>
    JumpIfTrue = 0x31,
    /// <summary>假分支跳转</summary>
    JmpIfFalse = 0x32,

    /// <summary>创建闭包</summary>
    CreateClosure = 0x40,
    /// <summary>调用函数</summary>
    Call = 0x41,
    /// <summary>返回</summary>
    Return = 0x42,

    /// <summary>导入模块</summary>
    Import = 0x50,

    /// <summary>捕获变量</summary>
    Capture = 0x60,
    /// <summary>加载捕获的变量</summary>
    LoadCapture = 0x61,
    /// <summary>存储捕获的变量</summary>
    StoreCapture = 0x62,

    /// <summary>创建对象</summary>
    CreateObject = 0x70,
    /// <summary>获取成员</summary>
    GetMember = 0x71,
    /// <summary>设置成员</summary>
    SetMember = 0x72,

    /// <summary>创建对象</summary>
    CreateArray = 0x80,
    /// <summary>获取索引</summary>
    GetIndex = 0x81,
    /// <summary>设置索引</summary>
    SetIndex = 0x82,

    /// <summary>获取迭代器</summary>
    GetIterator = 0x90,
    /// <summary>移动到下一个元素</summary>
    MoveNext = 0x91,
    /// <summary>读取迭代器当前</summary>
    Current = 0x92,

    /// <summary>-1</summary>
    LoadM1 = 0x93,
    /// <summary>0</summary>
    Load0 = 0x94,
    /// <summary>1</summary>
    Load1 = 0x95,
}

