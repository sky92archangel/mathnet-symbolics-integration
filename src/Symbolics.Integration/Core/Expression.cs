using System.Numerics;

namespace MathNet.Symbolics.Integration.Core;

using System.Text;

/// <summary>Abstract base class for all symbolic expressions.</summary>
public abstract partial record Expression
{
    // ── Subtypes ────────────────────────────

    /// <summary>Exact rational number.</summary>
    public sealed record Number(Rational Value) : Expression
    {
        public override string ToString()
        {
            if (Value.Denominator == 1)
                return Value.Numerator.ToString();
            return Value.IsNegative
                ? $"-{(-Value.Numerator)}/{Value.Denominator}"
                : $"{Value.Numerator}/{Value.Denominator}";
        }
    }

    /// <summary>Floating-point approximation.</summary>
    public sealed record Approximation(double RealValue, double ImagValue) : Expression
    {
        public bool IsReal => ImagValue == 0.0;
        public static Approximation Real(double v) => new(v, 0.0);
        public static Approximation Complex(double r, double i) => new(r, i);
        public override string ToString() =>
            IsReal ? RealValue.ToString("G") : $"{RealValue:G} + {ImagValue:G}i";
    }

    /// <summary>Symbolic variable (e.g. x, y, t).</summary>
    public sealed record SymbolExpr(string Name) : Expression
    {
        public override string ToString() => Name;
    }

    /// <summary>Well-known constant (E, Pi, I).</summary>
    public sealed record Constant(ConstantType Type) : Expression
    {
        public override string ToString() => Type switch
        {
            ConstantType.E => "e",
            ConstantType.Pi => "π",
            ConstantType.I => "i",
            _ => Type.ToString()
        };
    }

