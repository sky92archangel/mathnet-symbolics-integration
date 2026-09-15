// ----------------------------------------------------------------------------
// (EN) Purpose: General symbolic differentiation used by the integration solver to
//       recognize derivatives inside integrands (u-substitution and integration by
//       parts). Supports sums, the product rule, the power/generalized power rule,
//       the chain rule and a table of elementary/special-function derivatives.
// (ZH) 用途：积分求解器使用的通用符号微分，用于识别被积表达式中的导数（换元积分与
//       分部积分）。支持和式、乘积法则、幂与广义幂法则、链式法则，以及初等/特殊函数
//       的导数表。
// (EN) Notes: Returns null for nodes whose derivative is unknown, so callers can
//       safely abandon the strategy instead of producing a wrong derivative.
// (ZH) 说明：对未知导数的节点返回 null，调用方据此安全地放弃该策略，而不会得到错误的导数。
// ----------------------------------------------------------------------------

using static MathNet.Symbolics.Integration.Core.Operators;

namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// (EN) Symbolic differentiation engine for the integration solver.
/// (ZH) 供积分求解器使用的符号微分引擎。
/// </summary>
internal static class Differentiate
{
    /// <summary>
    /// (EN) Differentiates <paramref name="expr"/> with respect to <paramref name="v"/>.
    ///      Returns null when a derivative rule is missing.
    /// (ZH) 求 <paramref name="expr"/> 关于 <paramref name="v"/> 的导数。缺少导数规则时返回 null。
    /// </summary>
    /// <param name="expr">(EN) Expression to differentiate. (ZH) 待求导的表达式。</param>
    /// <param name="v">(EN) Differentiation variable. (ZH) 求导变量。</param>
    /// <returns>(EN) The derivative, or null if unknown. (ZH) 导数；未知时为 null。</returns>
    public static Expression? Diff(Expression expr, Expression v)
    {
        // (EN) Nodes independent of v differentiate to zero. (ZH) 与 v 无关的节点导数为零。
        if (!Structure.ContainsVariable(expr, v))
            return Zero;

        if (expr is Expression.SymbolExpr) return One;      // (EN) d/dx x = 1. (ZH) d/dx x = 1。
        if (expr is Expression.Number or Expression.Constant
            or Expression.Approximation or Expression.Infinity or Expression.Undefined)
            return Zero;

        switch (expr)
        {
            case Expression.Sum sum:
                return DiffSum(sum, v);

            case Expression.Product prod:
                return DiffProduct(prod, v);

            case Expression.Power power:
                return DiffPower(power.Base, power.Exponent, v);

            case Expression.Function fn:
                return DiffFunction(fn, v);

            case Expression.FunctionN fnN:
                return DiffFunctionN(fnN, v);

            default:
                return null;
        }
    }

    /// <summary>
    /// (EN) Term-by-term differentiation of a sum.
    /// (ZH) 对和式逐项求导。
    /// </summary>
    private static Expression? DiffSum(Expression.Sum sum, Expression v)
    {
        Expression? result = null;
        foreach (var term in sum.Terms)
        {
            var d = Diff(term, v);
            if (d is null) return null;
            result = result is null ? d : Add(result, d);
        }
        return result ?? Zero;
    }

    /// <summary>
    /// (EN) Product rule over any number of factors: (f1·f2·…)' = Σ_i f_i'·∏_{j≠i} f_j.
    /// (ZH) 任意因子个数的乘积法则：(f1·f2·…)' = Σ_i f_i'·∏_{j≠i} f_j。
    /// </summary>
    private static Expression? DiffProduct(Expression.Product prod, Expression v)
    {
        var factors = prod.Factors;
        Expression? total = null;

        for (int i = 0; i < factors.Count; i++)
        {
            var di = Diff(factors[i], v);
            if (di is null) return null;

            Expression? term = di;
            for (int j = 0; j < factors.Count; j++)
            {
                if (j == i) continue;
                term = Multiply(term!, factors[j]);
            }
            total = total is null ? term : Add(total, term!);
        }
        return total ?? Zero;
    }

    /// <summary>
    /// (EN) Power rule in its three forms:
    ///      (1) constant exponent n: d/dx f^n = n·f^(n-1)·f';
    ///      (2) constant base b:     d/dx b^g = b^g·ln(b)·g';
    ///      (3) general:             d/dx f^g = f^g·(g'·ln f + g·f'/f).
    /// (ZH) 幂法则的三种形式：
    ///      (1) 指数为常数 n：d/dx f^n = n·f^(n-1)·f'；
    ///      (2) 底为常数 b：   d/dx b^g = b^g·ln(b)·g'；
    ///      (3) 一般情形：      d/dx f^g = f^g·(g'·ln f + g·f'/f)。
    /// </summary>
    private static Expression? DiffPower(Expression bas, Expression exp, Expression v)
    {
        bool baseConst = !Structure.ContainsVariable(bas, v);
        bool expConst = !Structure.ContainsVariable(exp, v);

        if (expConst)
        {
            // (EN) n·f^(n-1)·f'. (ZH) n·f^(n-1)·f'。
            var df = Diff(bas, v);
            if (df is null) return null;
            var n = exp;
            return Multiply(Multiply(n, Pow(bas, Subtract(n, One))), df);
        }

        if (baseConst)
        {
            // (EN) b^g·ln(b)·g'. (ZH) b^g·ln(b)·g'。
            var dg = Diff(exp, v);
            if (dg is null) return null;
            return Multiply(Multiply(Pow(bas, exp), Ln(bas)), dg);
        }

        // (EN) General case f^g·(g'·ln f + g·f'/f). (ZH) 一般情形 f^g·(g'·ln f + g·f'/f)。
        var df2 = Diff(bas, v);
        var dg2 = Diff(exp, v);
        if (df2 is null || dg2 is null) return null;
        var bracket = Add(Multiply(dg2, Ln(bas)), Multiply(exp, Divide(df2, bas)));
        return Multiply(Pow(bas, exp), bracket);
    }

