using System.Numerics;

namespace MathNet.Symbolics.Integration.Core;

/// <summary>Expression construction and arithmetic operators.</summary>
public static class Operators
{
    // ── Constants ─────────────────────────────

    public static Expression Zero => Expression.Zero;
    public static Expression One => Expression.One;
    public static Expression MinusOne => Expression.MinusOne;
    public static Expression Two => Expression.Two;

    public static Expression E => new Expression.Constant(ConstantType.E);
    public static Expression Pi => new Expression.Constant(ConstantType.Pi);
    public static Expression I => new Expression.Constant(ConstantType.I);

    public static Expression Number(int value) => Expression.Int32(value);
    public static Expression Number(Rational value) => new Expression.Number(value);
    public static Expression Number(double value) =>
        new Expression.Approximation(value, 0.0);

    /// <summary>Create a named symbol.</summary>
    public static Expression Symbol(string name) => new Expression.SymbolExpr(name);

    // ── Arithmetic (with basic normalization) ─

    public static Expression Add(Expression x, Expression y)
    {
        // Identity elements
        if (Expression.IsZero(x)) return y;
        if (Expression.IsZero(y)) return x;

        // Number + Number
        if (x is Expression.Number nx && y is Expression.Number ny)
            return new Expression.Number(nx.Value + ny.Value);

        // Flatten nested sums + collect constant terms
        var terms = new List<Expression>();
        Rational? constSum = null;
        foreach (var t in FlattenSumTerms(x))
        {
            if (t is Expression.Number n)
                constSum = (constSum ?? Rational.Zero) + n.Value;
            else
                terms.Add(t);
        }
        foreach (var t in FlattenSumTerms(y))
        {
            if (t is Expression.Number n)
                constSum = (constSum ?? Rational.Zero) + n.Value;
            else
                terms.Add(t);
        }

        var result = BuildSumOrTerm(terms);
        if (constSum.HasValue && constSum.Value != Rational.Zero)
        {
            var constExpr = new Expression.Number(constSum.Value);
            if (result is Expression.Sum existingSum)
                result = new Expression.Sum(
                    new[] { constExpr }.Concat(existingSum.Terms).ToList());
            else
                result = BuildSumOrTerm(new[] { constExpr, result! });
        }
        return result!;
    }

    public static Expression Negate(Expression x)
    {
        if (x is Expression.Number n) return new Expression.Number(-n.Value);
        if (x is Expression.Product p && p.Factors[0] is Expression.Number fn)
            return new Expression.Product(
                new[] { new Expression.Number(-fn.Value) }
                    .Concat(p.Factors.Skip(1)).ToList());
        // -1 * x (raw product, no recursion)
        return new Expression.Product(new[] { MinusOne, x });
    }

    public static Expression Subtract(Expression x, Expression y) => Add(x, Negate(y));

    public static Expression Multiply(Expression x, Expression y)
    {
        if (Expression.IsZero(x) || Expression.IsZero(y)) return Zero;
        if (Expression.IsOne(x)) return y;
        if (Expression.IsOne(y)) return x;

        if (x is Expression.Number nx && y is Expression.Number ny)
            return new Expression.Number(nx.Value * ny.Value);

        // Flatten nested products + collect constant factors
        var factors = new List<Expression>();
        Rational? constMul = null;
        foreach (var f in FlattenProductFactors(x))
        {
            if (f is Expression.Number n)
                constMul = (constMul ?? Rational.One) * n.Value;
            else
                factors.Add(f);
        }
        foreach (var f in FlattenProductFactors(y))
        {
            if (f is Expression.Number n)
                constMul = (constMul ?? Rational.One) * n.Value;
            else
                factors.Add(f);
        }

        if (constMul.HasValue && constMul.Value.IsZero) return Zero;

        // Combine like factors: x*x → x^2, x^2*x → x^3
        factors = CombineLikeFactors(factors);

        var result = BuildProductOrTerm(factors);
        if (constMul.HasValue && constMul.Value != Rational.One)
        {
            if (constMul.Value.IsMinusOne)
            {
                // Direct negation, NOT calling Negate to avoid recursion
                if (result is Expression.Number rn)
                    result = new Expression.Number(-rn.Value);
                else
                    result = new Expression.Product(
                        new[] { MinusOne, result! });
            }
            else
            {
                result = new Expression.Product(
                    new[] { new Expression.Number(constMul.Value), result! });
            }
        }
        return result!;
    }

    public static Expression Divide(Expression x, Expression y)
    {
        if (Expression.IsOne(y)) return x;
        if (Expression.IsZero(y)) return new Expression.Infinity(InfinityType.ComplexInfinity);
        if (x.Equals(y) && !Expression.IsZero(x)) return One;
        return Multiply(x, new Expression.Power(y, MinusOne));
    }

