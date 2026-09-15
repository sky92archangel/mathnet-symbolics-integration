// ----------------------------------------------------------------------------
// (EN) Purpose: Rational-coefficient polynomial arithmetic used by the rational
//       function integrator: degree, Euclidean division, multiply/add, integer
//       scaling and conversion to/from expressions.
// (ZH) 用途：有理函数积分器使用的有理系数多项式运算：次数、带余除法、乘加、整数化以及
//       与表达式的相互转换。
// ----------------------------------------------------------------------------

using System.Numerics;

namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// (EN) Polynomial helper over <see cref="Rational"/> coefficients (index = degree).
/// (ZH) 基于 <see cref="Rational"/> 系数的多项式工具（下标为次数）。
/// </summary>
internal static class Polynomial
{
    /// <summary>
    /// (EN) Highest index with a non-zero coefficient, or -1 for the zero polynomial.
    /// (ZH) 最高非零系数的下标；零多项式返回 -1。
    /// </summary>
    public static int Degree(IReadOnlyList<Rational> c)
    {
        for (int i = c.Count - 1; i >= 0; i--)
            if (!c[i].IsZero) return i;
        return -1;
    }

    /// <summary>
    /// (EN) Removes trailing zero coefficients. (ZH) 去除尾部零系数。
    /// </summary>
    public static List<Rational> Trim(List<Rational> c)
    {
        while (c.Count > 0 && c[^1].IsZero) c.RemoveAt(c.Count - 1);
        return c;
    }

    /// <summary>
    /// (EN) Euclidean division num = q·den + r with deg r &lt; deg den. (ZH) 带余除法 num = q·den + r，deg r &lt; deg den。
    /// </summary>
    public static (List<Rational> Quotient, List<Rational> Remainder) Divide(
        IReadOnlyList<Rational> num, IReadOnlyList<Rational> den)
    {
        var r = new List<Rational>(num);
        int dd = Degree(den), dn = Degree(r);
        if (dd < 0) throw new DivideByZeroException();
        if (dn < dd) return (new List<Rational> { Rational.Zero }, r);

        var q = Enumerable.Repeat(Rational.Zero, dn - dd + 1).ToList();
        var lead = den[dd];
        for (int k = dn - dd; k >= 0; k--)
        {
            if (k + dd >= r.Count) continue;
            var factor = r[k + dd] / lead;
            if (factor.IsZero) continue;
            q[k] = factor;
            for (int j = 0; j <= dd; j++)
                r[k + j] -= factor * den[j];
        }
        return (Trim(q), Trim(r));
    }

    /// <summary>
    /// (EN) Makes a polynomial monic (leading coefficient 1). (ZH) 将多项式化为首一（首项系数 1）。
    /// </summary>
    public static List<Rational> Monic(IReadOnlyList<Rational> p)
    {
        int d = Degree(p);
        if (d < 0) return new List<Rational> { Rational.Zero };
        var lead = p[d];
        return p.Select(c => c / lead).ToList();
    }

    /// <summary>
    /// (EN) Greatest common divisor of two polynomials via the Euclidean algorithm (monic result).
    /// (ZH) 用欧几里得算法求两个多项式的最大公因式（结果首一）。
    /// </summary>
    public static List<Rational> Gcd(IReadOnlyList<Rational> a, IReadOnlyList<Rational> b)
    {
        var x = Trim(new List<Rational>(a));
        var y = Trim(new List<Rational>(b));
        while (Degree(y) >= 0)
        {
            var (_, r) = Divide(x, y);
            x = y;
            y = Trim(r);
        }
        return Monic(x);
    }

    /// <summary>
    /// (EN) Polynomial product. (ZH) 多项式乘积。
    /// </summary>
    public static List<Rational> Multiply(IReadOnlyList<Rational> a, IReadOnlyList<Rational> b)
    {
        if (Degree(a) < 0 || Degree(b) < 0) return new List<Rational> { Rational.Zero };
        var result = Enumerable.Repeat(Rational.Zero, a.Count + b.Count - 1).ToList();
        for (int i = 0; i < a.Count; i++)
            for (int j = 0; j < b.Count; j++)
                result[i + j] += a[i] * b[j];
        return Trim(result);
    }

    /// <summary>
    /// (EN) Polynomial sum. (ZH) 多项式和。
    /// </summary>
    public static List<Rational> Add(IReadOnlyList<Rational> a, IReadOnlyList<Rational> b)
    {
        int n = Math.Max(a.Count, b.Count);
        var result = Enumerable.Repeat(Rational.Zero, n).ToList();
        for (int i = 0; i < a.Count; i++) result[i] += a[i];
        for (int i = 0; i < b.Count; i++) result[i] += b[i];
        return Trim(result);
    }

    /// <summary>
    /// (EN) Evaluates the polynomial at a rational point. (ZH) 在有理点处求值。
    /// </summary>
    public static Rational Evaluate(IReadOnlyList<Rational> c, Rational x)
    {
        Rational result = Rational.Zero;
        for (int i = c.Count - 1; i >= 0; i--)
            result = result * x + c[i];
        return result;
    }

    /// <summary>
    /// (EN) Builds the expression Σ cₖ·vᵏ. (ZH) 构造表达式 Σ cₖ·vᵏ。
    /// </summary>
    public static Expression ToExpression(IReadOnlyList<Rational> c, Expression v)
    {
        Expression? result = null;
        for (int k = 0; k < c.Count; k++)
        {
            if (c[k].IsZero) continue;
            var num = new Expression.Number(c[k]);
            Expression term = k == 0 ? num : Operators.Multiply(num, Operators.Pow(v, k));
            result = result is null ? term : Operators.Add(result, term);
        }
        return result ?? Operators.Zero;
    }

    /// <summary>
    /// (EN) Scales rational coefficients to primitive integers (used for the rational-root search).
    /// (ZH) 将有理系数化为本原整数系数（用于有理根搜索）。
    /// </summary>
    public static List<BigInteger> ToIntegers(IReadOnlyList<Rational> c)
    {
        BigInteger lcm = BigInteger.One;
        foreach (var r in c)
        {
            var d = r.Denominator;
            lcm = lcm / BigInteger.GreatestCommonDivisor(lcm, d) * d;
        }
        var ints = c.Select(r => r.Numerator * (lcm / r.Denominator)).ToList();
        BigInteger gcd = BigInteger.Zero;
        foreach (var v in ints) gcd = BigInteger.GreatestCommonDivisor(gcd, BigInteger.Abs(v));
        if (gcd > BigInteger.One)
            for (int i = 0; i < ints.Count; i++) ints[i] /= gcd;
        return ints;
    }
}
