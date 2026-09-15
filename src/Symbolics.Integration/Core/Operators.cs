// ----------------------------------------------------------------------------
// (EN) Purpose: Builds and normalizes symbolic expression trees and exposes the
//       arithmetic and transcendental operators used by the integration engine.
// (ZH) 用途：构造并规范化符号表达式树，并暴露积分引擎所需的算术与超越运算符。
// (EN) Notes: Every operator performs light normalization (identity elimination,
//       constant folding, flattening) so downstream rules see canonical forms.
// (ZH) 说明：每个运算符都会做轻量规范化（消去单位元、常量折叠、展平），
//       以便下游规则处理规范形式。
// ----------------------------------------------------------------------------
using System.Numerics;

namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// (EN) Expression construction and arithmetic operators.
/// (ZH) 表达式构造与算术运算符。
/// </summary>
public static class Operators
{
    // ── Constants ─────────────────────────────

    /// <summary>
    /// (EN) The integer constant zero (0).
    /// (ZH) 整数常量零（0）。
    /// </summary>
    public static Expression Zero => Expression.Zero;
    /// <summary>
    /// (EN) The integer constant one (1).
    /// (ZH) 整数常量一（1）。
    /// </summary>
    public static Expression One => Expression.One;
    /// <summary>
    /// (EN) The integer constant minus one (-1).
    /// (ZH) 整数常量负一（-1）。
    /// </summary>
    public static Expression MinusOne => Expression.MinusOne;
    /// <summary>
    /// (EN) The integer constant two (2).
    /// (ZH) 整数常量二（2）。
    /// </summary>
    public static Expression Two => Expression.Two;

    /// <summary>
    /// (EN) Euler's number e, the base of the natural logarithm.
    /// (ZH) 自然对数的底 e（欧拉数）。
    /// </summary>
    public static Expression E => new Expression.Constant(ConstantType.E);
    /// <summary>
    /// (EN) The circle constant pi (ratio of circumference to diameter).
    /// (ZH) 圆周率 π（圆周长与直径之比）。
    /// </summary>
    public static Expression Pi => new Expression.Constant(ConstantType.Pi);
    /// <summary>
    /// (EN) The imaginary unit i, satisfying i^2 = -1.
    /// (ZH) 虚数单位 i，满足 i^2 = -1。
    /// </summary>
    public static Expression I => new Expression.Constant(ConstantType.I);

    /// <summary>
    /// (EN) Wraps an Int32 value as an exact integer expression.
    /// (ZH) 将 Int32 值包装为精确的整数表达式。
    /// </summary>
    public static Expression Number(int value) => Expression.Int32(value);
    /// <summary>
    /// (EN) Wraps a rational value as an exact rational expression.
    /// (ZH) 将有理数值包装为精确的有理数表达式。
    /// </summary>
    public static Expression Number(Rational value) => new Expression.Number(value);
    /// <summary>
    /// (EN) Wraps a double as an approximate (floating-point) expression; the
    ///      second argument is the accumulated error bound, fixed at 0.0 here.
    /// (ZH) 将 double 包装为近似（浮点）表达式；第二个参数为累积误差界，此处固定为 0.0。
    /// </summary>
    public static Expression Number(double value) =>
        new Expression.Approximation(value, 0.0);

    /// <summary>
    /// (EN) Create a named symbol.
    /// (ZH) 创建一个具名符号。
    /// </summary>
    public static Expression Symbol(string name) => new Expression.SymbolExpr(name);

    // ── Arithmetic (with basic normalization) ─

