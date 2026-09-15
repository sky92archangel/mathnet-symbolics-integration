// ----------------------------------------------------------------------------
// (EN) Purpose: Integrates integer powers and products of trigonometric and
//       hyperbolic functions (sin^m·cos^n, tan^m·sec^n, sinh^m·cosh^n, ...) using
//       the standard substitution and reduction-formula techniques.
// (ZH) 用途：使用标准的换元与递推公式，积分三角/双曲函数的整数次幂与乘积
//       （sin^m·cos^n、tan^m·sec^n、sinh^m·cosh^n 等）。
// (EN) Notes: Every routine returns an exact symbolic antiderivative in the
//       integration variable; unsupported forms make TryIntegrate return false.
// (ZH) 说明：每个例程都返回关于积分变量的精确符号原函数；不支持的形式使 TryIntegrate 返回 false。
// ----------------------------------------------------------------------------

using System.Numerics;
using MathNet.Symbolics.Integration.Core;
using static MathNet.Symbolics.Integration.Core.Operators;

namespace MathNet.Symbolics.Integration;

/// <summary>
/// (EN) Integrator for integer powers/products of trig and hyperbolic functions.
/// (ZH) 三角与双曲函数整数次幂/乘积的积分器。
/// </summary>
internal static class TrigIntegrals
{
    /// <summary>
    /// (EN) Largest supported exponent per function. Bounds the reduction recursion depth (and thus
    ///      the stack) while still covering every practical integrand.
    /// (ZH) 每个函数支持的最大指数。它同时限制递推深度（即栈深度），仍足以覆盖所有实用被积式。
    /// </summary>
    private const int MaxExponent = 100;

    /// <summary>
    /// (EN) Tries to integrate an expression made only of a constant factor and integer powers of a
    ///      single trig/hyperbolic family applied to the integration variable.
    /// (ZH) 尝试积分仅由常数因子与某个单一三角/双曲函数族的整数次幂（自变量为积分变量）构成的表达式。
    /// </summary>
    /// <param name="integrand">(EN) Expression to integrate. (ZH) 待积分的表达式。</param>
    /// <param name="x">(EN) Integration variable. (ZH) 积分变量。</param>
    /// <param name="result">(EN) The antiderivative on success. (ZH) 成功时为原函数。</param>
    /// <returns>(EN) True when the form was recognized and integrated. (ZH) 识别并完成积分时为 true。</returns>
    public static bool TryIntegrate(Expression integrand, Expression x, out Expression result)
    {
        result = null!;
        var factors = Algebraic.Factors(integrand);
        Expression coeff = One;
        var exps = new Dictionary<FunctionType, int>();
        Family family = Family.None;

        foreach (var f in factors)
        {
            if (!Structure.ContainsVariable(f, x))
            {
                coeff = Multiply(coeff, f);
                continue;
            }

            var (bas, exp) = Algebraic.AsPower(f);
            if (bas is not Expression.Function fn || !fn.Argument.Equals(x)) return false;
            if (exp is not Expression.Number { Value: var r } || !r.IsInteger || r.Numerator < 0) return false;
            // (EN) Cap the exponent: the reduction routines recurse O(exponent) deep, so unbounded
            //      exponents would overflow the stack. (ZH) 限制指数：递推深度与指数同阶，指数过大将导致栈溢出。
            if (r.Numerator > MaxExponent) return false;

            var fam = FamilyOf(fn.Op);
            if (fam == Family.None) return false;
            if (family == Family.None) family = fam;
            else if (family != fam) return false;

            int e = exps.GetValueOrDefault(fn.Op) + r.ToInt32();
            if (e > MaxExponent) return false;
            exps[fn.Op] = e;
        }

        if (family == Family.None) return false;

        Expression? body = family switch
        {
            Family.SinCos => SinCos(exps.GetValueOrDefault(FunctionType.Sin),
                                    exps.GetValueOrDefault(FunctionType.Cos), x),
            Family.TanSec => TanSec(exps.GetValueOrDefault(FunctionType.Tan),
                                    exps.GetValueOrDefault(FunctionType.Sec), x),
            Family.CotCsc => CotCsc(exps.GetValueOrDefault(FunctionType.Cot),
                                    exps.GetValueOrDefault(FunctionType.Csc), x),
            Family.SinhCosh => SinhCosh(exps.GetValueOrDefault(FunctionType.Sinh),
                                        exps.GetValueOrDefault(FunctionType.Cosh), x),
            Family.TanhSech => TanhSech(exps.GetValueOrDefault(FunctionType.Tanh),
                                        exps.GetValueOrDefault(FunctionType.Sech), x),
            Family.CothCsch => CothCsch(exps.GetValueOrDefault(FunctionType.Coth),
                                        exps.GetValueOrDefault(FunctionType.Csch), x),
            _ => null,
        };

        if (body is null) return false;
        result = Multiply(coeff, body);
        return true;
    }

