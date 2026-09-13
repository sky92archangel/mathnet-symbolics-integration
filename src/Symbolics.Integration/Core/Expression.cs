using System.Numerics;

namespace MathNet.Symbolics.Integration.Core;

/// <summary>Abstract base class for all symbolic expressions.</summary>
public abstract record Expression
{
    // ── Subtypes ────────────────────────────

    /// <summary>Exact rational number.</summary>
    public sealed record Number(Rational Value) : Expression;

    /// <summary>Floating-point approximation.</summary>
    public sealed record Approximation(double RealValue, double ImagValue) : Expression
    {
        public bool IsReal => ImagValue == 0.0;
        public static Approximation Real(double v) => new(v, 0.0);
        public static Approximation Complex(double r, double i) => new(r, i);
    }

    /// <summary>Symbolic variable (e.g. x, y, t).</summary>
    public sealed record SymbolExpr(string Name) : Expression;

    /// <summary>Well-known constant (E, Pi, I).</summary>
    public sealed record Constant(ConstantType Type) : Expression;

    /// <summary>Sum of terms: Terms[0] + Terms[1] + ...</summary>
    public sealed record Sum(IReadOnlyList<Expression> Terms) : Expression;

    /// <summary>Product of factors: Factors[0] * Factors[1] * ...</summary>
    public sealed record Product(IReadOnlyList<Expression> Factors) : Expression;

    /// <summary>Power: Base ^ Exponent.</summary>
    public sealed record Power(Expression Base, Expression Exponent) : Expression;

    /// <summary>Unary function: f(Argument).</summary>
    public sealed record Function(FunctionType Op, Expression Argument) : Expression;

    /// <summary>N-ary function: f(Arguments...).</summary>
    public sealed record FunctionN(FunctionNType Op, IReadOnlyList<Expression> Arguments) : Expression;

    /// <summary>Infinity.</summary>
    public sealed record Infinity(InfinityType Kind) : Expression;

    /// <summary>Undefined / indeterminate.</summary>
    public sealed record Undefined() : Expression;

    // ── Convenience constants ───────────────

    public static readonly Expression Zero = new Number(Rational.Zero);
    public static readonly Expression One = new Number(Rational.One);
    public static readonly Expression MinusOne = new Number(Rational.MinusOne);
    public static readonly Expression Two = new Number((Rational)2);
    public static readonly Expression Half = new Number(new Rational(1, 2));

    public static Expression Int32(int value) => new Number((Rational)value);
    public static Expression Integer(BigInteger value) => new Number(new Rational(value));

    // ── Pattern-matching helpers ────────────

    public static bool IsZero(Expression e) => e is Number n && n.Value.IsZero;
    public static bool IsOne(Expression e) => e is Number n && n.Value.IsOne;
    public static bool IsMinusOne(Expression e) => e is Number n && n.Value.IsMinusOne;
    public static bool IsInteger(Expression e) => e is Number n && n.Value.IsInteger;

    public static bool IsNumber(Expression e) => e is Number;
    public static bool IsSymbol(Expression e) => e is SymbolExpr;
    public static bool IsSymbol(Expression e, out string name)
    {
        if (e is SymbolExpr s) { name = s.Name; return true; }
        name = null!; return false;
    }

    public static bool IsSum(Expression e) => e is Sum;
    public static bool IsProduct(Expression e) => e is Product;
    public static bool IsPower(Expression e) => e is Power;
    public static bool IsFunction(Expression e) => e is Function;
    public static bool IsFunctionN(Expression e) => e is FunctionN;
    public static bool IsInfinity(Expression e) => e is Infinity;
    public static bool IsUndefined(Expression e) => e is Undefined;

    // ── Operator overloads ────────────────────

    public static implicit operator Expression(int value) => new Number((Rational)value);
    public static implicit operator Expression(double value) => new Approximation(value, 0.0);

    public static Expression operator +(Expression a, Expression b) => Operators.Add(a, b);
    public static Expression operator -(Expression a, Expression b) => Operators.Subtract(a, b);
    public static Expression operator -(Expression a) => Operators.Negate(a);
    public static Expression operator *(Expression a, Expression b) => Operators.Multiply(a, b);
    public static Expression operator /(Expression a, Expression b) => Operators.Divide(a, b);
}