    /// <summary>
    /// (EN) Adds two expressions with light normalization: zero identities are removed,
    ///      two numbers are folded, nested sums are flattened and constant terms collected.
    /// (ZH) 对两个表达式求和并做轻量规范化：消去零单位元、折叠两个数、展平嵌套和式并合并常数项。
    /// </summary>
    /// <param name="x">(EN) Left operand. (ZH) 左操作数。</param>
    /// <param name="y">(EN) Right operand. (ZH) 右操作数。</param>
    /// <returns>(EN) The normalized sum of <paramref name="x"/> and <paramref name="y"/>. (ZH) <paramref name="x"/> 与 <paramref name="y"/> 规范化后的和。</returns>
    public static Expression Add(Expression x, Expression y)
    {
        // (EN) Identity elements. (ZH) 消去单位元。
        if (Expression.IsZero(x)) return y;
        if (Expression.IsZero(y)) return x;

        // (EN) Number + Number. (ZH) 数值加数值。
        if (x is Expression.Number nx && y is Expression.Number ny)
            return new Expression.Number(nx.Value + ny.Value);

        // (EN) Flatten nested sums + collect constant terms. (ZH) 展平嵌套和式 + 收集常数项。
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
        if (result is null)
        {
            // (EN) Every non-numeric term cancelled: the sum reduces to its numeric part.
            // (ZH) 所有非数值项相消：和式只剩数值部分。
            return new Expression.Number(constSum ?? Rational.Zero);
        }
        if (constSum.HasValue && constSum.Value != Rational.Zero)
        {
            var constExpr = new Expression.Number(constSum.Value);
            if (result is Expression.Sum existingSum)
                result = new Expression.Sum(
                    new[] { constExpr }.Concat(existingSum.Terms).ToList());
            else
                result = BuildSumOrTerm(new[] { constExpr, result });
        }
        return result!;
    }

    /// <summary>
    /// (EN) Negates an expression: a number is negated directly, a product has its first
    ///      numeric factor negated, otherwise the raw product -1 * x is returned without
    ///      recursing into the simplifier.
    /// (ZH) 对表达式取负：数是直接取负，乘积则将首个数值因子取负，其他情况直接返回原始乘积
    ///      -1 * x，不再递归调用化简器。
    /// </summary>
    /// <param name="x">(EN) Expression to negate. (ZH) 待取负的表达式。</param>
    /// <returns>(EN) The expression <c>-x</c>. (ZH) 表达式 <c>-x</c>。</returns>
    public static Expression Negate(Expression x)
    {
        if (x is Expression.Number n) return new Expression.Number(-n.Value);
        if (x is Expression.Product p && p.Factors[0] is Expression.Number fn)
            return new Expression.Product(
                new[] { new Expression.Number(-fn.Value) }
                    .Concat(p.Factors.Skip(1)).ToList());
        // (EN) -1 * x (raw product, no recursion). (ZH) 直接构造 -1 * x（原始乘积，不递归）。
        return new Expression.Product(new[] { MinusOne, x });
    }

    /// <summary>
    /// (EN) Computes <c>x - y</c> as <c>x + (-y)</c>, reusing <see cref="Add"/> and <see cref="Negate"/>.
    /// (ZH) 以 <c>x + (-y)</c> 的形式计算 <c>x - y</c>，复用 <see cref="Add"/> 与 <see cref="Negate"/>。
    /// </summary>
    /// <param name="x">(EN) Minuend. (ZH) 被减数。</param>
    /// <param name="y">(EN) Subtrahend. (ZH) 减数。</param>
    /// <returns>(EN) The difference <paramref name="x"/> minus <paramref name="y"/>. (ZH) 差值 <paramref name="x"/> 减 <paramref name="y"/>。</returns>
    public static Expression Subtract(Expression x, Expression y) => Add(x, Negate(y));