    public static Expression Pow(Expression x, Expression y)
    {
        if (Expression.IsZero(y)) return One;
        if (Expression.IsOne(y)) return x;
        if (Expression.IsZero(x)) return Zero;
        if (Expression.IsOne(x)) return One;

        // Integer power of a number
        if (x is Expression.Number nx && y is Expression.Number ny && ny.Value.IsInteger)
        {
            int exp = ny.Value.ToInt32();
            return new Expression.Number(Rational.Pow(nx.Value, exp));
        }

        return new Expression.Power(x, y);
    }

    // ── Unary functions ──────────────────────

    public static Expression Sin(Expression x) => new Expression.Function(FunctionType.Sin, x);
    public static Expression Cos(Expression x) => new Expression.Function(FunctionType.Cos, x);
    public static Expression Tan(Expression x) => new Expression.Function(FunctionType.Tan, x);
    public static Expression Csc(Expression x) => new Expression.Function(FunctionType.Csc, x);
    public static Expression Sec(Expression x) => new Expression.Function(FunctionType.Sec, x);
    public static Expression Cot(Expression x) => new Expression.Function(FunctionType.Cot, x);

    public static Expression Sinh(Expression x) => new Expression.Function(FunctionType.Sinh, x);
    public static Expression Cosh(Expression x) => new Expression.Function(FunctionType.Cosh, x);
    public static Expression Tanh(Expression x) => new Expression.Function(FunctionType.Tanh, x);
    public static Expression Csch(Expression x) => new Expression.Function(FunctionType.Csch, x);
    public static Expression Sech(Expression x) => new Expression.Function(FunctionType.Sech, x);
    public static Expression Coth(Expression x) => new Expression.Function(FunctionType.Coth, x);

    public static Expression Asin(Expression x) => new Expression.Function(FunctionType.Asin, x);
    public static Expression Acos(Expression x) => new Expression.Function(FunctionType.Acos, x);
    public static Expression Atan(Expression x) => new Expression.Function(FunctionType.Atan, x);
    public static Expression Acsc(Expression x) => new Expression.Function(FunctionType.Acsc, x);
    public static Expression Asec(Expression x) => new Expression.Function(FunctionType.Asec, x);
    public static Expression Acot(Expression x) => new Expression.Function(FunctionType.Acot, x);

    public static Expression Asinh(Expression x) => new Expression.Function(FunctionType.Asinh, x);
    public static Expression Acosh(Expression x) => new Expression.Function(FunctionType.Acosh, x);
    public static Expression Atanh(Expression x) => new Expression.Function(FunctionType.Atanh, x);
    public static Expression Acsch(Expression x) => new Expression.Function(FunctionType.Acsch, x);
    public static Expression Asech(Expression x) => new Expression.Function(FunctionType.Asech, x);
    public static Expression Acoth(Expression x) => new Expression.Function(FunctionType.Acoth, x);

    public static Expression Exp(Expression x) => new Expression.Function(FunctionType.Exp, x);
    public static Expression Ln(Expression x) => new Expression.Function(FunctionType.Ln, x);
    public static Expression Lg(Expression x) => new Expression.Function(FunctionType.Lg, x);
    public static Expression Abs(Expression x) => new Expression.Function(FunctionType.Abs, x);

    public static Expression Sqrt(Expression x) => Pow(x, Expression.Half);

    // ── N‑ary functions ──────────────────────

    public static Expression Log(Expression basis, Expression x) =>
        new Expression.FunctionN(FunctionNType.Log, new[] { basis, x });
    public static Expression Atan2(Expression y, Expression x) =>
        new Expression.FunctionN(FunctionNType.Atan2, new[] { y, x });

    // ── Internal helpers ─────────────────────

    private static IEnumerable<Expression> FlattenSumTerms(Expression e) =>
        e is Expression.Sum s ? s.Terms.SelectMany(FlattenSumTerms) : new[] { e };

    private static IEnumerable<Expression> FlattenProductFactors(Expression e) =>
        e is Expression.Product p ? p.Factors.SelectMany(FlattenProductFactors) : new[] { e };

    /// <summary>Combine like factors: [x, x] → [x^2], [x, x^2] → [x^3].</summary>
    private static List<Expression> CombineLikeFactors(List<Expression> factors)
    {
        if (factors.Count <= 1) return factors;

        var groups = new Dictionary<Expression, int>();
        var others = new List<Expression>();

        foreach (var f in factors)
        {
            if (f is Expression.Power pw && !(pw.Base is Expression.Number))
            {
                // Already a power: try to combine base
                if (pw.Exponent is Expression.Number expN && expN.Value.IsInteger)
                {
                    if (groups.ContainsKey(pw.Base))
                        groups[pw.Base] += expN.Value.ToInt32();
                    else
                    {
                        // We'll look up non-power forms too
                        groups[pw.Base] = expN.Value.ToInt32();
                        // Remove from others if the plain form exists
                    }
                    continue;
                }
            }
            if (!(f is Expression.Number) && !(f is Expression.Constant))
            {
                if (groups.ContainsKey(f))
                    groups[f] += 1;
                else
                    groups[f] = 1;
                continue;
            }
            others.Add(f);
        }

        var result = new List<Expression>();
        Rational? numProduct = null;
        foreach (var kv in groups)
        {
            if (kv.Value == 1)
                result.Add(kv.Key);
            else if (kv.Value > 1)
                result.Add(new Expression.Power(kv.Key, Expression.Int32(kv.Value)));
            else if (kv.Value < 0)
                result.Add(new Expression.Power(kv.Key, Expression.Int32(kv.Value)));
            // value == 0 means x^0 = 1, skip
        }
        // Multiply all numeric factors together
        foreach (var f in others)
        {
            if (f is Expression.Number n)
                numProduct = (numProduct ?? Rational.One) * n.Value;
            else
                result.Add(f);
        }
        if (numProduct.HasValue && !numProduct.Value.IsOne)
            result.Insert(0, new Expression.Number(numProduct.Value));
        else if (numProduct.HasValue && numProduct.Value.IsOne && result.Count == 0)
            result.Add(One);
        return result;
    }

