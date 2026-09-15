// ----------------------------------------------------------------------------
// (EN) Purpose: General rational-function integrator. Performs polynomial division,
//       factors the denominator into linear factors (with multiplicity) and
//       irreducible quadratics, decomposes into partial fractions by solving a
//       linear system, and integrates each term (including repeated-quadratic
//       reduction formulas).
// (ZH) 用途：通用有理函数积分器。进行多项式除法，将分母分解为线性因子（含重数）与不可约二次
//       因子，通过求解线性方程组进行部分分式分解，并逐项积分（含重复二次因子的递推公式）。
// (EN) Notes: Coefficients must be numeric rationals; when the denominator cannot be
//       factored into real linear/quadratic factors the integrator declines.
// (ZH) 说明：系数必须为数值有理数；当分母无法分解为实线性/二次因子时，积分器放弃。
// ----------------------------------------------------------------------------

using System.Numerics;
using MathNet.Symbolics.Integration.Core;
using static MathNet.Symbolics.Integration.Core.Operators;

namespace MathNet.Symbolics.Integration;

/// <summary>
/// (EN) Integrates proper/improper rational functions with numeric rational coefficients.
/// (ZH) 积分具有数值有理系数的真/假有理函数。
/// </summary>
internal static class RationalIntegrator
{
    /// <summary>
    /// (EN) Maximum polynomial degree the rational canonicaliser/integrator will handle; guards against
    ///      exponential growth from large exponents.
    /// (ZH) 有理规范化/积分器处理的多项式最大次数；防止大指数导致的指数级膨胀。
    /// </summary>
    private const int MaxDegree = 24;

    /// <summary>
    /// (EN) Attempts ∫ num(x)/den(x) dx. (ZH) 尝试 ∫ num(x)/den(x) dx。
    /// </summary>
    public static bool TryIntegrate(IReadOnlyList<Rational> num, IReadOnlyList<Rational> den,
        Expression x, out Expression result)
    {
        result = Zero;
        int dd = Polynomial.Degree(den);
        if (dd <= 0) return false;

        var (q, r) = Polynomial.Divide(num, den);

        // (EN) Integrate the polynomial quotient term by term. (ZH) 逐项积分多项式商。
        Expression integ = Zero;
        for (int k = 0; k < q.Count; k++)
        {
            if (q[k].IsZero) continue;
            var c = q[k] / (Rational)(k + 1);
            integ = Add(integ, Multiply(new Expression.Number(c), Pow(x, k + 1)));
        }

        if (Polynomial.Degree(r) < 0) { result = integ; return true; }

        // (EN) Reduce r/den by the polynomial GCD: substitutions such as t = tan(θ/2) inflate the
        //      degrees without cancelling common factors. (ZH) 用多项式 GCD 约分 r/den：t = tan(θ/2) 之类的
        //      代换会抬高次数却不约去公因式。
        var denList = den.ToList();
        var gcd = Polynomial.Gcd(r, denList);
        if (Polynomial.Degree(gcd) > 0)
        {
            r = Polynomial.Divide(r, gcd).Quotient;
            denList = Polynomial.Divide(denList, gcd).Quotient;
        }

        if (!TryPartialFractions(r, denList, x, out var part)) return false;
        result = Add(integ, part);
        return true;
    }

