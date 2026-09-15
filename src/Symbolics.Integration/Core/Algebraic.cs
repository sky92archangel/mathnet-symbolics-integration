namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// (EN) Algebraic decomposition helpers: splitting sums into summands,
///      products into factors, etc.
/// (ZH) 代数分解辅助工具：拆分和式为加项、乘积为因式等。
/// </summary>
public static class Algebraic
{
    /// <summary>
    /// (EN) If expr is a Sum, return its terms; otherwise return [expr].
    /// (ZH) 若 expr 为 Sum 则返回其各项，否则返回 [expr]。
    /// </summary>
    public static IReadOnlyList<Expression> Summands(Expression expr) =>
        expr is Expression.Sum s ? s.Terms : new[] { expr };

    /// <summary>
    /// (EN) If expr is a Product, return its factors; otherwise return [expr].
    /// (ZH) 若 expr 为 Product 则返回其各因式，否则返回 [expr]。
    /// </summary>
    public static IReadOnlyList<Expression> Factors(Expression expr) =>
        expr is Expression.Product p ? p.Factors : new[] { expr };

    /// <summary>
    /// (EN) Expand a product of sums: (a+b)*(c+d) → a*c + a*d + b*c + b*d.
    ///      Only the top level; does not recurse.
    /// (ZH) 展开和的乘积：(a+b)*(c+d) → a*c + a*d + b*c + b*d。仅展开顶层，不递归。
    /// </summary>
    public static Expression ExpandMain(Expression expr)
    {
        if (expr is Expression.Product p && p.Factors.Count >= 2)
        {
            // (EN) Find first sum factor. (ZH) 找到第一个求和因子。
            for (int i = 0; i < p.Factors.Count; i++)
            {
                if (p.Factors[i] is Expression.Sum sum)
                {
                    var other = new List<Expression>(p.Factors);
                    other.RemoveAt(i);

                    var expanded = sum.Terms.Select(t =>
                    {
                        var factors = new List<Expression>(other) { t };
                        return (Expression)new Expression.Product(factors);
                    }).ToList();

                    return expanded.Count == 1
                        ? expanded[0]
                        : new Expression.Sum(expanded);
                }
            }
        }
        return expr;
    }
}