    /// <summary>
    /// (EN) Trig/hyperbolic family of a function; powers may only be combined within one family.
    /// (ZH) 函数的三角/双曲族别；幂只能在同一族内合并。
    /// </summary>
    private enum Family { None, SinCos, TanSec, CotCsc, SinhCosh, TanhSech, CothCsch }

    /// <summary>
    /// (EN) Maps a function tag to its power-integration family. (ZH) 将函数标签映射到其幂积分族别。
    /// </summary>
    private static Family FamilyOf(FunctionType op) => op switch
    {
        FunctionType.Sin or FunctionType.Cos => Family.SinCos,
        FunctionType.Tan or FunctionType.Sec => Family.TanSec,
        FunctionType.Cot or FunctionType.Csc => Family.CotCsc,
        FunctionType.Sinh or FunctionType.Cosh => Family.SinhCosh,
        FunctionType.Tanh or FunctionType.Sech => Family.TanhSech,
        FunctionType.Coth or FunctionType.Csch => Family.CothCsch,
        _ => Family.None,
    };

    // ── sin^m·cos^n ──────────────────────────────────────────────

    /// <summary>
    /// (EN) ∫ sin^m(x)·cos^n(x) dx for non-negative integers m, n. Odd exponents use u=cos or u=sin;
    ///      the all-even case uses the standard reduction formula.
    /// (ZH) ∫ sin^m(x)·cos^n(x) dx，m、n 为非负整数。奇数指数用 u=cos 或 u=sin 换元；全偶数情形用
    ///      标准递推公式。
    /// </summary>
    private static Expression SinCos(int m, int n, Expression x)
    {
        if (m == 0 && n == 0) return x;

        if (m % 2 == 1)
        {
            // (EN) u = cos(x): ∫ = -∫(1-u²)^((m-1)/2)·u^n du. (ZH) u = cos(x)：∫ = -∫(1-u²)^((m-1)/2)·u^n du。
            var u = Cos(x);
            return Negate(PolyIntegrate(1, -1, (m - 1) / 2, n, u));
        }
        if (n % 2 == 1)
        {
            // (EN) u = sin(x): ∫ = ∫u^m·(1-u²)^((n-1)/2) du. (ZH) u = sin(x)：∫ = ∫u^m·(1-u²)^((n-1)/2) du。
            var u = Sin(x);
            return PolyIntegrate(1, -1, (n - 1) / 2, m, u);
        }
        if (n >= 2)
        {
            // (EN) ∫sin^m cos^n = sin^(m+1)cos^(n-1)/(m+n) + (n-1)/(m+n)·∫sin^m cos^(n-2). (ZH) 递推公式。
            var term = Divide(Multiply(Pow(Sin(x), m + 1), Pow(Cos(x), n - 1)), m + n);
            return Add(term, Multiply(Divide(n - 1, m + n), SinCos(m, n - 2, x)));
        }
        // (EN) n == 0: ∫sin^m = -(1/m)·sin^(m-1)cos + (m-1)/m·∫sin^(m-2). (ZH) n == 0 时的幂递推。
        var t = Negate(Divide(Multiply(Pow(Sin(x), m - 1), Cos(x)), m));
        return Add(t, Multiply(Divide(m - 1, m), SinCos(m - 2, 0, x)));
    }

