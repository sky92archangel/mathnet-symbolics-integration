// ----------------------------------------------------------------------------
// (EN) Purpose: Defines the immutable expression tree used by the symbolic
//       integrator: numbers, symbols, constants, sums, products, powers and functions.
// (ZH) 用途：定义符号积分器所用的不可变表达式树：数字、符号、常量、和式、乘积、幂与函数。
// (EN) Notes: All node types derive from the abstract Expression record; formatting
//       helpers render expressions with implicit multiplication, fractions and superscript powers.
// (ZH) 说明：所有节点类型均派生自抽象记录 Expression；格式化辅助方法负责以隐式乘法、
//       分数与上标幂渲染表达式。
// ----------------------------------------------------------------------------

using System.Numerics;

namespace MathNet.Symbolics.Integration.Core;

using System.Text;

/// <summary>
/// (EN) Abstract base class for all symbolic expressions.
/// (ZH) 所有符号表达式的抽象基类。
/// </summary>
public abstract partial record Expression
{
    // ── Subtypes ────────────────────────────

    /// <summary>
    /// (EN) Exact rational number.
    /// (ZH) 精确有理数。
    /// </summary>
    /// <param name="Value">(EN) The exact rational value. (ZH) 精确的有理数值。</param>
    public sealed record Number(Rational Value) : Expression
    {
        /// <summary>
        /// (EN) Renders the rational as an integer when possible, otherwise as "n/d" with the sign on the numerator.
        /// (ZH) 尽可能渲染为整数，否则渲染为 "n/d"（符号置于分子）。
        /// </summary>
        /// <returns>(EN) The formatted number. (ZH) 格式化后的数字字符串。</returns>
        public override string ToString()
        {
            if (Value.Denominator == 1)
                return Value.Numerator.ToString();
            return Value.IsNegative
                ? $"-{(-Value.Numerator)}/{Value.Denominator}"
                : $"{Value.Numerator}/{Value.Denominator}";
        }
    }

    /// <summary>
    /// (EN) Floating-point approximation.
    /// (ZH) 浮点近似值。
    /// </summary>
    /// <param name="RealValue">(EN) Real part. (ZH) 实部。</param>
    /// <param name="ImagValue">(EN) Imaginary part. (ZH) 虚部。</param>
    public sealed record Approximation(double RealValue, double ImagValue) : Expression
    {
        /// <summary>
        /// (EN) True when the imaginary part is exactly zero.
        /// (ZH) 当虚部精确为 0 时为 true。
        /// </summary>
        public bool IsReal => ImagValue == 0.0;
        /// <summary>
        /// (EN) Creates a real-valued approximation.
        /// (ZH) 创建实数近似值。
        /// </summary>
        /// <param name="v">(EN) The real value. (ZH) 实数值。</param>
        /// <returns>(EN) A new approximation with zero imaginary part. (ZH) 虚部为 0 的新近似值。</returns>
        public static Approximation Real(double v) => new(v, 0.0);
        /// <summary>
        /// (EN) Creates a complex-valued approximation.
        /// (ZH) 创建复数近似值。
        /// </summary>
        /// <param name="r">(EN) Real part. (ZH) 实部。</param>
        /// <param name="i">(EN) Imaginary part. (ZH) 虚部。</param>
        /// <returns>(EN) A new approximation. (ZH) 新的近似值。</returns>
        public static Approximation Complex(double r, double i) => new(r, i);
        /// <summary>
        /// (EN) Renders the real part alone when the value is real, otherwise as "r + ii".
        /// (ZH) 实数值只渲染实部，否则渲染为 "r + ii"。
        /// </summary>
        /// <returns>(EN) The formatted approximation. (ZH) 格式化后的近似值字符串。</returns>
        public override string ToString() =>
            IsReal ? RealValue.ToString("G") : $"{RealValue:G} + {ImagValue:G}i";
    }

    /// <summary>
    /// (EN) Symbolic variable (e.g. x, y, t).
    /// (ZH) 符号变量（例如 x、y、t）。
    /// </summary>
    /// <param name="Name">(EN) The variable name. (ZH) 变量名。</param>
    public sealed record SymbolExpr(string Name) : Expression
    {
        /// <summary>
        /// (EN) Returns the variable name as-is.
        /// (ZH) 原样返回变量名。
        /// </summary>
        /// <returns>(EN) The variable name. (ZH) 变量名。</returns>
        public override string ToString() => Name;
    }