    /// <summary>
    /// (EN) Integrates the proper remainder by partial fractions over linear and irreducible quadratic
    ///      factors. (ZH) 通过线性与不可约二次因子的部分分式积分真余式。
    /// </summary>
    private static bool TryPartialFractions(IReadOnlyList<Rational> rem, IReadOnlyList<Rational> den,
        Expression x, out Expression result)
    {
        result = Zero;
        int dd = Polynomial.Degree(den);
        // (EN) Cap the denominator degree to keep factorization bounded. (ZH) 限制分母次数以保证分解有界。
        if (dd <= 0 || dd > 8) return false;
        var lead = den[dd];
        var monic = den.Select(c => c / lead).ToList();
        var num = rem.Select(c => c / lead).ToList();

        // (EN) Factor out linear factors, then at most one irreducible quadratic. (ZH) 分解出线性因子，最后至多一个不可约二次因子。
        var linears = new List<(Rational Root, int Mult)>();
        var quads = new List<(Rational B, Rational C, int Mult)>();
        var work = monic;
        while (true)
        {
            var root = FindRationalRoot(work);
            if (root is null) break;
            int m = 0;
            while (true)
            {
                var (q, rr) = Polynomial.Divide(work, new List<Rational> { -root.Value, Rational.One });
                if (Polynomial.Degree(rr) >= 0) break;
                work = q; m++;
                if (Polynomial.Degree(work) <= 0) break;
            }
            linears.Add((root.Value, m));
            if (m == 0) return false; // (EN) Defensive: avoid a non-reducing loop. (ZH) 防御：避免不归约的循环。
        }

        int wd = Polynomial.Degree(work);
        // (EN) Pull out irreducible quadratic factors (with multiplicity). Remaining factors of
        //      degree 2 (irrational real roots) are kept as a single general quadratic; higher-degree
        //      factors must contain an irreducible quadratic divisor.
        // (ZH) 提取不可约二次因子（含重数）。剩余次数为 2（无理实根）的因子作为单个一般二次保留；
        //      更高次因子必须含有不可约二次因式。
        int guard = 0;
        while (wd > 1)
        {
            if (++guard > 8) return false;
            var q = FindQuadraticFactor(work);
            if (q is null)
            {
                // (EN) Degree-2 remainder with non-rational (irrational) real roots. (ZH) 次数为 2 且根为无理实数的余式。
                if (wd == 2) { quads.Add((work[1], work[0], 1)); wd = 0; break; }
                return false;
            }
            int mult = 0;
            while (true)
            {
                var (qq, rr) = Polynomial.Divide(work, q);
                if (Polynomial.Degree(rr) >= 0) break;
                work = qq; mult++;
                if (Polynomial.Degree(work) <= 0) break;
            }
            if (mult == 0) return false; // (EN) Defensive. (ZH) 防御。
            quads.Add((q[1], q[0], mult));
            wd = Polynomial.Degree(work);
        }
        if (wd > 0) return false;

        // (EN) Unknowns: linear A's then quadratic (B,C) pairs. (ZH) 未知量：线性 A，随后二次 (B,C) 对。
        var unknownIndex = new List<(bool IsQuad, int Factor, int Power, bool IsB)>();
        for (int f = 0; f < linears.Count; f++)
            for (int k = 1; k <= linears[f].Mult; k++)
                unknownIndex.Add((false, f, k, false));
        for (int f = 0; f < quads.Count; f++)
            for (int k = 1; k <= quads[f].Mult; k++)
            {
                unknownIndex.Add((true, f, k, true));
                unknownIndex.Add((true, f, k, false));
            }
        int n = unknownIndex.Count;
        if (n != dd) return false;

        // (EN) Build the linear system: rem(x) = Σ coeff·basis, basis = numerator·(monic/factor^power).
        // (ZH) 构造线性方程组：rem(x) = Σ coeff·basis，basis = 分子·(monic/factor^power)。
        var matrix = new List<List<Rational>>();
        var rhs = new List<Rational>();
        for (int row = 0; row < dd; row++)
        {
            matrix.Add(Enumerable.Repeat(Rational.Zero, n).ToList());
            rhs.Add(row < num.Count ? num[row] : Rational.Zero);
        }

        for (int col = 0; col < n; col++)
        {
            var (isQuad, f, power, isB) = unknownIndex[col];
            List<Rational> factorPow;
            List<Rational> numer;
            if (!isQuad)
            {
                var r = linears[f].Root;
                factorPow = PowPoly(new List<Rational> { -r, Rational.One }, power);
                numer = new List<Rational> { Rational.One };
            }
            else
            {
                var (b, c, _) = quads[f];
                factorPow = PowPoly(new List<Rational> { c, b, Rational.One }, power);
                numer = isB ? new List<Rational> { Rational.Zero, Rational.One }   // x
                            : new List<Rational> { Rational.One };                // 1
            }

            var (quot, _) = Polynomial.Divide(monic, factorPow);
            var basis = Polynomial.Multiply(quot, numer);
            for (int row = 0; row < dd; row++)
                matrix[row][col] = row < basis.Count ? basis[row] : Rational.Zero;
        }

        if (!SolveLinear(matrix, rhs, out var coeffs)) return false;

        // (EN) Integrate each partial fraction term. (ZH) 逐项积分每个部分分式。
        Expression sum = Zero;
        for (int i = 0; i < n; i++)
        {
            var (isQuad, f, power, isB) = unknownIndex[i];
            var a = coeffs[i];
            if (a.IsZero) continue;
            if (!isQuad)
                sum = Add(sum, IntegrateLinear(a, linears[f].Root, power, x));
            else if (isB)
                sum = Add(sum, IntegrateQuadB(a, quads[f].B, quads[f].C, power, x));
            else
                sum = Add(sum, IntegrateQuadC(a, quads[f].B, quads[f].C, power, x));
        }
        result = sum;
        return true;
    }