    // ── sinh^m·cosh^n ────────────────────────────────────────────

    /// <summary>
    /// (EN) ∫ sinh^m(x)·cosh^n(x) dx for non-negative integers m, n (analogous to SinCos).
    /// (ZH) ∫ sinh^m(x)·cosh^n(x) dx，m、n 为非负整数（与 SinCos 类似）。
    /// </summary>
    private static Expression SinhCosh(int m, int n, Expression x)
    {
        if (m == 0 && n == 0) return x;

        if (m % 2 == 1)
        {
            // (EN) u = cosh(x): ∫ = ∫(u²-1)^((m-1)/2)·u^n du. (ZH) u = cosh(x)：∫ = ∫(u²-1)^((m-1)/2)·u^n du。
            var u = Cosh(x);
            return PolyIntegrate(-1, 1, (m - 1) / 2, n, u);
        }
        if (n % 2 == 1)
        {
            // (EN) u = sinh(x): ∫ = ∫u^m·(1+u²)^((n-1)/2) du. (ZH) u = sinh(x)：∫ = ∫u^m·(1+u²)^((n-1)/2) du。
            var u = Sinh(x);
            return PolyIntegrate(1, 1, (n - 1) / 2, m, u);
        }
        if (n >= 2)
        {
            // (EN) ∫sinh^m cosh^n = sinh^(m+1)cosh^(n-1)/(m+n) + (n-1)/(m+n)·∫sinh^m cosh^(n-2). (ZH) 递推公式。
            var term = Divide(Multiply(Pow(Sinh(x), m + 1), Pow(Cosh(x), n - 1)), m + n);
            return Add(term, Multiply(Divide(n - 1, m + n), SinhCosh(m, n - 2, x)));
        }
        // (EN) n == 0: ∫sinh^m = sinh^(m-1)cosh/m - (m-1)/m·∫sinh^(m-2). (ZH) n == 0 时的幂递推。
        var t = Divide(Multiply(Pow(Sinh(x), m - 1), Cosh(x)), m);
        return Subtract(t, Multiply(Divide(m - 1, m), SinhCosh(m - 2, 0, x)));
    }

    // ── tan^m·sec^n ──────────────────────────────────────────────

    /// <summary>
    /// (EN) ∫ tan^m(x)·sec^n(x) dx. Even sec powers use u=tan, odd tan powers use u=sec, and the
    ///      remaining case expands (sec²-1)^(m/2) and uses the secant reduction formula.
    /// (ZH) ∫ tan^m(x)·sec^n(x) dx。偶数 sec 幂用 u=tan，奇数 tan 幂用 u=sec，其余情形展开
    ///      (sec²-1)^(m/2) 并使用正割递推公式。
    /// </summary>
    private static Expression TanSec(int m, int n, Expression x)
    {
        if (m == 0 && n == 0) return x;
        if (m == 0) return SecReduce(n, x);
        if (n == 0) return TanReduce(m, x);

        if (n % 2 == 0)
        {
            // (EN) u = tan(x): ∫ = ∫u^m·(1+u²)^((n-2)/2) du. (ZH) u = tan(x)：∫ = ∫u^m·(1+u²)^((n-2)/2) du。
            var u = Tan(x);
            return PolyIntegrate(1, 1, (n - 2) / 2, m, u);
        }
        if (m % 2 == 1)
        {
            // (EN) u = sec(x): ∫ = ∫(u²-1)^((m-1)/2)·u^(n-1) du. (ZH) u = sec(x)：∫ = ∫(u²-1)^((m-1)/2)·u^(n-1) du。
            var u = Sec(x);
            return PolyIntegrate(-1, 1, (m - 1) / 2, n - 1, u);
        }

        // (EN) m even, n odd: tan^m = (sec²-1)^(m/2) → sum of secant powers. (ZH) m 偶、n 奇：tan^m = (sec²-1)^(m/2) → sec 幂之和。
        Expression sum = Zero;
        int h = m / 2;
        for (int j = 0; j <= h; j++)
        {
            var c = Comb(h, j);
            if ((h - j) % 2 == 1) c = -c;
            sum = Add(sum, Multiply(Expression.Integer(c), SecReduce(n + 2 * j, x)));
        }
        return sum;
    }