    /// <summary>
    /// (EN) Well-known constant (E, Pi, I).
    /// (ZH) 常用数学常量（E、Pi、I）。
    /// </summary>
    /// <param name="Type">(EN) Which constant. (ZH) 常量种类。</param>
    public sealed record Constant(ConstantType Type) : Expression
    {
        /// <summary>
        /// (EN) Renders the constant as "e", "π" or "i".
        /// (ZH) 将常量渲染为 "e"、"π" 或 "i"。
        /// </summary>
        /// <returns>(EN) The constant symbol. (ZH) 常量符号。</returns>
        public override string ToString() => Type switch
        {
            ConstantType.E => "e",
            ConstantType.Pi => "π",
            ConstantType.I => "i",
            _ => Type.ToString()
        };
    }

    /// <summary>
    /// (EN) Sum of terms: Terms[0] + Terms[1] + ...
    /// (ZH) 各项之和：Terms[0] + Terms[1] + ...。
    /// </summary>
    /// <param name="Terms">(EN) The summed terms. (ZH) 参与相加的各项。</param>
    public sealed record Sum(IReadOnlyList<Expression> Terms) : Expression
    {
        /// <summary>
        /// (EN) Structural equality: sums are equal when they have equal terms in the same order
        ///      (the default record equality would compare the list by reference).
        /// (ZH) 结构相等：和式各项按相同顺序逐一相等时才相等（默认 record 相等会按引用比较列表）。
        /// </summary>
        /// <param name="other">(EN) The sum to compare with. (ZH) 要比较的和式。</param>
        /// <returns>(EN) True when both sums have equal terms in order. (ZH) 两个和式各项按顺序相等时为 true。</returns>
        public bool Equals(Sum? other)
            => other is not null && Terms.SequenceEqual(other.Terms);

        /// <summary>
        /// (EN) Hash code consistent with the structural equality above.
        /// (ZH) 与上述结构相等一致哈希码。
        /// </summary>
        /// <returns>(EN) The combined hash of the terms. (ZH) 各项哈希的组合值。</returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var t in Terms) hash.Add(t);
            return hash.ToHashCode();
        }

        /// <summary>
        /// (EN) Renders the sum, turning negative terms (or terms whose leading factor is -1 or
        /// (EN) negative) into subtractions for a more natural output.
        /// (ZH) 渲染和式；把负项（或首因子为 -1 / 负数的项）改写为减法，使输出更自然。
        /// </summary>
        /// <returns>(EN) The formatted sum. (ZH) 格式化后的和式字符串。</returns>
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

    /// <summary>
    /// (EN) Product of factors: Factors[0] * Factors[1] * ...
    /// (ZH) 各因子之积：Factors[0] * Factors[1] * ...。
    /// </summary>
    /// <param name="Factors">(EN) The multiplied factors. (ZH) 参与相乘的各因子。</param>
    public sealed record Product(IReadOnlyList<Expression> Factors) : Expression
    {
        /// <summary>
        /// (EN) Structural equality: products are equal when they have equal factors in the same
        ///      order (the default record equality would compare the list by reference).
        /// (ZH) 结构相等：乘积各因子按相同顺序逐一相等时才相等（默认 record 相等会按引用比较列表）。
        /// </summary>
        /// <param name="other">(EN) The product to compare with. (ZH) 要比较的乘积。</param>
        /// <returns>(EN) True when both products have equal factors in order. (ZH) 两个乘积各因子按顺序相等时为 true。</returns>
        public bool Equals(Product? other)
            => other is not null && Factors.SequenceEqual(other.Factors);

        /// <summary>
        /// (EN) Hash code consistent with the structural equality above.
        /// (ZH) 与上述结构相等一致哈希码。
        /// </summary>
        /// <returns>(EN) The combined hash of the factors. (ZH) 各因子哈希的组合值。</returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var f in Factors) hash.Add(f);
            return hash.ToHashCode();
        }

