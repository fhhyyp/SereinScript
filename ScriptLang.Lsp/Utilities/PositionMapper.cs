using ScriptLang.Lexer;
using ScriptLang.Parser;

namespace ScriptLang.Lsp.Utilities;

/// <summary>
/// 文档位置 ↔ Token偏移 双向转换
/// </summary>
public static class PositionMapper
{
    /// <summary>
    /// 将 (line, character) 转换为文档绝对偏移量
    /// </summary>
    public static int GetOffset(string text, int line, int character)
    {
        int currentLine = 0;
        int currentChar = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (currentLine == line && currentChar == character)
                return i;

            if (text[i] == '\n')
            {
                currentLine++;
                currentChar = 0;
            }
            else
            {
                currentChar++;
            }
        }

        // 到达行末
        return text.Length;
    }

    /// <summary>
    /// 将文档绝对偏移量转换为 (line, character)
    /// </summary>
    public static (int line, int character) GetPosition(string text, int offset)
    {
        int line = 0;
        int character = 0;

        for (int i = 0; i < Math.Min(offset, text.Length); i++)
        {
            if (text[i] == '\n')
            {
                line++;
                character = 0;
            }
            else
            {
                character++;
            }
        }

        return (line, character);
    }

    /// <summary>
    /// 判断位置是否在 SourceSpan 范围内
    /// </summary>
    public static bool IsInSpan(SourceSpan span, int offset)
    {
        return offset >= span.Start && offset < span.Start + span.Length;
    }

    /// <summary>
    /// 判断位置是否在 Token 范围内
    /// </summary>
    public static bool IsInToken(Token token, int offset)
    {
        return offset >= token.StartIndex && offset < token.StartIndex + token.Length;
    }

    /// <summary>
    /// 查找指定偏移量处的 Token
    /// </summary>
    public static Token? FindTokenAt(List<Token> tokens, int offset)
    {
        // 二分查找
        int left = 0, right = tokens.Count - 1;
        while (left <= right)
        {
            int mid = (left + right) / 2;
            var token = tokens[mid];

            if (offset < token.StartIndex)
                right = mid - 1;
            else if (offset >= token.StartIndex + token.Length)
                left = mid + 1;
            else
                return token;
        }
        return null;
    }

    /// <summary>
    /// 查找指定位置所在的 AST 节点（最深匹配）
    /// </summary>
    public static Expr? FindNodeAt(Expr root, int offset)
    {
        return FindNodeAtRecursive(root, offset, null);
    }

    private static Expr? FindNodeAtRecursive(Expr? node, int offset, Expr? best)
    {
        if (node == null) return best;

        if (IsInSpan(node.SourceSpan, offset))
        {
            best = node;

            best = node switch
            {
                ProgramExpr e => FindInList(e.Statements, offset, best),
                BlockExpr e => FindInList(e.Statements, offset, best),
                LetExpr e => FindNodeAtRecursive(e.Value, offset, best),
                VarExpr e => FindNodeAtRecursive(e.Value, offset, best),
                AssignExpr e => FindNodeAtRecursive(e.Value, offset, best),
                BinaryExpr e => FindNodeAtRecursive(e.Left, offset,
                                    FindNodeAtRecursive(e.Right, offset, best)),
                UnaryExpr e => FindNodeAtRecursive(e.Expr, offset, best),
                ConditionalExpr e => FindNodeAtRecursive(e.Cond, offset,
                                        FindNodeAtRecursive(e.Then, offset,
                                            FindNodeAtRecursive(e.Else, offset, best))),
                IfExpr e => FindNodeAtRecursive(e.Cond, offset,
                                FindNodeAtRecursive(e.Then, offset,
                                    FindNodeAtRecursive(e.Else, offset, best))),
                WhenExpr e => FindInWhen(e, offset, best),
                ForExpr e => FindNodeAtRecursive(e.Iterable, offset,
                                FindNodeAtRecursive(e.Body, offset, best)),
                LambdaExpr e => FindNodeAtRecursive(e.Body, offset, best),
                CallExpr e => FindNodeAtRecursive(e.Target, offset,
                                FindInList(e.Args, offset, best)),
                ReturnExpr e => e.Value != null ? FindNodeAtRecursive(e.Value, offset, best) : best,
                ArrayLiteralExpr e => FindInList(e.Elements, offset, best),
                ObjectLiteralExpr e => FindInList(e.Properties, offset, best),
                MemberAccessExpr e => FindNodeAtRecursive(e.Target, offset, best),
                MemberAssignExpr e => FindNodeAtRecursive(e.Target, offset,
                                        FindNodeAtRecursive(e.Value, offset, best)),
                IndexAccessExpr e => FindNodeAtRecursive(e.Target, offset,
                                        FindNodeAtRecursive(e.Index, offset, best)),
                IndexAssignExpr e => FindNodeAtRecursive(e.Target, offset,
                                        FindNodeAtRecursive(e.Index, offset,
                                            FindNodeAtRecursive(e.Value, offset, best))),
                _ => best
            };
        }

        return best;
    }

    private static Expr? FindInList<T>(IEnumerable<T> items, int offset, Expr? best) where T : Expr
    {
        foreach (var item in items)
            best = FindNodeAtRecursive(item, offset, best);
        return best;
    }

    private static Expr? FindInWhen(WhenExpr e, int offset, Expr? best)
    {
        best = FindNodeAtRecursive(e.Value, offset, best);
        foreach (var clause in e.Clauses)
        {
            best = FindNodeAtRecursive(clause.Pattern, offset, best);
            best = FindNodeAtRecursive(clause.Body, offset, best);
        }
        if (e.OtherClause != null)
            best = FindNodeAtRecursive(e.OtherClause.Body, offset, best);
        return best;
    }
}