    // ── cot^m·csc^n ──────────────────────────────────────────────

    /// <summary>
    /// (EN) ∫ cot^m(x)·csc^n(x) dx (mirror of TanSec with the cot/csc identities).
    /// (ZH) ∫ cot^m(x)·csc^n(x) dx（利用 cot/csc 恒等式，与 TanSec 镜像）。
    /// </summary>
    private static Expression CotCsc(int m, int n, Expression x)
    {
        if (m == 0 && n == 0) return x;
        if (m == 0) return CscReduce(n, x);
        if (n == 0) return CotReduce(m, x);

        if (n % 2 == 0)
        {
            // (EN) u = cot(x), csc²dx = -du: ∫ = -∫u^m·(1+u²)^((n-2)/2) du. (ZH) u = cot(x)，csc²dx = -du：∫ = -∫u^m·(1+u²)^((n-2)/2) du。
            var u = Cot(x);
            return Negate(PolyIntegrate(1, 1, (n - 2) / 2, m, u));
        }
        if (m % 2 == 1)
        {
            // (EN) u = csc(x), cot·csc^n dx = -u^(n-1) du: ∫ = -∫(u²-1)^((m-1)/2)·u^(n-1) du. (ZH) u = csc(x)：∫ = -∫(u²-1)^((m-1)/2)·u^(n-1) du。
            var u = Csc(x);
            return Negate(PolyIntegrate(-1, 1, (m - 1) / 2, n - 1, u));
        }

        // (EN) m even, n odd: cot^m = (csc²-1)^(m/2) → sum of cosecant powers. (ZH) m 偶、n 奇：cot^m = (csc²-1)^(m/2) → csc 幂之和。
        Expression sum = Zero;
        int h = m / 2;
        for (int j = 0; j <= h; j++)
        {
            var c = Comb(h, j);
            if ((h - j) % 2 == 1) c = -c;
            sum = Add(sum, Multiply(Expression.Integer(c), CscReduce(n + 2 * j, x)));
        }
        return sum;
    }

    // ── tanh^m·sech^n ────────────────────────────────────────────

    /// <summary>
    /// (EN) ∫ tanh^m(x)·sech^n(x) dx. (ZH) ∫ tanh^m(x)·sech^n(x) dx。
    /// </summary>
    private static Expression TanhSech(int m, int n, Expression x)
    {
        if (m == 0 && n == 0) return x;
        if (m == 0) return SechReduce(n, x);
        if (n == 0) return TanhReduce(m, x);

        if (n % 2 == 0)
        {
            // (EN) u = tanh(x), sech²dx = du: ∫ = ∫u^m·(1-u²)^((n-2)/2) du. (ZH) u = tanh(x)：∫ = ∫u^m·(1-u²)^((n-2)/2) du。
            var u = Tanh(x);
            return PolyIntegrate(1, -1, (n - 2) / 2, m, u);
        }
        if (m % 2 == 1)
        {
            // (EN) u = sech(x), tanh·sech^n dx = -u^(n-1) du: ∫ = -∫(1-u²)^((m-1)/2)·u^(n-1) du. (ZH) u = sech(x)：∫ = -∫(1-u²)^((m-1)/2)·u^(n-1) du。
            var u = Sech(x);
            return Negate(PolyIntegrate(1, -1, (m - 1) / 2, n - 1, u));
        }

        // (EN) m even, n odd: tanh^m = (1-sech²)^(m/2) → sum of sech powers. (ZH) m 偶、n 奇：tanh^m = (1-sech²)^(m/2) → sech 幂之和。
        Expression sum = Zero;
        int h = m / 2;
        for (int j = 0; j <= h; j++)
        {
            var c = Comb(h, j);
            if (j % 2 == 1) c = -c;
            sum = Add(sum, Multiply(Expression.Integer(c), SechReduce(n + 2 * j, x)));
        }
        return sum;
    }