    /// <summary>
    /// (EN) Multiplies two expressions with light normalization: zero/one identities are
    ///      removed, two numbers are folded, nested products are flattened, numeric factors
    ///      are collected and like factors are combined (x*x -> x^2).
    /// (ZH) 对两个表达式求积并做轻量规范化：消去零/一单位元、折叠两个数、展平嵌套乘积、
    ///      合并数值因子并合并同类因式（x*x -> x^2）。
    /// </summary>
    /// <param name="x">(EN) Left operand. (ZH) 左操作数。</param>
    /// <param name="y">(EN) Right operand. (ZH) 右操作数。</param>
    /// <returns>(EN) The normalized product of <paramref name="x"/> and <paramref name="y"/>. (ZH) <paramref name="x"/> 与 <paramref name="y"/> 规范化后的积。</returns>
    public static Expression Multiply(Expression x, Expression y)
    {
        if (Expression.IsZero(x) || Expression.IsZero(y)) return Zero;
        if (Expression.IsOne(x)) return y;
        if (Expression.IsOne(y)) return x;

        if (x is Expression.Number nx && y is Expression.Number ny)
            return new Expression.Number(nx.Value * ny.Value);

        // (EN) Flatten nested products + collect constant factors. (ZH) 展平嵌套乘积 + 收集常数因子。
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

        // (EN) Combine like factors: x*x → x^2, x^2*x → x^3. (ZH) 合并同类因式：x*x → x^2，x^2*x → x^3。
        factors = CombineLikeFactors(factors);

        // (EN) All non-numeric factors cancelled (e.g. x·x⁻¹): what remains is the numeric factor.
        // (ZH) 所有非数值因式相消（如 x·x⁻¹）：只剩数值因子。
        if (factors.Count == 0)
            return new Expression.Number(constMul ?? Rational.One);

        var result = BuildProductOrTerm(factors)!;
        if (constMul.HasValue && constMul.Value != Rational.One)
        {
            if (constMul.Value.IsMinusOne)
            {
                // (EN) Direct negation, NOT calling Negate to avoid recursion; keep products flat.
                // (ZH) 直接取负，不调用 Negate 以避免递归；保持乘积扁平。
                if (result is Expression.Number rn)
                    result = new Expression.Number(-rn.Value);
                else if (result is Expression.Product rp)
                    result = new Expression.Product(new[] { MinusOne }.Concat(rp.Factors).ToList());
                else
                    result = new Expression.Product(new[] { MinusOne, result! });
            }
            else
            {
                var cnum = new Expression.Number(constMul.Value);
                if (result is Expression.Product rp2)
                    result = new Expression.Product(new[] { cnum }.Concat(rp2.Factors).ToList());
                else
                    result = new Expression.Product(new[] { cnum, result! });
            }
        }
        return result!;
    }

    /// <summary>
    /// (EN) Divides <paramref name="x"/> by <paramref name="y"/>: dividing by one is a no-op,
    ///      dividing by zero yields complex infinity, and identical non-zero operands yield one;
    ///      otherwise it is implemented as <c>x * y^(-1)</c>.
    /// (ZH) 计算 <paramref name="x"/> 除以 <paramref name="y"/>：除以一为空操作，除以零得到复无穷，
    ///      分子分母相等且非零时得到一；其余情况实现为 <c>x * y^(-1)</c>。
    /// </summary>
    /// <param name="x">(EN) Numerator. (ZH) 分子。</param>
    /// <param name="y">(EN) Denominator. (ZH) 分母。</param>
    /// <returns>(EN) The quotient <paramref name="x"/> / <paramref name="y"/>. (ZH) 商 <paramref name="x"/> / <paramref name="y"/>。</returns>
    public static Expression Divide(Expression x, Expression y)
    {
        if (Expression.IsOne(y)) return x;
        if (Expression.IsZero(y)) return new Expression.Infinity(InfinityType.ComplexInfinity);
        if (x.Equals(y) && !Expression.IsZero(x)) return One;
        return Multiply(x, Pow(y, MinusOne));
    }

    /// <summary>
    /// (EN) Raises <paramref name="x"/> to the power <paramref name="y"/>. Trivial exponents
    ///      (0, 1) and bases (0, 1) are short-circuited; an integer exponent over a numeric base
    ///      is folded exactly with <see cref="Rational.Pow"/>; otherwise a power node is built.
    /// (ZH) 计算 <paramref name="x"/> 的 <paramref name="y"/> 次幂。指数为 0、1 或底数为 0、1 时
    ///      直接短路返回；数值底数配整数指数用 <see cref="Rational.Pow"/> 精确折叠；其余情况构造幂节点。
    /// </summary>
    /// <param name="x">(EN) Base. (ZH) 底数。</param>
    /// <param name="y">(EN) Exponent. (ZH) 指数。</param>
    /// <returns>(EN) The power <c>x^y</c>. (ZH) 幂 <c>x^y</c>。</returns>
    public static Expression Pow(Expression x, Expression y)
    {
        if (Expression.IsZero(y)) return One;
        if (Expression.IsOne(y)) return x;
        if (Expression.IsZero(x)) return Zero;
        if (Expression.IsOne(x)) return One;

        // (EN) Collapse (b^e)^n → b^(e·n) when the outer exponent n is an integer (safe for b≠0).
        // (ZH) 当外层指数 n 为整数时合并 (b^e)^n → b^(e·n)（b≠0 时成立）。
        if (x is Expression.Power inner && y is Expression.Number ny && ny.Value.IsInteger)
            return Pow(inner.Base, Multiply(inner.Exponent, y));

        // (EN) Integer power of a number. (ZH) 数值的整数次幂。
        if (x is Expression.Number nx && y is Expression.Number ny2 && ny2.Value.IsInteger)
        {
            int exp = ny2.Value.ToInt32();
            return new Expression.Number(Rational.Pow(nx.Value, exp));
        }

        return new Expression.Power(x, y);
    }

