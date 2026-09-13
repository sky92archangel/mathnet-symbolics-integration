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
            result = BuildSumOrTerm(new[] { new Expression.Number(constSum.Value), result! });
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
}