        /// <summary>
        /// (EN) Renders the product with "·" separators, lifting a leading -1 or negative factor into a minus sign.
        /// (ZH) 用 "·" 分隔渲染乘积，并把开头的 -1 或负因子提升为减号。
        /// </summary>
        /// <returns>(EN) The formatted product. (ZH) 格式化后的乘积字符串。</returns>
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

    /// <summary>
    /// (EN) Power: Base ^ Exponent.
    /// (ZH) 幂：Base ^ Exponent。
    /// </summary>
    /// <param name="Base">(EN) The base. (ZH) 底数。</param>
    /// <param name="Exponent">(EN) The exponent. (ZH) 指数。</param>
    public sealed record Power(Expression Base, Expression Exponent) : Expression
    {
        /// <summary>
        /// (EN) Renders the power, using "1/base" for exponent -1, superscript digits for small
        /// (EN) integer exponents, and dropping exponents of 1 and 0.
        /// (ZH) 渲染幂：指数为 -1 时写作 "1/base"，小整数指数用上标数字，指数 1 与 0 则省略化简。
        /// </summary>
        /// <returns>(EN) The formatted power. (ZH) 格式化后的幂字符串。</returns>
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

    /// <summary>
    /// (EN) Unary function: f(Argument).
    /// (ZH) 一元函数：f(Argument)。
    /// </summary>
    /// <param name="Op">(EN) The function kind. (ZH) 函数种类。</param>
    /// <param name="Argument">(EN) The function argument. (ZH) 函数自变量。</param>
    public sealed record Function(FunctionType Op, Expression Argument) : Expression
    {
        /// <summary>
        /// (EN) Renders the function using its display name; the exponential is rendered as "e^(arg)".
        /// (ZH) 用显示名渲染函数；指数函数渲染为 "e^(arg)"。
        /// </summary>
        /// <returns>(EN) The formatted function. (ZH) 格式化后的函数字符串。</returns>
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

    /// <summary>
    /// (EN) N-ary function: f(Arguments...).
    /// (ZH) 多元函数：f(Arguments...)。
    /// </summary>
    /// <param name="Op">(EN) The function kind. (ZH) 函数种类。</param>
    /// <param name="Arguments">(EN) The function arguments. (ZH) 函数的各个自变量。</param>
    public sealed record FunctionN(FunctionNType Op, IReadOnlyList<Expression> Arguments) : Expression
    {
        /// <summary>
        /// (EN) Structural equality: n-ary functions are equal when their operator and argument
        ///      lists match in order (the default record equality would compare the list by reference).
        /// (ZH) 结构相等：多元函数在操作符与参数列表按顺序都一致时才相等（默认 record 相等会按引用比较列表）。
        /// </summary>
        /// <param name="other">(EN) The function to compare with. (ZH) 要比较的函数。</param>
        /// <returns>(EN) True when both operator and arguments match. (ZH) 操作符与参数都匹配时为 true。</returns>
        public bool Equals(FunctionN? other)
            => other is not null && Op == other.Op && Arguments.SequenceEqual(other.Arguments);

        /// <summary>
        /// (EN) Hash code consistent with the structural equality above.
        /// (ZH) 与上述结构相等一致哈希码。
        /// </summary>
        /// <returns>(EN) The combined hash of the operator and arguments. (ZH) 操作符与参数哈希的组合值。</returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Op);
            foreach (var a in Arguments) hash.Add(a);
            return hash.ToHashCode();
        }

        /// <summary>
        /// (EN) Renders the n-ary function; orthogonal polynomials use the subscript/superscript
        /// (EN) notation P_n(x) or P_n^(a,b)(x), others use the plain f(args...) form.
        /// (ZH) 渲染多元函数；正交多项式采用下标/上标记法 P_n(x) 或 P_n^(a,b)(x)，其余用普通 f(args...) 形式。
        /// </summary>
        /// <returns>(EN) The formatted function. (ZH) 格式化后的函数字符串。</returns>
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