    /// <summary>
    /// (EN) ∫ A/(x-r)^k dx. (ZH) ∫ A/(x-r)^k dx。
    /// </summary>
    private static Expression IntegrateLinear(Rational a, Rational r, int k, Expression x)
    {
        var an = new Expression.Number(a);
        var lin = Subtract(x, new Expression.Number(r));
        if (k == 1) return Multiply(an, Ln(lin));
        return Multiply(an, Divide(Pow(lin, 1 - k), new Expression.Number((Rational)(1 - k))));
    }

    /// <summary>
    /// (EN) ∫ A·x/(x²+bx+c)^k dx = (A/2)∫(2x+b)/(...)dx - (A·b/2)∫dx/(...)^k. (ZH) ∫ A·x/(x²+bx+c)^k dx。
    /// </summary>
    private static Expression IntegrateQuadB(Rational a, Rational b, Rational c, int k, Expression x)
    {
        var an = new Expression.Number(a);
        var quad = Add(Add(Multiply(x, x), Multiply(new Expression.Number(b), x)), new Expression.Number(c));
        Expression first = k == 1
            ? Multiply(Divide(an, Two), Ln(quad))
            : Multiply(Divide(an, Two), Divide(Pow(quad, 1 - k), new Expression.Number((Rational)(1 - k))));
        var negHalfAb = new Expression.Number(a * b / (Rational)2 * (Rational)(-1));
        return Add(first, Multiply(negHalfAb, ReciprocalQuadIntegral(k, b, c, x)));
    }

    private static Expression IntegrateQuadC(Rational a, Rational b, Rational c, int k, Expression x)
        => Multiply(new Expression.Number(a), ReciprocalQuadIntegral(k, b, c, x));

    /// <summary>
    /// (EN) ∫ dx/(x²+bx+c)^k. For k = 1 the branch is chosen from the discriminant Δ = b²-4c:
    ///      arctangent (Δ &lt; 0), logarithm (Δ &gt; 0) or -2/(2x+b) (Δ = 0). For k &gt; 1 the
    ///      repeated-quadratic reduction (valid for Δ &lt; 0) is used.
    /// (ZH) ∫ dx/(x²+bx+c)^k。k = 1 时按判别式 Δ = b²-4c 分支：反正切（Δ &lt; 0）、对数（Δ &gt; 0）或
    ///      -2/(2x+b)（Δ = 0）。k &gt; 1 时使用重复二次递推（适用于 Δ &lt; 0）。
    /// </summary>
    private static Expression ReciprocalQuadIntegral(int k, Rational b, Rational c, Expression x)
    {
        if (k > 1) return QuadReduce(k, b, c, x);

        var disc = b * b - (Rational)4 * c;
        if (disc.IsZero)
            return Negate(Divide(Two, Add(Multiply(Two, x), new Expression.Number(b))));
        if (disc.IsPositive)
        {
            // (EN) Δ > 0: (1/√Δ)·ln|(2x+b-√Δ)/(2x+b+√Δ)|. (ZH) Δ > 0：对数形式。
            var sqrtD = Sqrt(new Expression.Number(disc));
            var lin = Add(Multiply(Two, x), new Expression.Number(b));
            return Multiply(Divide(One, sqrtD), Ln(Divide(Subtract(lin, sqrtD), Add(lin, sqrtD))));
        }
        // (EN) Δ < 0: complete the square. (ZH) Δ < 0：配方。
        var u = Add(x, new Expression.Number(b / (Rational)2));
        var a2 = new Expression.Number(c - b * b / (Rational)4);
        var sq = Sqrt(a2);
        return Multiply(Divide(One, sq), Atan(Divide(u, sq)));
    }

    /// <summary>
    /// (EN) ∫ dx/(x²+bx+c)^k by completing the square and the repeated-quadratic reduction formula.
    /// (ZH) 通过配方与重复二次递推公式计算 ∫ dx/(x²+bx+c)^k。
    /// </summary>
    private static Expression QuadReduce(int k, Rational b, Rational c, Expression x)
    {
        // (EN) u = x + b/2, a² = c - b²/4 (> 0 for an irreducible quadratic). (ZH) u = x + b/2，a² = c - b²/4（不可约二次时 > 0）。
        var u = Add(x, new Expression.Number(b / (Rational)2));
        var a2r = c - b * b / (Rational)4;
        var a2 = new Expression.Number(a2r);
        if (k == 1)
        {
            var sq = Sqrt(a2);
            return Multiply(Divide(One, sq), Atan(Divide(u, sq)));
        }
        var um = Add(Multiply(u, u), a2);
        var term = Divide(u,
            Multiply(Multiply(Two, a2), Multiply(new Expression.Number((Rational)(k - 1)), Pow(um, k - 1))));
        var factor = new Expression.Number((Rational)(2 * k - 3) / (Rational)(2 * k - 2) / a2r);
        return Add(term, Multiply(factor, QuadReduce(k - 1, b, c, x)));
    }