    // ── coth^m·csch^n ────────────────────────────────────────────

    /// <summary>
    /// (EN) ∫ coth^m(x)·csch^n(x) dx. (ZH) ∫ coth^m(x)·csch^n(x) dx。
    /// </summary>
    private static Expression CothCsch(int m, int n, Expression x)
    {
        if (m == 0 && n == 0) return x;
        if (m == 0) return CschReduce(n, x);
        if (n == 0) return CothReduce(m, x);

        if (n % 2 == 0)
        {
            // (EN) u = coth(x), csch²dx = -du: ∫ = -∫u^m·(u²-1)^((n-2)/2) du. (ZH) u = coth(x)：∫ = -∫u^m·(u²-1)^((n-2)/2) du。
            var u = Coth(x);
            return Negate(PolyIntegrate(-1, 1, (n - 2) / 2, m, u));
        }
        if (m % 2 == 1)
        {
            // (EN) u = csch(x), coth·csch^n dx = -u^(n-1) du; coth² = 1+csch². (ZH) u = csch(x)：coth² = 1+csch²。
            var u = Csch(x);
            return Negate(PolyIntegrate(1, 1, (m - 1) / 2, n - 1, u));
        }

        // (EN) m even, n odd: coth^m = (1+csch²)^(m/2) → sum of csch powers. (ZH) m 偶、n 奇：coth^m = (1+csch²)^(m/2) → csch 幂之和。
        Expression sum = Zero;
        int h = m / 2;
        for (int j = 0; j <= h; j++)
            sum = Add(sum, Multiply(Expression.Integer(Comb(h, j)), CschReduce(n + 2 * j, x)));
        return sum;
    }

    // ── Reduction formulas for single-function powers ────────────

    /// <summary>(EN) ∫ tan^m dx = tan^(m-1)/(m-1) - ∫ tan^(m-2) dx. (ZH) 正切幂递推。</summary>
    private static Expression TanReduce(int m, Expression x)
    {
        if (m == 0) return x;
        if (m == 1) return Negate(Ln(Cos(x)));
        return Subtract(Divide(Pow(Tan(x), m - 1), m - 1), TanReduce(m - 2, x));
    }

    /// <summary>(EN) ∫ cot^m dx = -cot^(m-1)/(m-1) - ∫ cot^(m-2) dx. (ZH) 余切幂递推。</summary>
    private static Expression CotReduce(int m, Expression x)
    {
        if (m == 0) return x;
        if (m == 1) return Ln(Sin(x));
        return Subtract(Negate(Divide(Pow(Cot(x), m - 1), m - 1)), CotReduce(m - 2, x));
    }

    /// <summary>(EN) ∫ sec^n dx = sec^(n-2)tan/(n-1) + (n-2)/(n-1)·∫ sec^(n-2) dx. (ZH) 正割幂递推。</summary>
    private static Expression SecReduce(int n, Expression x)
    {
        if (n == 0) return x;
        if (n == 1) return Ln(Sec(x) + Tan(x));
        var term = Divide(Multiply(Pow(Sec(x), n - 2), Tan(x)), n - 1);
        return Add(term, Multiply(Divide(n - 2, n - 1), SecReduce(n - 2, x)));
    }

    /// <summary>(EN) ∫ csc^n dx = -csc^(n-2)cot/(n-1) + (n-2)/(n-1)·∫ csc^(n-2) dx. (ZH) 余割幂递推。</summary>
    private static Expression CscReduce(int n, Expression x)
    {
        if (n == 0) return x;
        if (n == 1) return Negate(Ln(Csc(x) + Cot(x)));
        var term = Negate(Divide(Multiply(Pow(Csc(x), n - 2), Cot(x)), n - 1));
        return Add(term, Multiply(Divide(n - 2, n - 1), CscReduce(n - 2, x)));
    }

