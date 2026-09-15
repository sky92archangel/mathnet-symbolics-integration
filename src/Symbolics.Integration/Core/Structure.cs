namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// (EN) Tree traversal, substitution, and inspection utilities.
/// (ZH) 表达式树的遍历、替换与检查工具。
/// </summary>
public static class Structure
{
    /// <summary>
    /// (EN) Does the expression contain the given variable?
    /// (ZH) 表达式是否包含指定的变量？
    /// </summary>
    public static bool ContainsVariable(Expression expr, Expression variable)
    {
        if (expr.Equals(variable)) return true;
        return expr switch
        {
            Expression.SymbolExpr _ => false,
            Expression.Number _ => false,
            Expression.Constant _ => false,
            Expression.Approximation _ => false,
            Expression.Infinity _ => false,
            Expression.Undefined _ => false,
            Expression.Sum s => s.Terms.Any(t => ContainsVariable(t, variable)),
            Expression.Product p => p.Factors.Any(f => ContainsVariable(f, variable)),
            Expression.Power pw => ContainsVariable(pw.Base, variable) || ContainsVariable(pw.Exponent, variable),
            Expression.Function f => ContainsVariable(f.Argument, variable),
            Expression.FunctionN fn => fn.Arguments.Any(a => ContainsVariable(a, variable)),
            _ => false
        };
    }

    /// <summary>
    /// (EN) Recursively substitute 'from' with 'to' in the expression.
    /// (ZH) 在表达式中递归地将 'from' 替换为 'to'。
    /// </summary>
    public static Expression Substitute(Expression from, Expression to, Expression expr)
    {
        if (expr.Equals(from)) return to;
        return expr switch
        {
            Expression.Sum s => new Expression.Sum(
                s.Terms.Select(t => Substitute(from, to, t)).ToList()),
            Expression.Product p => new Expression.Product(
                p.Factors.Select(f => Substitute(from, to, f)).ToList()),
            Expression.Power pw => new Expression.Power(
                Substitute(from, to, pw.Base),
                Substitute(from, to, pw.Exponent)),
            Expression.Function f => new Expression.Function(
                f.Op, Substitute(from, to, f.Argument)),
            Expression.FunctionN fn => new Expression.FunctionN(
                fn.Op, fn.Arguments.Select(a => Substitute(from, to, a)).ToList()),
            _ => expr
        };
    }

    /// <summary>
    /// (EN) Recursively apply a transformation to every node in the expression tree.
    /// (ZH) 对表达式树中的每个节点递归应用变换函数。
    /// </summary>
    public static Expression Map(Func<Expression, Expression> f, Expression expr)
    {
        var mapped = expr switch
        {
            Expression.Sum s => new Expression.Sum(
                s.Terms.Select(t => Map(f, t)).ToList()),
            Expression.Product p => new Expression.Product(
                p.Factors.Select(fa => Map(f, fa)).ToList()),
            Expression.Power pw => new Expression.Power(
                Map(f, pw.Base), Map(f, pw.Exponent)),
            Expression.Function fn => new Expression.Function(
                fn.Op, Map(f, fn.Argument)),
            Expression.FunctionN fn => new Expression.FunctionN(
                fn.Op, fn.Arguments.Select(a => Map(f, a)).ToList()),
            _ => expr
        };
        return f(mapped);
    }

    /// <summary>
    /// (EN) Collect all distinct sub-expressions matching a predicate.
    /// (ZH) 收集所有满足谓词条件的互异子表达式。
    /// </summary>
    public static HashSet<Expression> CollectAll(Expression expr,
        Func<Expression, bool> predicate)
    {
        var results = new HashSet<Expression>();
        CollectAllImpl(expr, predicate, results);
        return results;
    }

    /// <summary>
    /// (EN) Internal recursive implementation of <see cref="CollectAll"/>.
    /// (ZH) <see cref="CollectAll"/> 的内部递归实现。
    /// </summary>
    private static void CollectAllImpl(Expression expr,
        Func<Expression, bool> predicate, HashSet<Expression> results)
    {
        if (predicate(expr))
            results.Add(expr);

        switch (expr)
        {
            case Expression.Sum s:
                foreach (var t in s.Terms) CollectAllImpl(t, predicate, results);
                break;
            case Expression.Product p:
                foreach (var f in p.Factors) CollectAllImpl(f, predicate, results);
                break;
            case Expression.Power pw:
                CollectAllImpl(pw.Base, predicate, results);
                CollectAllImpl(pw.Exponent, predicate, results);
                break;
            case Expression.Function fn:
                CollectAllImpl(fn.Argument, predicate, results);
                break;
            case Expression.FunctionN fn:
                foreach (var a in fn.Arguments) CollectAllImpl(a, predicate, results);
                break;
        }
    }

    /// <summary>
    /// (EN) Count the "complexity" of an expression (number of operator nodes).
    /// (ZH) 计算表达式的"复杂度"（运算符节点的数量）。
    /// </summary>
    public static int CountOperators(Expression expr) => expr switch
    {
        Expression.Number _ or Expression.SymbolExpr _
            or Expression.Constant _ or Expression.Approximation _
            or Expression.Infinity _ or Expression.Undefined _ => 0,

        Expression.Sum s => 1 + s.Terms.Sum(CountOperators),
        Expression.Product p => 1 + p.Factors.Sum(CountOperators),
        Expression.Power pw => 1 + CountOperators(pw.Base) + CountOperators(pw.Exponent),
        Expression.Function f => 1 + CountOperators(f.Argument),
        Expression.FunctionN fn => 1 + fn.Arguments.Sum(CountOperators),
        _ => 0
    };
}