    /// <summary>
    /// (EN) Infinity.
    /// (ZH) 无穷大。
    /// </summary>
    /// <param name="Kind">(EN) Which infinity (positive or negative). (ZH) 无穷大的种类（正或负）。</param>
    public sealed record Infinity(InfinityType Kind) : Expression
    {
        /// <summary>
        /// (EN) Renders the infinity as "+∞" or "-∞".
        /// (ZH) 将无穷大渲染为 "+∞" 或 "-∞"。
        /// </summary>
        /// <returns>(EN) The infinity symbol. (ZH) 无穷大符号。</returns>
        public override string ToString() => Kind switch
        {
            InfinityType.PositiveInfinity => "+∞",
            InfinityType.NegativeInfinity => "-∞",
            _ => "∞"
        };
    }

    /// <summary>
    /// (EN) Undefined / indeterminate.
    /// (ZH) 未定义 / 不定式。
    /// </summary>
    public sealed record Undefined() : Expression
    {
        /// <summary>
        /// (EN) Renders the literal text "undefined".
        /// (ZH) 渲染为字面文本 "undefined"。
        /// </summary>
        /// <returns>(EN) The text "undefined". (ZH) 文本 "undefined"。</returns>
        public override string ToString() => "undefined";
    }

    // ── Helpers for formatting ──────────────

    /// <summary>
    /// (EN) Renders a single factor for product/sum output, parenthesizing compound
    ///      nodes (sum, power, function) and collapsing the numeric ±1 identities.
    /// (ZH) 为乘积/和式输出渲染单个因子：对复合节点（和式、幂、函数）加括号，
    ///      并折叠数值 ±1 单位元。
    /// </summary>
    /// <param name="f">(EN) The factor to render. (ZH) 待渲染的因子。</param>
    /// <param name="inProduct">(EN) Whether the factor is being rendered inside a product. (ZH) 是否在乘积上下文中渲染。</param>
    /// <returns>(EN) The formatted factor text. (ZH) 格式化后的因子文本。</returns>
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

    /// <summary>
    /// (EN) Renders an exponent as a superscript digit for small integers (-9..9) and
    ///      as "^n" or "^(...)" otherwise, parenthesizing multi-character exponents.
    /// (ZH) 将指数渲染为小整数（-9..9）的上标数字，否则渲染为 "^n" 或 "^(...)"，
    ///      多字符指数加括号。
    /// </summary>
    /// <param name="e">(EN) The exponent expression. (ZH) 指数表达式。</param>
    /// <returns>(EN) The formatted exponent text. (ZH) 格式化后的指数文本。</returns>
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

    /// <summary>
    /// (EN) Renders a product after dropping its leading (negative) numeric factor,
    ///      used when a sum term is rewritten as a subtraction.
    /// (ZH) 在去掉开头（负的）数值因子后渲染乘积，用于把和式中的项改写为减法。
    /// </summary>
    /// <param name="p">(EN) The product whose first factor is to be omitted. (ZH) 需要省略首因子的乘积。</param>
    /// <returns>(EN) The formatted remaining factors. (ZH) 格式化后的剩余因子文本。</returns>
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

    /// <summary>
    /// (EN) The exact number 0.
    /// (ZH) 精确数值 0。
    /// </summary>
    public static readonly Expression Zero = new Number(Rational.Zero);
    /// <summary>
    /// (EN) The exact number 1.
    /// (ZH) 精确数值 1。
    /// </summary>
    public static readonly Expression One = new Number(Rational.One);
    /// <summary>
    /// (EN) The exact number -1.
    /// (ZH) 精确数值 -1。
    /// </summary>
    public static readonly Expression MinusOne = new Number(Rational.MinusOne);
    /// <summary>
    /// (EN) The exact number 2.
    /// (ZH) 精确数值 2。
    /// </summary>
    public static readonly Expression Two = new Number((Rational)2);
    /// <summary>
    /// (EN) The exact number 1/2.
    /// (ZH) 精确数值 1/2。
    /// </summary>
    public static readonly Expression Half = new Number(new Rational(1, 2));