    // ── Unary functions ──────────────────────

    /// <summary>
    /// (EN) Sine function sin(x).
    /// (ZH) 正弦函数 sin(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Sin(Expression x) => new Expression.Function(FunctionType.Sin, x);
    /// <summary>
    /// (EN) Cosine function cos(x).
    /// (ZH) 余弦函数 cos(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Cos(Expression x) => new Expression.Function(FunctionType.Cos, x);
    /// <summary>
    /// (EN) Tangent function tan(x).
    /// (ZH) 正切函数 tan(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Tan(Expression x) => new Expression.Function(FunctionType.Tan, x);
    /// <summary>
    /// (EN) Cosecant function csc(x) = 1/sin(x).
    /// (ZH) 余割函数 csc(x) = 1/sin(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Csc(Expression x) => new Expression.Function(FunctionType.Csc, x);
    /// <summary>
    /// (EN) Secant function sec(x) = 1/cos(x).
    /// (ZH) 正割函数 sec(x) = 1/cos(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Sec(Expression x) => new Expression.Function(FunctionType.Sec, x);
    /// <summary>
    /// (EN) Cotangent function cot(x) = 1/tan(x).
    /// (ZH) 余切函数 cot(x) = 1/tan(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Cot(Expression x) => new Expression.Function(FunctionType.Cot, x);

    /// <summary>
    /// (EN) Hyperbolic sine sinh(x) = (e^x - e^(-x))/2.
    /// (ZH) 双曲正弦 sinh(x) = (e^x - e^(-x))/2。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Sinh(Expression x) => new Expression.Function(FunctionType.Sinh, x);
    /// <summary>
    /// (EN) Hyperbolic cosine cosh(x) = (e^x + e^(-x))/2.
    /// (ZH) 双曲余弦 cosh(x) = (e^x + e^(-x))/2。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Cosh(Expression x) => new Expression.Function(FunctionType.Cosh, x);
    /// <summary>
    /// (EN) Hyperbolic tangent tanh(x) = sinh(x)/cosh(x).
    /// (ZH) 双曲正切 tanh(x) = sinh(x)/cosh(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Tanh(Expression x) => new Expression.Function(FunctionType.Tanh, x);
    /// <summary>
    /// (EN) Hyperbolic cosecant csch(x) = 1/sinh(x).
    /// (ZH) 双曲余割 csch(x) = 1/sinh(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Csch(Expression x) => new Expression.Function(FunctionType.Csch, x);
    /// <summary>
    /// (EN) Hyperbolic secant sech(x) = 1/cosh(x).
    /// (ZH) 双曲正割 sech(x) = 1/cosh(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Sech(Expression x) => new Expression.Function(FunctionType.Sech, x);
    /// <summary>
    /// (EN) Hyperbolic cotangent coth(x) = 1/tanh(x).
    /// (ZH) 双曲余切 coth(x) = 1/tanh(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Coth(Expression x) => new Expression.Function(FunctionType.Coth, x);

    /// <summary>
    /// (EN) Inverse sine arcsin(x).
    /// (ZH) 反正弦 arcsin(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Asin(Expression x) => new Expression.Function(FunctionType.Asin, x);
    /// <summary>
    /// (EN) Inverse cosine arccos(x).
    /// (ZH) 反余弦 arccos(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Acos(Expression x) => new Expression.Function(FunctionType.Acos, x);
    /// <summary>
    /// (EN) Inverse tangent arctan(x).
    /// (ZH) 反正切 arctan(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Atan(Expression x) => new Expression.Function(FunctionType.Atan, x);
    /// <summary>
    /// (EN) Inverse cosecant arccsc(x) = arcsin(1/x).
    /// (ZH) 反余割 arccsc(x) = arcsin(1/x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Acsc(Expression x) => new Expression.Function(FunctionType.Acsc, x);
    /// <summary>
    /// (EN) Inverse secant arcsec(x) = arccos(1/x).
    /// (ZH) 反正割 arcsec(x) = arccos(1/x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Asec(Expression x) => new Expression.Function(FunctionType.Asec, x);
    /// <summary>
    /// (EN) Inverse cotangent arccot(x).
    /// (ZH) 反余切 arccot(x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Acot(Expression x) => new Expression.Function(FunctionType.Acot, x);

