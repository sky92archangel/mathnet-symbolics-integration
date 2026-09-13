using System.Numerics;

namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// An exact rational number (fraction of two arbitrary-precision integers).
/// This replaces the F# BigRational from MathNet.Numerics.FSharp.
/// </summary>
public readonly record struct Rational : IComparable<Rational>
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

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

    public Rational(BigInteger numerator, BigInteger denominator)
        : this(numerator, denominator, true) { }

    public Rational(BigInteger value) : this(value, BigInteger.One, false) { }

    // ──────────────────────────────────────────────
    //  Constants
    // ──────────────────────────────────────────────

    public static readonly Rational Zero = new(0, 1, false);
    public static readonly Rational One = new(1, 1, false);
    public static readonly Rational MinusOne = new(-1, 1, false);

    // ──────────────────────────────────────────────
    //  Properties
    // ──────────────────────────────────────────────

    public bool IsZero => Numerator == 0;
    public bool IsOne => Numerator == 1 && Denominator == 1;
    public bool IsMinusOne => Numerator == -1 && Denominator == 1;
    public bool IsInteger => Denominator == 1;
    public bool IsPositive => Numerator > 0;
    public bool IsNegative => Numerator < 0;

    // ──────────────────────────────────────────────
    //  Arithmetic
    // ──────────────────────────────────────────────

    public static Rational operator +(Rational a, Rational b)
    {
        var num = a.Numerator * b.Denominator + b.Numerator * a.Denominator;
        var den = a.Denominator * b.Denominator;
        return new Rational(num, den);
    }

    public static Rational operator -(Rational a, Rational b)
    {
        var num = a.Numerator * b.Denominator - b.Numerator * a.Denominator;
        var den = a.Denominator * b.Denominator;
        return new Rational(num, den);
    }

    public static Rational operator -(Rational a) =>
        new(-a.Numerator, a.Denominator, false);

    public static Rational operator *(Rational a, Rational b)
    {
        // Cross-cancel before multiplying to keep numbers smaller
        var g1 = BigInteger.GreatestCommonDivisor(
            a.Numerator < 0 ? -a.Numerator : a.Numerator, b.Denominator);
        var g2 = BigInteger.GreatestCommonDivisor(
            b.Numerator < 0 ? -b.Numerator : b.Numerator, a.Denominator);

        var num = (a.Numerator / g1) * (b.Numerator / g2);
        var den = (a.Denominator / g2) * (b.Denominator / g1);
        return new Rational(num, den, false);
    }

    public static Rational operator /(Rational a, Rational b)
    {
        if (b.IsZero) throw new DivideByZeroException();
        return a * new Rational(b.Denominator, b.Numerator, false);
    }

    public static Rational Abs(Rational a) =>
        a.Numerator < 0 ? new Rational(-a.Numerator, a.Denominator, false) : a;

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

    public double ToDouble() => (double)Numerator / (double)Denominator;
    public int ToInt32() => (int)(Numerator / Denominator);

    public int CompareTo(Rational other) =>
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public override string ToString() =>
        IsInteger ? Numerator.ToString() : $"{Numerator}/{Denominator}";

    // ──────────────────────────────────────────────
    //  Implicit conversions
    // ──────────────────────────────────────────────

    public static implicit operator Rational(int value) => new(value, 1, false);
    public static explicit operator double(Rational r) => r.ToDouble();
}