    /// <summary>
    /// (EN) Wraps an int as an exact rational number expression.
    /// (ZH) 把 int 包装为精确有理数表达式。
    /// </summary>
    /// <param name="value">(EN) The integer value. (ZH) 整数值。</param>
    /// <returns>(EN) A Number expression. (ZH) 一个 Number 表达式。</returns>
    public static Expression Int32(int value) => new Number((Rational)value);
    /// <summary>
    /// (EN) Wraps a BigInteger as an exact rational number expression.
    /// (ZH) 把 BigInteger 包装为精确有理数表达式。
    /// </summary>
    /// <param name="value">(EN) The integer value. (ZH) 整数值。</param>
    /// <returns>(EN) A Number expression. (ZH) 一个 Number 表达式。</returns>
    public static Expression Integer(BigInteger value) => new Number(new Rational(value));

    // ── Pattern-matching helpers ────────────

    /// <summary>
    /// (EN) True if <paramref name="e"/> is the numeric zero.
    /// (ZH) 若 <paramref name="e"/> 为数值 0 则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is zero. (ZH) 该表达式是否为零。</returns>
    public static bool IsZero(Expression e) => e is Number n && n.Value.IsZero;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is the numeric one.
    /// (ZH) 若 <paramref name="e"/> 为数值 1 则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is one. (ZH) 该表达式是否为一。</returns>
    public static bool IsOne(Expression e) => e is Number n && n.Value.IsOne;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is the numeric minus one.
    /// (ZH) 若 <paramref name="e"/> 为数值 -1 则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is minus one. (ZH) 该表达式是否为 -1。</returns>
    public static bool IsMinusOne(Expression e) => e is Number n && n.Value.IsMinusOne;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is a numeric value with denominator one.
    /// (ZH) 若 <paramref name="e"/> 为分母为 1 的数值则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is an integer. (ZH) 该表达式是否为整数。</returns>
    public static bool IsInteger(Expression e) => e is Number n && n.Value.IsInteger;

    /// <summary>
    /// (EN) True if <paramref name="e"/> is a Number node.
    /// (ZH) 若 <paramref name="e"/> 是 Number 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is a number. (ZH) 该表达式是否为数字。</returns>
    public static bool IsNumber(Expression e) => e is Number;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is a SymbolExpr node.
    /// (ZH) 若 <paramref name="e"/> 是 SymbolExpr 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is a symbol. (ZH) 该表达式是否为符号。</returns>
    public static bool IsSymbol(Expression e) => e is SymbolExpr;
    /// <summary>
    /// (EN) Tests whether <paramref name="e"/> is a symbol and, if so, returns its name.
    /// (ZH) 判断 <paramref name="e"/> 是否为符号，若是则同时返回其名称。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <param name="name">(EN) The symbol name when the test succeeds, otherwise null. (ZH) 判断成功时为符号名，否则为 null。</param>
    /// <returns>(EN) Whether the expression is a symbol. (ZH) 该表达式是否为符号。</returns>
    public static bool IsSymbol(Expression e, out string name)
    {
        if (e is SymbolExpr s) { name = s.Name; return true; }
        name = null!; return false;
    }

    /// <summary>
    /// (EN) True if <paramref name="e"/> is a Sum node.
    /// (ZH) 若 <paramref name="e"/> 是 Sum 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is a sum. (ZH) 该表达式是否为和式。</returns>
    public static bool IsSum(Expression e) => e is Sum;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is a Product node.
    /// (ZH) 若 <paramref name="e"/> 是 Product 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is a product. (ZH) 该表达式是否为乘积。</returns>
    public static bool IsProduct(Expression e) => e is Product;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is a Power node.
    /// (ZH) 若 <paramref name="e"/> 是 Power 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is a power. (ZH) 该表达式是否为幂。</returns>
    public static bool IsPower(Expression e) => e is Power;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is a unary Function node.
    /// (ZH) 若 <paramref name="e"/> 是一元 Function 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is a function. (ZH) 该表达式是否为函数。</returns>
    public static bool IsFunction(Expression e) => e is Function;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is an n-ary FunctionN node.
    /// (ZH) 若 <paramref name="e"/> 是多元 FunctionN 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is an n-ary function. (ZH) 该表达式是否为多元函数。</returns>
    public static bool IsFunctionN(Expression e) => e is FunctionN;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is an Infinity node.
    /// (ZH) 若 <paramref name="e"/> 是 Infinity 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is an infinity. (ZH) 该表达式是否为无穷大。</returns>
    public static bool IsInfinity(Expression e) => e is Infinity;
    /// <summary>
    /// (EN) True if <paramref name="e"/> is an Undefined node.
    /// (ZH) 若 <paramref name="e"/> 是 Undefined 节点则为 true。
    /// </summary>
    /// <param name="e">(EN) The expression to test. (ZH) 待判断的表达式。</param>
    /// <returns>(EN) Whether the expression is undefined. (ZH) 该表达式是否为未定义。</returns>
    public static bool IsUndefined(Expression e) => e is Undefined;