    /// <summary>
    /// (EN) Chain rule for unary functions: d/dx f(u) = f'(u)·u', with f' taken from the table below.
    /// (ZH) 一元函数的链式法则：d/dx f(u) = f'(u)·u'，其中 f' 取自下表。
    /// </summary>
    private static Expression? DiffFunction(Expression.Function fn, Expression v)
    {
        var u = fn.Argument;
        var du = Diff(u, v);
        if (du is null) return null;

        Expression? outer = fn.Op switch
        {
            // (EN) Logarithmic and exponential. (ZH) 对数与指数。
            FunctionType.Ln => Divide(One, u),
            FunctionType.Lg => Divide(One, Multiply(u, Ln(Number(10)))),
            FunctionType.Exp => Exp(u),

            // (EN) Absolute value: sign(u) = u/|u|. (ZH) 绝对值：符号函数 sign(u) = u/|u|。
            FunctionType.Abs => Divide(u, Abs(u)),

            // (EN) Trigonometric. (ZH) 三角函数。
            FunctionType.Sin => Cos(u),
            FunctionType.Cos => Negate(Sin(u)),
            FunctionType.Tan => Pow(Sec(u), Two),
            FunctionType.Cot => Negate(Pow(Csc(u), Two)),
            FunctionType.Sec => Multiply(Sec(u), Tan(u)),
            FunctionType.Csc => Negate(Multiply(Csc(u), Cot(u))),

            // (EN) Inverse trigonometric. (ZH) 反三角函数。
            FunctionType.Asin => Divide(One, Sqrt(One - Pow(u, Two))),
            FunctionType.Acos => Negate(Divide(One, Sqrt(One - Pow(u, Two)))),
            FunctionType.Atan => Divide(One, One + Pow(u, Two)),
            FunctionType.Acot => Negate(Divide(One, One + Pow(u, Two))),

            // (EN) Hyperbolic. (ZH) 双曲函数。
            FunctionType.Sinh => Cosh(u),
            FunctionType.Cosh => Sinh(u),
            FunctionType.Tanh => Pow(Sech(u), Two),
            FunctionType.Coth => Negate(Pow(Csch(u), Two)),
            FunctionType.Sech => Negate(Multiply(Sech(u), Tanh(u))),
            FunctionType.Csch => Negate(Multiply(Csch(u), Coth(u))),

            // (EN) Inverse hyperbolic. (ZH) 反双曲函数。
            FunctionType.Asinh => Divide(One, Sqrt(Pow(u, Two) + One)),
            FunctionType.Acosh => Divide(One, Sqrt(Pow(u, Two) - One)),
            FunctionType.Atanh => Divide(One, One - Pow(u, Two)),

            // (EN) Error functions. (ZH) 误差函数。
            FunctionType.Erf => Multiply(Divide(Two, Sqrt(Pi)), Exp(Negate(Pow(u, Two)))),
            FunctionType.Erfc => Negate(Multiply(Divide(Two, Sqrt(Pi)), Exp(Negate(Pow(u, Two))))),
            FunctionType.Erfi => Multiply(Divide(Two, Sqrt(Pi)), Exp(Pow(u, Two))),

            // (EN) Special integrals (the integrand that each one inverts). (ZH) 特殊积分函数（各自反演的 integrand）。
            FunctionType.Si => Divide(Sin(u), u),
            FunctionType.Ci => Divide(Cos(u), u),
            FunctionType.Shi => Divide(Sinh(u), u),
            FunctionType.Chi => Divide(Cosh(u), u),
            FunctionType.Ei => Divide(Exp(u), u),
            FunctionType.Li => Divide(One, Ln(u)),

            // (EN) Fresnel integrals. (ZH) 菲涅耳积分。
            FunctionType.FresnelS => Sin(Multiply(Divide(Pi, Two), Pow(u, Two))),
            FunctionType.FresnelC => Cos(Multiply(Divide(Pi, Two), Pow(u, Two))),

            _ => null,
        };

        if (outer is null) return null;
        return Multiply(outer, du);
    }

    /// <summary>
    /// (EN) Differentiation of selected n-ary functions (currently the two-argument logarithm).
    /// (ZH) 部分多元函数的微分（目前为双参数对数）。
    /// </summary>
    private static Expression? DiffFunctionN(Expression.FunctionN fn, Expression v)
    {
        if (fn.Op == FunctionNType.Log && fn.Arguments.Count == 2)
        {
            var basis = fn.Arguments[0];
            var u = fn.Arguments[1];
            // (EN) Only handle a constant base: d/dx log_b(u) = u' / (u·ln b). (ZH) 只处理常数底：d/dx log_b(u) = u' / (u·ln b)。
            if (Structure.ContainsVariable(basis, v)) return null;
            var du = Diff(u, v);
            if (du is null) return null;
            return Multiply(Divide(One, Multiply(u, Ln(basis))), du);
        }
        return null;
    }
}