    private static Expression? BuildSumOrTerm(IReadOnlyList<Expression> terms) => terms.Count switch
    {
        0 => null,
        1 => terms[0],
        _ => new Expression.Sum(terms)
    };

    private static Expression? BuildProductOrTerm(IReadOnlyList<Expression> factors) => factors.Count switch
    {
        0 => null,
        1 => factors[0],
        _ => new Expression.Product(factors)
    };

    // ── Simplification ─────────────────────────

    /// <summary>Simplify an expression tree (flatten, combine constants, cancel identities).</summary>
    public static Expression Simplify(Expression expr)
    {
        // 1. Recursively simplify children
        var s = expr switch
        {
            Expression.Sum sum => SimplifySum(sum),
            Expression.Product prod => SimplifyProduct(prod),
            Expression.Power pw => SimplifyPower(Simplify(pw.Base), Simplify(pw.Exponent)),
            Expression.Function fn => new Expression.Function(fn.Op, Simplify(fn.Argument)),
            Expression.FunctionN fnn => new Expression.FunctionN(fnn.Op,
                fnn.Arguments.Select(Simplify).ToList()),
            _ => expr
        };
        return s;
    }

    private static Expression SimplifySum(Expression.Sum sum)
    {
        var simplified = sum.Terms.Select(Simplify).ToList();
        // Flatten nested sums + collect constants
        var terms = new List<Expression>();
        Rational? constSum = null;
        foreach (var t in simplified)
        {
            if (t is Expression.Sum nested)
                terms.AddRange(nested.Terms);
            else if (t is Expression.Number n)
                constSum = (constSum ?? Rational.Zero) + n.Value;
            else if (!Expression.IsZero(t))
                terms.Add(t);
        }
        if (constSum.HasValue && constSum.Value != Rational.Zero)
            terms.Insert(0, new Expression.Number(constSum.Value));

        return terms.Count switch
        {
            0 => Zero,
            1 => terms[0],
            _ => new Expression.Sum(terms)
        };
    }

    private static Expression SimplifyProduct(Expression.Product prod)
    {
        var simplified = prod.Factors.Select(Simplify).ToList();
        // Collect numeric factors, flatten nested products
        var factors = new List<Expression>();
        Rational? numProduct = null;
        int sign = 1;
        foreach (var f in simplified)
        {
            if (f is Expression.Product nested)
                factors.AddRange(nested.Factors);
            else if (f is Expression.Number n)
            {
                if (n.Value.IsMinusOne)
                    sign *= -1;
                else if (n.Value.IsZero)
                    return Zero;
                else
                    numProduct = (numProduct ?? Rational.One) * n.Value;
            }
            else if (f is Expression.Power pw && pw.Exponent is Expression.Number pe && pe.Value.IsMinusOne
                     && pw.Base is Expression.Number bn)
            {
                // Numeric reciprocal: 2^(-1) → 1/2 as a rational
                numProduct = (numProduct ?? Rational.One) * Rational.Pow(bn.Value, -1);
            }
            else if (!Expression.IsOne(f))
                factors.Add(f);
        }

        // Apply sign
        if (sign < 0)
        {
            if (numProduct.HasValue)
                numProduct = -numProduct.Value;
            else
                numProduct = Rational.MinusOne;
        }

        // Combine numeric factors
        if (numProduct.HasValue && !numProduct.Value.IsOne)
        {
            if (numProduct.Value.IsZero) return Zero;
            factors.Insert(0, new Expression.Number(numProduct.Value));
        }
        // Remove leading 1 if no numeric factor and sign=+1
        // (factors already had ones removed above)

        return factors.Count switch
        {
            0 => One,
            1 => factors[0],
            _ => new Expression.Product(factors)
        };
    }

    private static Expression SimplifyPower(Expression b, Expression e)
    {
        if (Expression.IsZero(e)) return One;
        if (Expression.IsOne(e)) return b;
        if (Expression.IsZero(b)) return Zero;
        if (Expression.IsOne(b)) return One;
        // x^(-1) → 1/x form is already handled elsewhere
        return new Expression.Power(b, e);
    }
}