    // ── Operator overloads ────────────────────

    /// <summary>
    /// (EN) Implicitly converts an int to an exact rational Number expression.
    /// (ZH) 将 int 隐式转换为精确有理数 Number 表达式。
    /// </summary>
    /// <param name="value">(EN) The integer value. (ZH) 整数值。</param>
    /// <returns>(EN) A Number expression. (ZH) 一个 Number 表达式。</returns>
    public static implicit operator Expression(int value) => new Number((Rational)value);
    /// <summary>
    /// (EN) Implicitly converts a double to a real Approximation expression.
    /// (ZH) 将 double 隐式转换为实数 Approximation 表达式。
    /// </summary>
    /// <param name="value">(EN) The real value. (ZH) 实数值。</param>
    /// <returns>(EN) An Approximation expression. (ZH) 一个 Approximation 表达式。</returns>
    public static implicit operator Expression(double value) => new Approximation(value, 0.0);

    /// <summary>
    /// (EN) Adds two expressions (delegates to <c>Operators.Add</c>, which performs simplification).
    /// (ZH) 两个表达式相加（委托给 <c>Operators.Add</c>，其中包含化简）。
    /// </summary>
    /// <param name="a">(EN) Left operand. (ZH) 左操作数。</param>
    /// <param name="b">(EN) Right operand. (ZH) 右操作数。</param>
    /// <returns>(EN) The simplified sum. (ZH) 化简后的和。</returns>
    public static Expression operator +(Expression a, Expression b) => Operators.Add(a, b);
    /// <summary>
    /// (EN) Subtracts two expressions (delegates to <c>Operators.Subtract</c>).
    /// (ZH) 两个表达式相减（委托给 <c>Operators.Subtract</c>）。
    /// </summary>
    /// <param name="a">(EN) Left operand. (ZH) 左操作数。</param>
    /// <param name="b">(EN) Right operand. (ZH) 右操作数。</param>
    /// <returns>(EN) The simplified difference. (ZH) 化简后的差。</returns>
    public static Expression operator -(Expression a, Expression b) => Operators.Subtract(a, b);
    /// <summary>
    /// (EN) Negates an expression (unary minus, delegates to <c>Operators.Negate</c>).
    /// (ZH) 对表达式取负（一元负号，委托给 <c>Operators.Negate</c>）。
    /// </summary>
    /// <param name="a">(EN) The operand. (ZH) 操作数。</param>
    /// <returns>(EN) The negated expression. (ZH) 取负后的表达式。</returns>
    public static Expression operator -(Expression a) => Operators.Negate(a);
    /// <summary>
    /// (EN) Multiplies two expressions (delegates to <c>Operators.Multiply</c>).
    /// (ZH) 两个表达式相乘（委托给 <c>Operators.Multiply</c>）。
    /// </summary>
    /// <param name="a">(EN) Left operand. (ZH) 左操作数。</param>
    /// <param name="b">(EN) Right operand. (ZH) 右操作数。</param>
    /// <returns>(EN) The simplified product. (ZH) 化简后的积。</returns>
    public static Expression operator *(Expression a, Expression b) => Operators.Multiply(a, b);
    /// <summary>
    /// (EN) Divides two expressions (delegates to <c>Operators.Divide</c>).
    /// (ZH) 两个表达式相除（委托给 <c>Operators.Divide</c>）。
    /// </summary>
    /// <param name="a">(EN) Numerator. (ZH) 分子。</param>
    /// <param name="b">(EN) Denominator. (ZH) 分母。</param>
    /// <returns>(EN) The simplified quotient. (ZH) 化简后的商。</returns>
    public static Expression operator /(Expression a, Expression b) => Operators.Divide(a, b);
}
