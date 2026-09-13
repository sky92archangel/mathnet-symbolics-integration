namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// Algebraic decomposition helpers: splitting sums into summands,
/// products into factors, etc.
/// </summary>
public static class Algebraic
{
    /// <summary>If expr is a Sum, return its terms; otherwise return [expr].</summary>
    public static IReadOnlyList<Expression> Summands(Expression expr) =>
        expr is Expression.Sum s ? s.Terms : new[] { expr };

    /// <summary>If expr is a Product, return its factors; otherwise return [expr].</summary>
    public static IReadOnlyList<Expression> Factors(Expression expr) =>
        expr is Expression.Product p ? p.Factors : new[] { expr };

    /// <summary>
    /// Expand a product of sums: (a+b)*(c+d) → a*c + a*d + b*c + b*d.
    /// Only the top level; does not recurse.
    /// </summary>
    public static Expression ExpandMain(Expression expr)
    {
        if (expr is Expression.Product p && p.Factors.Count >= 2)
        {
            // Find first sum factor
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
