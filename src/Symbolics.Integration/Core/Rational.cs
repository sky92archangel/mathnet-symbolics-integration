using System.Numerics;

namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// (EN) An exact rational number (fraction of two arbitrary-precision integers).
///      This replaces the F# BigRational from MathNet.Numerics.FSharp.
/// (ZH) 精确有理数（两个任意精度整数的分数）。替代 MathNet.Numerics.FSharp 中的 F# BigRational。
/// </summary>
public readonly record struct Rational : IComparable<Rational>
{
    /// <summary>
    /// (EN) Numerator of the fraction. (ZH) 分数的分子。
    /// </summary>
    public BigInteger Numerator { get; }

    /// <summary>
    /// (EN) Denominator of the fraction (always positive after normalization). (ZH) 分数的分母（规范化后恒为正）。
    /// </summary>
    public BigInteger Denominator { get; }

    /// <summary>
    /// (EN) Internal constructor with optional normalization.
    /// (ZH) 内部构造函数，可选是否规范化。
    /// </summary>
    private Rational(BigInteger numerator, BigInteger denominator, bool normalize)
    {
        if (denominator == BigInteger.Zero)
            throw new DivideByZeroException("Rational denominator cannot be zero.");

        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        if (normalize)
        {
            var g = BigInteger.GreatestCommonDivisor(
                numerator < 0 ? -numerator : numerator, denominator);
            if (g > 1)
            {
                numerator /= g;
                denominator /= g;
            }
        }

        Numerator = numerator;
        Denominator = denominator;
    }

    /// <summary>
    /// (EN) Creates a rational from numerator and denominator; automatically normalizes.
    /// (ZH) 从分子和分母创建有理数；自动规范化。
    /// </summary>
    public Rational(BigInteger numerator, BigInteger denominator)
        : this(numerator, denominator, true) { }

    /// <summary>
    /// (EN) Creates a rational from an integer value.
    /// (ZH) 从整数值创建有理数。
    /// </summary>
    public Rational(BigInteger value) : this(value, BigInteger.One, false) { }

    // ──────────────────────────────────────────────
    //  Constants
    // ──────────────────────────────────────────────

    /// <summary>
    /// (EN) The rational number 0. (ZH) 有理数 0。
    /// </summary>
    public static readonly Rational Zero = new(0, 1, false);

    /// <summary>
    /// (EN) The rational number 1. (ZH) 有理数 1。
    /// </summary>
    public static readonly Rational One = new(1, 1, false);

    /// <summary>
    /// (EN) The rational number -1. (ZH) 有理数 -1。
    /// </summary>
    public static readonly Rational MinusOne = new(-1, 1, false);

    // ──────────────────────────────────────────────
    //  Properties
    // ──────────────────────────────────────────────

    /// <summary> (EN) True if the rational is zero. (ZH) 若有理数为零则为 true。 </summary>
    public bool IsZero => Numerator == 0;
    /// <summary> (EN) True if the rational is one. (ZH) 若有理数为一则为 true。 </summary>
    public bool IsOne => Numerator == 1 && Denominator == 1;
    /// <summary> (EN) True if the rational is minus one. (ZH) 若有理数为负一则 true。 </summary>
    public bool IsMinusOne => Numerator == -1 && Denominator == 1;
    /// <summary> (EN) True if the rational is an integer (denominator is 1). (ZH) 若分母为 1（整数）则为 true。 </summary>
    public bool IsInteger => Denominator == 1;
    /// <summary> (EN) True if the rational is positive. (ZH) 若为正数则为 true。 </summary>
    public bool IsPositive => Numerator > 0;
    /// <summary> (EN) True if the rational is negative. (ZH) 若为负数则为 true。 </summary>
    public bool IsNegative => Numerator < 0;

    // ──────────────────────────────────────────────
    //  Arithmetic
    // ──────────────────────────────────────────────

    /// <summary>
    /// (EN) Adds two rationals. (ZH) 两个有理数相加。
    /// </summary>
    public static Rational operator +(Rational a, Rational b)
    {
        var num = a.Numerator * b.Denominator + b.Numerator * a.Denominator;
        var den = a.Denominator * b.Denominator;
        return new Rational(num, den);
    }

    /// <summary>
    /// (EN) Subtracts two rationals. (ZH) 两个有理数相减。
    /// </summary>
    public static Rational operator -(Rational a, Rational b)
    {
        var num = a.Numerator * b.Denominator - b.Numerator * a.Denominator;
        var den = a.Denominator * b.Denominator;
        return new Rational(num, den);
    }

    /// <summary>
    /// (EN) Negates a rational. (ZH) 对有理数取负。
    /// </summary>
    public static Rational operator -(Rational a) =>
        new(-a.Numerator, a.Denominator, false);

    /// <summary>
    /// (EN) Multiplies two rationals with cross-cancellation to keep numbers small.
    /// (ZH) 两个有理数相乘，使用交叉约简以保持数值规模。
    /// </summary>
    public static Rational operator *(Rational a, Rational b)
    {
        // (EN) Cross-cancel before multiplying to keep numbers smaller.
        // (ZH) 交叉约简后再相乘，以控制数值大小。
        var g1 = BigInteger.GreatestCommonDivisor(
            a.Numerator < 0 ? -a.Numerator : a.Numerator, b.Denominator);
        var g2 = BigInteger.GreatestCommonDivisor(
            b.Numerator < 0 ? -b.Numerator : b.Numerator, a.Denominator);

        var num = (a.Numerator / g1) * (b.Numerator / g2);
        var den = (a.Denominator / g2) * (b.Denominator / g1);
        return new Rational(num, den, false);
    }

    /// <summary>
    /// (EN) Divides two rationals. (ZH) 两个有理数相除。
    /// </summary>
    public static Rational operator /(Rational a, Rational b)
    {
        if (b.IsZero) throw new DivideByZeroException();
        return a * new Rational(b.Denominator, b.Numerator, false);
    }

    /// <summary>
    /// (EN) Absolute value of a rational. (ZH) 有理数的绝对值。
    /// </summary>
    public static Rational Abs(Rational a) =>
        a.Numerator < 0 ? new Rational(-a.Numerator, a.Denominator, false) : a;

    /// <summary>
    /// (EN) Raises a rational to an integer power (exact result).
    /// (ZH) 有理数的整数次幂（精确结果）。
    /// </summary>
    public static Rational Pow(Rational a, int exp)
    {
        if (exp == 0) return One;
        if (exp < 0) return new Rational(
            BigInteger.Pow(a.Denominator, -exp),
            BigInteger.Pow(a.Numerator, -exp));
        return new Rational(
            BigInteger.Pow(a.Numerator, exp),
            BigInteger.Pow(a.Denominator, exp));
    }

    /// <summary>
    /// (EN) Converts to a double (may lose precision). (ZH) 转换为 double（可能丢失精度）。
    /// </summary>
    public double ToDouble() => (double)Numerator / (double)Denominator;

    /// <summary>
    /// (EN) Converts to an int (truncates). (ZH) 转换为 int（截断）。
    /// </summary>
    public int ToInt32() => (int)(Numerator / Denominator);

    /// <summary>
    /// (EN) Compares this rational to another. (ZH) 比较两个有理数。
    /// </summary>
    public int CompareTo(Rational other) =>
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    /// <summary>
    /// (EN) Renders as "N" for integers or "N/D" for fractions.
    /// (ZH) 整数渲染为 "N"，分数渲染为 "N/D"。
    /// </summary>
    public override string ToString() =>
        IsInteger ? Numerator.ToString() : $"{Numerator}/{Denominator}";

    // ──────────────────────────────────────────────
    //  Implicit conversions
    // ──────────────────────────────────────────────

    /// <summary>
    /// (EN) Implicitly converts an int to a rational. (ZH) 将 int 隐式转换为有理数。
    /// </summary>
    public static implicit operator Rational(int value) => new(value, 1, false);

    /// <summary>
    /// (EN) Explicitly converts a rational to a double. (ZH) 将有理数显式转换为 double。
    /// </summary>
    public static explicit operator double(Rational r) => r.ToDouble();
}