    /// <summary>
    /// (EN) Inverse hyperbolic sine arsinh(x) = ln(x + sqrt(x^2 + 1)).
    /// (ZH) 反双曲正弦 arsinh(x) = ln(x + sqrt(x^2 + 1))。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Asinh(Expression x) => new Expression.Function(FunctionType.Asinh, x);
    /// <summary>
    /// (EN) Inverse hyperbolic cosine arcosh(x) = ln(x + sqrt(x^2 - 1)), defined for x >= 1.
    /// (ZH) 反双曲余弦 arcosh(x) = ln(x + sqrt(x^2 - 1))，定义域为 x >= 1。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Acosh(Expression x) => new Expression.Function(FunctionType.Acosh, x);
    /// <summary>
    /// (EN) Inverse hyperbolic tangent artanh(x) = ln((1 + x)/(1 - x))/2, defined for |x| &lt; 1.
    /// (ZH) 反双曲正切 artanh(x) = ln((1 + x)/(1 - x))/2，定义域为 |x| &lt; 1。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Atanh(Expression x) => new Expression.Function(FunctionType.Atanh, x);
    /// <summary>
    /// (EN) Inverse hyperbolic cosecant arcsch(x) = arsinh(1/x).
    /// (ZH) 反双曲余割 arcsch(x) = arsinh(1/x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Acsch(Expression x) => new Expression.Function(FunctionType.Acsch, x);
    /// <summary>
    /// (EN) Inverse hyperbolic secant arsech(x) = arcosh(1/x).
    /// (ZH) 反双曲正割 arsech(x) = arcosh(1/x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Asech(Expression x) => new Expression.Function(FunctionType.Asech, x);
    /// <summary>
    /// (EN) Inverse hyperbolic cotangent arcoth(x) = artanh(1/x).
    /// (ZH) 反双曲余切 arcoth(x) = artanh(1/x)。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Acoth(Expression x) => new Expression.Function(FunctionType.Acoth, x);

    /// <summary>
    /// (EN) Natural exponential function exp(x) = e^x.
    /// (ZH) 自然指数函数 exp(x) = e^x。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Exp(Expression x) => new Expression.Function(FunctionType.Exp, x);
    /// <summary>
    /// (EN) Natural logarithm ln(x), the inverse of <see cref="Exp"/>.
    /// (ZH) 自然对数 ln(x)，为 <see cref="Exp"/> 的反函数。
    /// </summary>
    /// <param name="x">(EN) Argument, must be positive. (ZH) 自变量，必须为正。</param>
    public static Expression Ln(Expression x) => new Expression.Function(FunctionType.Ln, x);
    /// <summary>
    /// (EN) Decadic (base-10) logarithm lg(x).
    /// (ZH) 常用（以 10 为底）对数 lg(x)。
    /// </summary>
    /// <param name="x">(EN) Argument, must be positive. (ZH) 自变量，必须为正。</param>
    public static Expression Lg(Expression x) => new Expression.Function(FunctionType.Lg, x);
    /// <summary>
    /// (EN) Absolute value |x|.
    /// (ZH) 绝对值 |x|。
    /// </summary>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Abs(Expression x) => new Expression.Function(FunctionType.Abs, x);

    /// <summary>
    /// (EN) Square root, expressed as the exact power x^(1/2) via <see cref="Pow"/>.
    /// (ZH) 平方根，通过 <see cref="Pow"/> 表示为精确幂 x^(1/2)。
    /// </summary>
    /// <param name="x">(EN) Radicand. (ZH) 被开方数。</param>
    public static Expression Sqrt(Expression x) => Pow(x, Expression.Half);

    // ── N‑ary functions ──────────────────────

