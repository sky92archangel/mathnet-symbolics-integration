namespace MathNet.Symbolics.Integration.Core;

/// <summary>Tree traversal, substitution, and inspection utilities.</summary>
public static class Structure
{
    /// <summary>Does the expression contain the given variable?</summary>
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

    /// <summary>Recursively substitute 'from' with 'to' in the expression.</summary>
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

    /// <summary>Recursively apply a transformation to every node in the expression tree.</summary>
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

    /// <summary>Collect all distinct sub-expressions matching a predicate.</summary>
    public static HashSet<Expression> CollectAll(Expression expr,
        Func<Expression, bool> predicate)
    {
        var results = new HashSet<Expression>();
        CollectAllImpl(expr, predicate, results);
        return results;
    }

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

    /// <summary>Count the "complexity" of an expression (operators count).</summary>
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