    /// <summary>
    /// (EN) Canonicalises an expression that is rational in v into (numerator, denominator) polynomials
    ///      over numeric rationals, combining nested fractions over a common denominator. Returns false
    ///      for anything that is not a numeric rational function of v.
    /// (ZH) 将关于 v 的有理表达式规范化为（分子, 分母）数值有理系数多项式，并把嵌套分式通分合并。
    ///      对任何非 v 的数值有理函数返回 false。
    /// </summary>
    public static bool TryToRationalFunction(Expression e, Expression v,
        out List<Rational> num, out List<Rational> den)
    {
        num = new List<Rational> { Rational.Zero };
        den = new List<Rational> { Rational.One };

        if (!Structure.ContainsVariable(e, v))
        {
            if (e is Expression.Number n)
            {
                num = new List<Rational> { n.Value };
                return true;
            }
            return false;
        }
        if (e.Equals(v))
        {
            num = new List<Rational> { Rational.Zero, Rational.One };
            return true;
        }

        switch (e)
        {
            case Expression.Sum s:
                {
                    var accNum = new List<Rational> { Rational.Zero };
                    var accDen = new List<Rational> { Rational.One };
                    foreach (var term in s.Terms)
                    {
                        if (!TryToRationalFunction(term, v, out var tn, out var td)) return false;
                        // (EN) acc += tn/td. (ZH) acc += tn/td。
                        var newNum = Polynomial.Add(Polynomial.Multiply(accNum, td), Polynomial.Multiply(accDen, tn));
                        accDen = Polynomial.Multiply(accDen, td);
                        accNum = newNum;
                        if (Polynomial.Degree(accNum) > MaxDegree || Polynomial.Degree(accDen) > MaxDegree) return false;
                    }
                    num = accNum; den = accDen;
                    return true;
                }
            case Expression.Product p:
                {
                    var accNum = new List<Rational> { Rational.One };
                    var accDen = new List<Rational> { Rational.One };
                    foreach (var factor in p.Factors)
                    {
                        if (!TryToRationalFunction(factor, v, out var fn, out var fd)) return false;
                        accNum = Polynomial.Multiply(accNum, fn);
                        accDen = Polynomial.Multiply(accDen, fd);
                        if (Polynomial.Degree(accNum) > MaxDegree || Polynomial.Degree(accDen) > MaxDegree) return false;
                    }
                    num = accNum; den = accDen;
                    return true;
                }
            case Expression.Power pw:
                {
                    if (!TryToRationalFunction(pw.Base, v, out var bn, out var bd)) return false;
                    if (pw.Exponent is not Expression.Number ne || !ne.Value.IsInteger) return false;
                    int k = ne.Value.ToInt32();
                    // (EN) Bound the exponent so polynomial degrees stay manageable. (ZH) 限制指数以保证多项式次数可控。
                    if (k > 64 || k < -64) return false;
                    if (k >= 0) { num = PowPoly(bn, k); den = PowPoly(bd, k); }
                    else { num = PowPoly(bd, -k); den = PowPoly(bn, -k); }
                    if (Polynomial.Degree(den) < 0) return false;
                    if (Polynomial.Degree(num) > MaxDegree || Polynomial.Degree(den) > MaxDegree) return false;
                    return true;
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// (EN) Multiplies a polynomial by itself power times. (ZH) 多项式自乘 power 次。
    /// </summary>
    private static List<Rational> PowPoly(List<Rational> p, int power)
    {
        var result = new List<Rational> { Rational.One };
        for (int i = 0; i < power; i++) result = Polynomial.Multiply(result, p);
        return result;
    }

    /// <summary>
    /// (EN) True when the monic quadratic x²+bx+c has negative discriminant. (ZH) 首一二次式 x²+bx+c 判别式为负时返回 true。
    /// </summary>
    private static bool IsIrreducibleQuadratic(IReadOnlyList<Rational> p)
    {
        // (EN) p = [c, b, 1]; Δ = b² - 4c < 0. (ZH) p = [c, b, 1]；Δ = b² - 4c < 0。
        var disc = p[1] * p[1] - (Rational)4 * p[0];
        return disc.IsNegative;
    }

    /// <summary>
    /// (EN) Finds a monic irreducible quadratic factor x²+bx+c (integer b,c in a bounded range) of the
    ///      polynomial. (ZH) 求多项式的首一不可约二次因子 x²+bx+c（整数 b,c 在有界范围内）。
    /// </summary>
    private static List<Rational>? FindQuadraticFactor(IReadOnlyList<Rational> poly)
    {
        if (Polynomial.Degree(poly) < 2) return null;
        // (EN) Search monic irreducible quadratics x²+bx+c with small rational b,c (denominators up to 6),
        //      so denominators like (3+2x²)² = 2²(x²+3/2)² are handled.
        // (ZH) 搜索小有理系数（分母至多 6）的首一不可约二次式 x²+bx+c，以处理 (3+2x²)² = 2²(x²+3/2)² 之类。
        for (int bd = 1; bd <= 6; bd++)
            for (int bn = -24; bn <= 24; bn++)
                for (int cd = 1; cd <= 6; cd++)
                    for (int cn = -24; cn <= 24; cn++)
                    {
                        var q = new List<Rational> { new Rational(cn, cd), new Rational(bn, bd), Rational.One };
                        if (!IsIrreducibleQuadratic(q)) continue;
                        var (_, r) = Polynomial.Divide(poly, q);
                        if (Polynomial.Degree(r) < 0) return q;
                    }
        return null;
    }

    /// <summary>
    /// (EN) Finds one rational root of a polynomial via the rational-root theorem (bounded search).
    /// (ZH) 通过有理根定理（有界搜索）求多项式的一个有理根。
    /// </summary>
    private static Rational? FindRationalRoot(IReadOnlyList<Rational> p)
    {
        int d = Polynomial.Degree(p);
        if (d <= 0) return null;
        var ints = Polynomial.ToIntegers(p);
        var a0 = BigInteger.Abs(ints[0]);
        var an = BigInteger.Abs(ints[d]);
        if (a0.IsZero) return Rational.Zero;

        foreach (var q in Divisors(an))
            foreach (var pn in Divisors(a0))
            {
                var cand = new Rational(pn, q);
                if (Polynomial.Evaluate(p, cand).IsZero) return cand;
                var neg = -cand;
                if (Polynomial.Evaluate(p, neg).IsZero) return neg;
            }
        return null;
    }

    /// <summary>
    /// (EN) Positive divisors of |n| (bounded; falls back to {1, n} for very large n). (ZH) |n| 的正因子（有界；n 过大时退化为 {1, n}）。
    /// </summary>
    private static IEnumerable<BigInteger> Divisors(BigInteger n)
    {
        if (n <= 0) yield break;
        if (n == BigInteger.One) { yield return BigInteger.One; yield break; }
        if (n > 1_000_000) { yield return BigInteger.One; yield return n; yield break; }
        long v = (long)n;
        for (long i = 1; i * i <= v; i++)
        {
            if (v % i == 0)
            {
                yield return i;
                if (i != v / i) yield return v / i;
            }
        }
    }

    /// <summary>
    /// (EN) Solves an n×n rational linear system by Gauss-Jordan elimination with normalization.
    /// (ZH) 用归一化高斯-约当消元求解 n×n 有理线性方程组。
    /// </summary>
    private static bool SolveLinear(List<List<Rational>> matrix, List<Rational> rhs, out List<Rational> solution)
    {
        solution = new List<Rational>();
        int n = rhs.Count;
        for (int col = 0; col < n; col++)
        {
            int pivot = -1;
            for (int row = col; row < n; row++)
                if (!matrix[row][col].IsZero) { pivot = row; break; }
            if (pivot < 0) return false;
            (matrix[col], matrix[pivot]) = (matrix[pivot], matrix[col]);
            (rhs[col], rhs[pivot]) = (rhs[pivot], rhs[col]);

            var pv = matrix[col][col];
            for (int c = 0; c < n; c++) matrix[col][c] /= pv;
            rhs[col] /= pv;

            for (int row = 0; row < n; row++)
            {
                if (row == col || matrix[row][col].IsZero) continue;
                var factor = matrix[row][col];
                for (int c = 0; c < n; c++) matrix[row][c] -= factor * matrix[col][c];
                rhs[row] -= factor * rhs[col];
            }
        }
        solution = rhs;
        return true;
    }
}