    /// <summary>
    /// (EN) Logarithm of <paramref name="x"/> to an arbitrary <paramref name="basis"/>,
    ///      represented as a two-argument <c>log</c> function node.
    /// (ZH) 以任意 <paramref name="basis"/> 为底、<paramref name="x"/> 的对数，
    ///      表示为双参数 <c>log</c> 函数节点。
    /// </summary>
    /// <param name="basis">(EN) Logarithm base. (ZH) 对数的底。</param>
    /// <param name="x">(EN) Argument. (ZH) 自变量。</param>
    public static Expression Log(Expression basis, Expression x) =>
        new Expression.FunctionN(FunctionNType.Log, new[] { basis, x });
    /// <summary>
    /// (EN) Two-argument arctangent atan2(y, x), returning the angle of the point (x, y)
    ///      in the range (-pi, pi].
    /// (ZH) 双参数反正切 atan2(y, x)，返回点 (x, y) 的辐角，取值范围为 (-pi, pi]。
    /// </summary>
    /// <param name="y">(EN) Ordinate. (ZH) 纵坐标。</param>
    /// <param name="x">(EN) Abscissa. (ZH) 横坐标。</param>
    public static Expression Atan2(Expression y, Expression x) =>
        new Expression.FunctionN(FunctionNType.Atan2, new[] { y, x });

    // ── Internal helpers ─────────────────────

    /// <summary>
    /// (EN) Flattens a sum tree, yielding the individual non-sum terms in order (recursively).
    /// (ZH) 展平求和树，按顺序递归产出各个非求和项。
    /// </summary>
    private static IEnumerable<Expression> FlattenSumTerms(Expression e) =>
        e is Expression.Sum s ? s.Terms.SelectMany(FlattenSumTerms) : new[] { e };

    /// <summary>
    /// (EN) Flattens a product tree, yielding the individual non-product factors in order (recursively).
    /// (ZH) 展平乘积树，按顺序递归产出各个非乘积因式。
    /// </summary>
    private static IEnumerable<Expression> FlattenProductFactors(Expression e) =>
        e is Expression.Product p ? p.Factors.SelectMany(FlattenProductFactors) : new[] { e };