    /// <summary>(EN) ∫ tanh^m dx = -tanh^(m-1)/(m-1) + ∫ tanh^(m-2) dx. (ZH) 双曲正切幂递推。</summary>
    private static Expression TanhReduce(int m, Expression x)
    {
        if (m == 0) return x;
        if (m == 1) return Ln(Cosh(x));
        return Subtract(Negate(Divide(Pow(Tanh(x), m - 1), m - 1)), Negate(TanhReduce(m - 2, x)));
    }

    /// <summary>(EN) ∫ coth^m dx = -coth^(m-1)/(m-1) + ∫ coth^(m-2) dx. (ZH) 双曲余切幂递推。</summary>
    private static Expression CothReduce(int m, Expression x)
    {
        if (m == 0) return x;
        if (m == 1) return Ln(Sinh(x));
        return Subtract(Negate(Divide(Pow(Coth(x), m - 1), m - 1)), Negate(CothReduce(m - 2, x)));
    }

    /// <summary>(EN) ∫ sech^n dx = sech^(n-2)tanh/(n-1) + (n-2)/(n-1)·∫ sech^(n-2) dx. (ZH) 双曲正割幂递推。</summary>
    private static Expression SechReduce(int n, Expression x)
    {
        if (n == 0) return x;
        if (n == 1) return Multiply(Two, Atan(Exp(x)));
        var term = Divide(Multiply(Pow(Sech(x), n - 2), Tanh(x)), n - 1);
        return Add(term, Multiply(Divide(n - 2, n - 1), SechReduce(n - 2, x)));
    }

    /// <summary>(EN) ∫ csch^n dx = -csch^(n-2)coth/(n-1) + (n-2)/(n-1)·∫ csch^(n-2) dx. (ZH) 双曲余割幂递推。</summary>
    private static Expression CschReduce(int n, Expression x)
    {
        if (n == 0) return x;
        if (n == 1) return Ln(Tanh(Divide(x, Two)));
        var term = Negate(Divide(Multiply(Pow(Csch(x), n - 2), Coth(x)), n - 1));
        return Add(term, Multiply(Divide(n - 2, n - 1), CschReduce(n - 2, x)));
    }

    // ── Polynomial helper ────────────────────────────────────────

    /// <summary>
    /// (EN) Integrates u^extra·(s + t·u²)^k du term by term after binomial expansion, where s and t
    ///      are ±1. Each term a·u^p integrates to a·u^(p+1)/(p+1).
    /// (ZH) 二项式展开后逐项积分 u^extra·(s + t·u²)^k du，其中 s、t 为 ±1。每项 a·u^p 积分为
    ///      a·u^(p+1)/(p+1)。
    /// </summary>
    private static Expression PolyIntegrate(int s, int t, int k, int extra, Expression u)
    {
        Expression sum = Zero;
        for (int j = 0; j <= k; j++)
        {
            // (EN) Coefficient C(k,j)·s^(k-j)·t^j. (ZH) 系数 C(k,j)·s^(k-j)·t^j。
            var c = Comb(k, j);
            if (s < 0 && (k - j) % 2 == 1) c = -c;
            if (t < 0 && j % 2 == 1) c = -c;
            int power = 2 * j + extra;
            var term = Divide(Multiply(Expression.Integer(c), Pow(u, power + 1)), power + 1);
            sum = Add(sum, term);
        }
        return sum;
    }

    /// <summary>
    /// (EN) Binomial coefficient C(n, k) as a BigInteger. (ZH) 二项式系数 C(n, k)（BigInteger）。
    /// </summary>
    private static BigInteger Comb(int n, int k)
    {
        if (k < 0 || k > n) return BigInteger.Zero;
        BigInteger result = BigInteger.One;
        for (int i = 0; i < k; i++)
            result = result * (n - i) / (i + 1);
        return result;
    }
}