    /// <summary>Sum of terms: Terms[0] + Terms[1] + ...</summary>
    public sealed record Sum(IReadOnlyList<Expression> Terms) : Expression
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var t in Terms)
            {
                string ts = t.ToString();
                // Detect negative numeric term → render as subtraction
                if (!first && t is Number n && n.Value.IsNegative)
                {
                    sb.Append(" - ");
                    sb.Append((-n.Value).ToString());
                }
                else if (!first && t is Product p && p.Factors.Count >= 1 &&
                         p.Factors[0] is Number n2 && n2.Value.IsMinusOne)
                {
                    sb.Append(" - ");
                    sb.Append(RenderFactor(p with { Factors = p.Factors.Skip(1).ToList() }, false));
                }
                else if (!first && t is Product p2 && p2.Factors.Count >= 1 &&
                         p2.Factors[0] is Number n3 && n3.Value.IsNegative)
                {
                    sb.Append(" - ");
                    sb.Append(RenderProductWithoutFirstFactor(p2));
                }
                else
                {
                    if (!first) sb.Append(" + ");
                    sb.Append(ts);
                }
                first = false;
            }
            return sb.ToString();
        }
    }

    /// <summary>Product of factors: Factors[0] * Factors[1] * ...</summary>
    public sealed record Product(IReadOnlyList<Expression> Factors) : Expression
    {
        public override string ToString()
        {
            if (Factors.Count == 0) return "1";
            // Check for leading -1
            if (Factors[0] is Number n && n.Value.IsMinusOne)
            {
                if (Factors.Count == 1) return "-1";
                return "-" + RenderFactor(Factors[1], true) +
                       string.Concat(Factors.Skip(2).Select(f => "·" + RenderFactor(f, true)));
            }
            if (Factors[0] is Number n2 && n2.Value.IsNegative)
            {
                return "-" + RenderFactor(new Number(-n2.Value), true) +
                       string.Concat(Factors.Skip(1).Select(f => "·" + RenderFactor(f, true)));
            }
            return string.Join("·", Factors.Select(f => RenderFactor(f, true)));
        }
    }

    /// <summary>Power: Base ^ Exponent.</summary>
    public sealed record Power(Expression Base, Expression Exponent) : Expression
    {
        public override string ToString()
        {
            // 1/(x) → 1/x
            if (Exponent is Number ne && ne.Value.IsMinusOne)
                return "1/" + RenderFactor(Base, false);
            string baseStr = RenderFactor(Base, false);
            string expStr = ExponentToString(Exponent);
            if (expStr == "1") return baseStr;
            if (expStr == "0") return "1";
            return baseStr + expStr;
        }
    }

    /// <summary>Unary function: f(Argument).</summary>
    public sealed record Function(FunctionType Op, Expression Argument) : Expression
    {
        public override string ToString()
        {
            var name = Op switch
            {
                FunctionType.Abs => "abs",
                FunctionType.Ln => "ln",
                FunctionType.Lg => "lg",
                FunctionType.Exp => "e",
                FunctionType.Sin => "sin",
                FunctionType.Cos => "cos",
                FunctionType.Tan => "tan",
                FunctionType.Csc => "csc",
                FunctionType.Sec => "sec",
                FunctionType.Cot => "cot",
                FunctionType.Sinh => "sinh",
                FunctionType.Cosh => "cosh",
                FunctionType.Tanh => "tanh",
                FunctionType.Csch => "csch",
                FunctionType.Sech => "sech",
                FunctionType.Coth => "coth",
                FunctionType.Asin => "asin",
                FunctionType.Acos => "acos",
                FunctionType.Atan => "atan",
                FunctionType.Acsc => "acsc",
                FunctionType.Asec => "asec",
                FunctionType.Acot => "acot",
                FunctionType.Asinh => "asinh",
                FunctionType.Acosh => "acosh",
                FunctionType.Atanh => "atanh",
                FunctionType.Acsch => "acsch",
                FunctionType.Asech => "asech",
                FunctionType.Acoth => "acoth",
                FunctionType.Erf => "erf",
                FunctionType.Erfc => "erfc",
                FunctionType.Erfi => "erfi",
                FunctionType.FresnelC => "FresnelC",
                FunctionType.FresnelS => "FresnelS",
                FunctionType.Si => "Si",
                FunctionType.Ci => "Ci",
                FunctionType.Shi => "Shi",
                FunctionType.Chi => "Chi",
                FunctionType.Ei => "Ei",
                FunctionType.Li => "Li",
                FunctionType.AiryAi => "Ai",
                FunctionType.AiryAiPrime => "Ai'",
                FunctionType.AiryBi => "Bi",
                FunctionType.AiryBiPrime => "Bi'",
                _ => Op.ToString()
            };
            if (Op == FunctionType.Exp)
                return "e^(" + Argument + ")";
            return name + "(" + Argument + ")";
        }
    }

    /// <summary>N-ary function: f(Arguments...).</summary>
    public sealed record FunctionN(FunctionNType Op, IReadOnlyList<Expression> Arguments) : Expression
    {
        public override string ToString()
        {
            var name = Op switch
            {
                FunctionNType.Log => "log",
                FunctionNType.Atan2 => "atan2",
                FunctionNType.LegendreP => "P",
                FunctionNType.ChebyshevT => "T",
                FunctionNType.ChebyshevU => "U",
                FunctionNType.HermiteH => "H",
                FunctionNType.LaguerreL => "L",
                FunctionNType.GegenbauerC => "C",
                FunctionNType.JacobiP => "P",
                FunctionNType.AssocLaguerreL => "L",
                FunctionNType.OwensT => "T",
                FunctionNType.Polylog => "Li",
                FunctionNType.UpperGamma => "Γ",
                FunctionNType.EllipticF => "F",
                FunctionNType.EllipticE => "E",
                FunctionNType.BesselJ => "J",
                FunctionNType.BesselY => "Y",
                FunctionNType.BesselI => "I",
                FunctionNType.BesselK => "K",
                _ => Op.ToString()
            };
            if (Op == FunctionNType.Log)
                return "log(" + string.Join(", ", Arguments.Select(a => a.ToString())) + ")";
            // Orthogonal polynomials: P_n(x) or P_n^(a,b)(x)
            if (Op is FunctionNType.LegendreP or FunctionNType.ChebyshevT
                or FunctionNType.ChebyshevU or FunctionNType.HermiteH
                or FunctionNType.LaguerreL or FunctionNType.GegenbauerC
                or FunctionNType.JacobiP or FunctionNType.AssocLaguerreL)
            {
                var args = Arguments.ToList();
                var x = args[^1];
                var n = args[0];
                if (args.Count == 2)
                    return $"{name}_{{{n}}}({x})";
                // args = [n, a, x] or [n, a, b, x]
                var @params = string.Join(", ", args.Skip(1).Take(args.Count - 2));
                return $"{name}_{{{n}}}^{{({@params})}}({x})";
            }
            return name + "(" + string.Join(", ", Arguments.Select(a => a.ToString())) + ")";
        }
    }

    /// <summary>Infinity.</summary>
    public sealed record Infinity(InfinityType Kind) : Expression
    {
        public override string ToString() => Kind switch
        {
            InfinityType.PositiveInfinity => "+∞",
            InfinityType.NegativeInfinity => "-∞",
            _ => "∞"
        };
    }

    /// <summary>Undefined / indeterminate.</summary>
    public sealed record Undefined() : Expression
    {
        public override string ToString() => "undefined";
    }

    // ── Helpers for formatting ──────────────

    private static string RenderFactor(Expression f, bool inProduct)
    {
        if (f is Number { Value: var nv })
        {
            if (nv.Denominator == 1)
            {
                if (nv.IsOne && inProduct) return "1";
                if (nv.IsMinusOne && inProduct) return "-1";
                return nv.Numerator.ToString();
            }
            if (nv.IsNegative)
                return $"-{(-nv.Numerator)}/{nv.Denominator}";
            return $"{nv.Numerator}/{nv.Denominator}";
        }
        if (f is Sum or Power or Function or FunctionN)
            return "(" + f.ToString() + ")";
        return f.ToString();
    }

    private static string ExponentToString(Expression e)
    {
        if (e is Number n)
        {
            if (n.Value.IsInteger && n.Value.Numerator >= -9 && n.Value.Numerator <= 9)
            {
                int v = n.Value.ToInt32();
                return v switch
                {
                    0 => "⁰", 1 => "¹", 2 => "²", 3 => "³", 4 => "⁴",
                    5 => "⁵", 6 => "⁶", 7 => "⁷", 8 => "⁸", 9 => "⁹",
                    -1 => "⁻¹", -2 => "⁻²", -3 => "⁻³",
                    -4 => "⁻⁴", -5 => "⁻⁵", -6 => "⁻⁶",
                    -7 => "⁻⁷", -8 => "⁻⁸", -9 => "⁻⁹",
                    _ => "^" + v
                };
            }
            return "^(" + n + ")";
        }
        // Non-numeric exponent
        var s = e.ToString();
        if (s.Length == 1) return "^" + s;
        return "^(" + s + ")";
    }

    private static string RenderProductWithoutFirstFactor(Product p)
    {
        var rest = p.Factors.Skip(1).ToList();
        if (rest.Count == 0) return "1";
        if (rest[0] is Number { Value: var nv })
            return nv.ToString() + string.Concat(rest.Skip(1).Select(f => "·" + RenderFactor(f, true)));
        return RenderFactor(rest[0], true) +
               string.Concat(rest.Skip(1).Select(f => "·" + RenderFactor(f, true)));
    }

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