    /// <summary>
    /// (EN) Combine like factors: [x, x] → [x^2], [x, x^2] → [x^3].
    /// (ZH) 合并同类因式：[x, x] → [x^2]，[x, x^2] → [x^3]。
    /// </summary>
    private static List<Expression> CombineLikeFactors(List<Expression> factors)
    {
        if (factors.Count <= 1) return factors;

        // (EN) Sum exponents per base (rational exponents combine too: x^½·x^⅓ = x^⅚), and combine
        //      exponentials e^a·e^b = e^(a+b).
        // (ZH) 按底累加指数（有理指数也合并：x^½·x^⅓ = x^⅚），并合并指数 e^a·e^b = e^(a+b)。
        var groups = new Dictionary<Expression, Rational>();
        var expArgs = new List<Expression>();
        var others = new List<Expression>();

        foreach (var f in factors)
        {
            if (f is Expression.Number || f is Expression.Constant) { others.Add(f); continue; }

            if (f is Expression.Function fn && fn.Op == FunctionType.Exp)
            {
                expArgs.Add(fn.Argument);
                continue;
            }

            Expression? bas = f;
            Rational exp = Rational.One;
            if (f is Expression.Power pw)
            {
                if (pw.Exponent is not Expression.Number pe) { others.Add(f); continue; }
                bas = pw.Base;
                exp = pe.Value;
            }
            groups[bas] = (groups.TryGetValue(bas, out var cur) ? cur : Rational.Zero) + exp;
        }

        var result = new List<Expression>();
        foreach (var kv in groups)
        {
            if (kv.Value.IsZero) continue;
            if (kv.Value.IsOne) result.Add(kv.Key);
            else result.Add(Pow(kv.Key, new Expression.Number(kv.Value)));
        }

        if (expArgs.Count > 0)
        {
            var total = expArgs[0];
            for (int i = 1; i < expArgs.Count; i++) total = Add(total, expArgs[i]);
            result.Add(Exp(total));
        }

        Rational? numProduct = null;
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

    /// <summary>
    /// (EN) Wraps terms into a sum node, collapsing the degenerate 0-term (null) and
    ///      1-term (bare term) cases.
    /// (ZH) 将各项包装为求和节点，并折叠退化情形：0 项返回 null，1 项直接返回该项。
    /// </summary>
    private static Expression? BuildSumOrTerm(IReadOnlyList<Expression> terms) => terms.Count switch
    {
        0 => null,
        1 => terms[0],
        _ => new Expression.Sum(terms)
    };

    /// <summary>
    /// (EN) Wraps factors into a product node, collapsing the degenerate 0-factor (null) and
    ///      1-factor (bare factor) cases.
    /// (ZH) 将各因式包装为乘积节点，并折叠退化情形：0 个因式返回 null，1 个因式直接返回该因式。
    /// </summary>
    private static Expression? BuildProductOrTerm(IReadOnlyList<Expression> factors) => factors.Count switch
    {
        0 => null,
        1 => factors[0],
        _ => new Expression.Product(factors)
    };

    // ── Simplification ─────────────────────────

    /// <summary>
    /// (EN) Simplify an expression tree (flatten, combine constants, cancel identities).
    /// (ZH) 化简表达式树（展平、合并常量、消去单位元）。
    /// </summary>
    /// <param name="expr">(EN) Expression to simplify. (ZH) 待化简的表达式。</param>
    /// <returns>(EN) An equivalent expression in a normalized form. (ZH) 规范化形式的等价表达式。</returns>
    public static Expression Simplify(Expression expr)
    {
        // (EN) Recursively simplify children. (ZH) 递归化简子节点。
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

    /// <summary>
    /// (EN) Simplifies a sum: recursively simplifies children, flattens nested sums and
    ///      accumulates constant terms into a single numeric term placed first.
    /// (ZH) 化简求和式：递归化简子项、展平嵌套和式，并将常数项累加为单个数值项放在最前。
    /// </summary>
    private static Expression SimplifySum(Expression.Sum sum)
    {
        var simplified = sum.Terms.Select(Simplify).ToList();
        // (EN) Flatten nested sums + collect constants. (ZH) 展平嵌套和式 + 收集常数。
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

    /// <summary>
    /// (EN) Simplifies a product: recursively simplifies factors, flattens nested products,
    ///      collects numeric factors (including reciprocals of numbers) and applies the sign.
    /// (ZH) 化简乘积式：递归化简因式、展平嵌套乘积，合并数值因子（含数的倒数）并施加符号。
    /// </summary>
    private static Expression SimplifyProduct(Expression.Product prod)
    {
        var simplified = prod.Factors.Select(Simplify).ToList();
        // (EN) Collect numeric factors, flatten nested products. (ZH) 收集数值因子，展平嵌套乘积。
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
                // (EN) Numeric reciprocal: 2^(-1) → 1/2 as a rational. (ZH) 数值倒数：2^(-1) → 有理数 1/2。
                numProduct = (numProduct ?? Rational.One) * Rational.Pow(bn.Value, -1);
            }
            else if (!Expression.IsOne(f))
                factors.Add(f);
        }

        // (EN) Apply sign. (ZH) 施加符号。
        if (sign < 0)
        {
            if (numProduct.HasValue)
                numProduct = -numProduct.Value;
            else
                numProduct = Rational.MinusOne;
        }

        // (EN) Combine numeric factors. (ZH) 合并数值因子。
        if (numProduct.HasValue && !numProduct.Value.IsOne)
        {
            if (numProduct.Value.IsZero) return Zero;
            factors.Insert(0, new Expression.Number(numProduct.Value));
        }
        // (EN) Remove leading 1 if no numeric factor and sign=+1.
        // (ZH) 若无数值因子且符号为 +1 则移除前导的 1（已在上面移除过 1）。

        return factors.Count switch
        {
            0 => One,
            1 => factors[0],
            _ => new Expression.Product(factors)
        };
    }

    /// <summary>
    /// (EN) Simplifies a power by cancelling the trivial cases b^0 = 1, b^1 = b, 0^e = 0 and 1^e = 1.
    /// (ZH) 化简幂式，消去退化情形 b^0 = 1、b^1 = b、0^e = 0 与 1^e = 1。
    /// </summary>
    private static Expression SimplifyPower(Expression b, Expression e)
    {
        if (Expression.IsZero(e)) return One;
        if (Expression.IsOne(e)) return b;
        if (Expression.IsZero(b)) return Zero;
        if (Expression.IsOne(b)) return One;
        // (EN) Collapse (b^e)^n → b^(e·n) for an integer outer exponent n. (ZH) 外层指数 n 为整数时合并 (b^e)^n → b^(e·n)。
        if (b is Expression.Power bp && e is Expression.Number ne && ne.Value.IsInteger)
            return SimplifyPower(bp.Base, Multiply(bp.Exponent, e));
        // (EN) x^(-1) → 1/x form is already handled elsewhere. (ZH) x^(-1) → 1/x 形式已在别处处理。
        return new Expression.Power(b, e);
    }
}
