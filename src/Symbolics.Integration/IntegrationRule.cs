// ----------------------------------------------------------------------------
// (EN) Purpose: Defines the symbolic integration rule tree used by the integrator.
//       Each record models one derivation step and can evaluate itself into the
//       antiderivative expression of that step.
// (ZH) 用途：定义积分器使用的符号积分规则树。每个 record 表示一步推导，并可将自身
//       求值为该步骤的原函数表达式。
// (EN) Notes: The rules are immutable records assembled by the solver; because substeps
//       are required properties, Eval() may only be called on a fully built tree. An
//       integral that cannot be solved is represented explicitly by DontKnowRule so that
//       the derivation always stays complete.
// (ZH) 说明：规则是由求解器组装的不可变 record；由于子步骤是 required 属性，只有在规则树
//       构建完成之后才能调用 Eval()。无法求解的积分由 DontKnowRule 显式表示，从而保证推导
//       始终是完整的。
// ----------------------------------------------------------------------------

using MathNet.Symbolics.Integration.Core;
using static MathNet.Symbolics.Integration.Core.Operators;

namespace MathNet.Symbolics.Integration;

/// <summary>
/// (EN) Base type of every symbolic integration rule: it carries the integrand and the
/// integration variable and evaluates to the antiderivative of the current step.
/// (ZH) 所有符号积分规则的基类型：持有被积表达式与积分变量，并求值为当前步骤的原函数。
/// </summary>
public abstract record IntegrationRule
{
    /// <summary>
    /// (EN) The expression being integrated at this step.
    /// (ZH) 本步骤中被积的表达式。
    /// </summary>
    public required Expression Integrand { get; init; }

    /// <summary>
    /// (EN) The integration variable (usually x); the antiderivative is expressed in this variable.
    /// (ZH) 积分变量（通常为 x）；原函数以该变量表示。
    /// </summary>
    public required Expression Variable { get; init; }

    /// <summary>
    /// (EN) Evaluates this step and returns the antiderivative expression. The result is a
    /// symbolic expression that the solver composes with the surrounding substeps.
    /// (ZH) 求值本步骤并返回原函数表达式。结果为符号表达式，由求解器与外围子步骤组合。
    /// </summary>
    public abstract Expression Eval();

    /// <summary>
    /// (EN) Whether the derivation below this rule contains an unresolved (DontKnowRule) leaf,
    /// i.e. the integral could not be completed.
    /// (ZH) 该规则之下的推导是否包含未解决（DontKnowRule）的叶子节点，即积分未能完成。
    /// </summary>
    public virtual bool ContainsDontKnow => false;
}

/// <summary>
/// (EN) Base type for rules that are resolved in a single step and therefore never have substeps.
/// (ZH) 单步即可求解、因而没有子步骤的规则的基类型。
/// </summary>
public abstract record AtomicRule : IntegrationRule
{
    /// <summary>
    /// (EN) Always <c>false</c>: an atomic rule never depends on an unresolved substep.
    /// (ZH) 恒为 <c>false</c>：原子规则不依赖任何未解决的子步骤。
    /// </summary>
    public override bool ContainsDontKnow => false;
}

/// <summary>
/// (EN) Rule for ∫ c dx = c·x: integrates a constant factor with respect to the variable.
/// (ZH) 常数积分规则 ∫ c dx = c·x：对常数因子关于积分变量求积分。
/// </summary>
public sealed record ConstantRule : AtomicRule
{
    /// <summary>
    /// (EN) The constant factor c (any expression independent of the integration variable).
    /// (ZH) 常数因子 c（任意与积分变量无关的表达式）。
    /// </summary>
    public required Expression Constant { get; init; }

    /// <summary>
    /// (EN) Returns c·x, where x is the integration variable.
    /// (ZH) 返回 c·x，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Multiply(Constant, Variable);
}

/// <summary>
/// (EN) Power rule ∫ x^n dx = x^(n+1)/(n+1), with the special case ∫ x^(-1) dx = ln(x).
/// (ZH) 幂函数积分规则 ∫ x^n dx = x^(n+1)/(n+1)，特例 ∫ x^(-1) dx = ln(x)。
/// </summary>
public sealed record PowerRule : AtomicRule
{
    /// <summary>
    /// (EN) Base of the power (typically the linear term in the integration variable).
    /// (ZH) 幂的底（通常是积分变量的线性表达式）。
    /// </summary>
    public required Expression Base { get; init; }

    /// <summary>
    /// (EN) Exponent n; the value -1 selects the logarithm branch instead of the power formula.
    /// (ZH) 指数 n；取值为 -1 时走对数分支而非幂公式。
    /// </summary>
    public required Expression Exp { get; init; }

    /// <summary>
    /// (EN) Returns ln(Base) when n = -1, otherwise Base^(n+1)/(n+1).
    /// (ZH) 当 n = -1 时返回 ln(Base)，否则返回 Base^(n+1)/(n+1)。
    /// </summary>
    public override Expression Eval()
    {
        if (Expression.IsMinusOne(Exp))
            return Ln(Base);
        return Divide(Pow(Base, Add(Exp, One)), Add(Exp, One));
    }
}

/// <summary>
/// (EN) ∫ √(a+bx+cx²) dx = ((2cx+b)/4c)·√(a+bx+cx²) - (b²-4ac)/(8c)·∫ 1/√(a+bx+cx²) dx.
/// (ZH) 二次式平方根积分：∫ √(a+bx+cx²) dx = ((2cx+b)/4c)·√(a+bx+cx²) - (b²-4ac)/(8c)·∫ 1/√(a+bx+cx²) dx。
/// </summary>
public sealed record SqrtQuadraticRule : AtomicRule
{
    /// <summary>
    /// (EN) Constant term a of the quadratic a+bx+cx².
    /// (ZH) 二次式 a+bx+cx² 的常数项 a。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) Linear coefficient b of the quadratic a+bx+cx².
    /// (ZH) 二次式 a+bx+cx² 的一次项系数 b。
    /// </summary>
    public required Expression B { get; init; }

    /// <summary>
    /// (EN) Quadratic coefficient c of a+bx+cx²; the formula requires c ≠ 0.
    /// (ZH) 二次式 a+bx+cx² 的二次项系数 c；公式要求 c ≠ 0。
    /// </summary>
    public required Expression C { get; init; }

    /// <summary>
    /// (EN) Rule that evaluates the recursive term ∫ 1/√(a+bx+cx²) dx appearing in the formula.
    /// (ZH) 用于求值公式中递归项 ∫ 1/√(a+bx+cx²) dx 的规则。
    /// </summary>
    public required IntegrationRule ReciprocalStep { get; init; }

    /// <summary>
    /// (EN) Applies the formula above with x = Variable, taking the recursive term from
    /// <c>ReciprocalStep.Eval()</c>.
    /// (ZH) 以 x = Variable 应用上述公式，递归项取自 <c>ReciprocalStep.Eval()</c>。
    /// </summary>
    public override Expression Eval()
    {
        var (a, b, c, x) = (A, B, C, Variable);
        var sqrtQ = Sqrt(a + b * x + c * x * x);
        // (EN) Term1 = ((2*c*x + b) / (4*c)) * sqrt(a+bx+cx²). (ZH) 第一项 = ((2*c*x + b) / (4*c)) * sqrt(a+bx+cx²)。
        var term1 = ((Two * c * x + b) / (Four * c)) * sqrtQ;
        // (EN) Term2 = -(b² - 4*a*c) / (8*c) * ∫ 1/√(a+bx+cx²) dx. (ZH) 第二项 = -(b² - 4*a*c) / (8*c) * ∫ 1/√(a+bx+cx²) dx。
        var discriminant = b * b - Four * a * c;
        var coeff2 = -discriminant / (Eight * c);
        var term2 = coeff2 * ReciprocalStep.Eval();
        return term1 + term2;
    }

    /// <summary>
    /// (EN) Cached integer constant 8 used in the sqrt-quadratic coefficient formula.
    /// (ZH) 缓存的整数常量 8，用于平方根二次式系数公式。
    /// </summary>
    private static readonly Expression Eight = Expression.Int32(8);
    /// <summary>
    /// (EN) Cached integer constant 4 used in the sqrt-quadratic coefficient formula.
    /// (ZH) 缓存的整数常量 4，用于平方根二次式系数公式。
    /// </summary>
    private static readonly Expression Four = Expression.Int32(4);
}

/// <summary>
/// (EN) Rule for ∫ 1/u dx = ln(u), where u is a linear term in the integration variable.
/// (ZH) 倒数积分规则 ∫ 1/u dx = ln(u)，其中 u 是积分变量的线性表达式。
/// </summary>
public sealed record ReciprocalRule : AtomicRule
{
    /// <summary>
    /// (EN) The denominator u whose reciprocal is integrated.
    /// (ZH) 被积倒数的分母 u。
    /// </summary>
    public required Expression Base { get; init; }

    /// <summary>
    /// (EN) Returns ln(Base).
    /// (ZH) 返回 ln(Base)。
    /// </summary>
    public override Expression Eval() => Ln(Base);
}

/// <summary>
/// (EN) Rule for ∫ b^x dx = b^x/ln(b); when the base is Euler's number e the result is Exp(x).
/// (ZH) 指数函数积分规则 ∫ b^x dx = b^x/ln(b)；当底为自然常数 e 时结果为 Exp(x)。
/// </summary>
public sealed record ExpRule : AtomicRule
{
    /// <summary>
    /// (EN) The base b of the exponential; must be positive and different from 1.
    /// (ZH) 指数函数的底 b；必须为正且不等于 1。
    /// </summary>
    public required Expression Base { get; init; }

    /// <summary>
    /// (EN) The exponent of the power in the integrand.
    /// (ZH) 被积表达式中幂的指数。
    /// </summary>
    public required Expression Exp { get; init; }

    /// <summary>
    /// (EN) Returns Exp(x) when the base is e, otherwise divides the integrand by ln(Base).
    /// (ZH) 底为 e 时返回 Exp(x)，否则用 ln(Base) 除被积表达式。
    /// </summary>
    public override Expression Eval()
    {
        if (Base is Expression.Constant { Type: ConstantType.E })
            return new Expression.Function(FunctionType.Exp, Variable);
        return Divide(Integrand, Ln(Base));
    }
}

/// <summary>
/// (EN) Rule ∫ sin(x) dx = -cos(x).
/// (ZH) 正弦积分规则 ∫ sin(x) dx = -cos(x)。
/// </summary>
public sealed record SinRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns -cos(x) with x the integration variable.
    /// (ZH) 返回 -cos(x)，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Negate(Cos(Variable));
}

/// <summary>
/// (EN) Rule ∫ cos(x) dx = sin(x).
/// (ZH) 余弦积分规则 ∫ cos(x) dx = sin(x)。
/// </summary>
public sealed record CosRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns sin(x) with x the integration variable.
    /// (ZH) 返回 sin(x)，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Sin(Variable);
}

/// <summary>
/// (EN) Rule ∫ sinh(x) dx = cosh(x).
/// (ZH) 双曲正弦积分规则 ∫ sinh(x) dx = cosh(x)。
/// </summary>
public sealed record SinhRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns cosh(x) with x the integration variable.
    /// (ZH) 返回 cosh(x)，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Cosh(Variable);
}

/// <summary>
/// (EN) Rule ∫ cosh(x) dx = sinh(x).
/// (ZH) 双曲余弦积分规则 ∫ cosh(x) dx = sinh(x)。
/// </summary>
public sealed record CoshRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns sinh(x) with x the integration variable.
    /// (ZH) 返回 sinh(x)，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Sinh(Variable);
}

/// <summary>
/// (EN) Rule ∫ tan(x) dx = -ln|cos(x)|.
/// (ZH) 正切积分规则 ∫ tan(x) dx = -ln|cos(x)|。
/// </summary>
public sealed record TanRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns -ln(cos(x)) with x the integration variable.
    /// (ZH) 返回 -ln(cos(x))，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Negate(Ln(Cos(Variable)));
}

/// <summary>
/// (EN) Rule ∫ cot(x) dx = ln|sin(x)|.
/// (ZH) 余切积分规则 ∫ cot(x) dx = ln|sin(x)|。
/// </summary>
public sealed record CotRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns ln(sin(x)) with x the integration variable.
    /// (ZH) 返回 ln(sin(x))，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Ln(Sin(Variable));
}

/// <summary>
/// (EN) Rule ∫ sec(x) dx = ln|sec(x) + tan(x)|.
/// (ZH) 正割积分规则 ∫ sec(x) dx = ln|sec(x) + tan(x)|。
/// </summary>
public sealed record SecRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns ln(sec(x) + tan(x)) with x the integration variable.
    /// (ZH) 返回 ln(sec(x) + tan(x))，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Ln(Sec(Variable) + Tan(Variable));
}

/// <summary>
/// (EN) Rule ∫ csc(x) dx = -ln|csc(x) + cot(x)|.
/// (ZH) 余割积分规则 ∫ csc(x) dx = -ln|csc(x) + cot(x)|。
/// </summary>
public sealed record CscRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns -ln(csc(x) + cot(x)) with x the integration variable.
    /// (ZH) 返回 -ln(csc(x) + cot(x))，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Negate(Ln(Csc(Variable) + Cot(Variable)));
}

/// <summary>
/// (EN) Rule ∫ tanh(x) dx = ln(cosh(x)).
/// (ZH) 双曲正切积分规则 ∫ tanh(x) dx = ln(cosh(x))。
/// </summary>
public sealed record TanhRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns ln(cosh(x)) with x the integration variable.
    /// (ZH) 返回 ln(cosh(x))，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Ln(Cosh(Variable));
}

/// <summary>
/// (EN) Rule ∫ coth(x) dx = ln|sinh(x)|.
/// (ZH) 双曲余切积分规则 ∫ coth(x) dx = ln|sinh(x)|。
/// </summary>
public sealed record CothRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns ln(sinh(x)) with x the integration variable.
    /// (ZH) 返回 ln(sinh(x))，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Ln(Sinh(Variable));
}

/// <summary>
/// (EN) Rule ∫ sech(x) dx = 2·atan(e^x).
/// (ZH) 双曲正割积分规则 ∫ sech(x) dx = 2·atan(e^x)。
/// </summary>
public sealed record SechRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns 2·atan(exp(x)) with x the integration variable.
    /// (ZH) 返回 2·atan(exp(x))，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() =>
        Multiply(Two, Atan(Exp(Variable)));
}

/// <summary>
/// (EN) Rule ∫ csch(x) dx = ln|tanh(x/2)|.
/// (ZH) 双曲余割积分规则 ∫ csch(x) dx = ln|tanh(x/2)|。
/// </summary>
public sealed record CschRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns ln(tanh(x/2)) with x the integration variable.
    /// (ZH) 返回 ln(tanh(x/2))，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Ln(Tanh(Divide(Variable, Two)));
}

/// <summary>
/// (EN) Rule whose antiderivative is asin(x); it corresponds to ∫ 1/√(1-x²) dx = asin(x).
/// (ZH) 原函数为 asin(x) 的规则，对应 ∫ 1/√(1-x²) dx = asin(x)。
/// </summary>
public sealed record ArcsinRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns asin(x) with x the integration variable.
    /// (ZH) 返回 asin(x)，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Asin(Variable);
}

/// <summary>
/// (EN) Rule whose antiderivative is asinh(x); it corresponds to ∫ 1/√(1+x²) dx = asinh(x).
/// (ZH) 原函数为 asinh(x) 的规则，对应 ∫ 1/√(1+x²) dx = asinh(x)。
/// </summary>
public sealed record ArcsinhRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns asinh(x) with x the integration variable.
    /// (ZH) 返回 asinh(x)，其中 x 为积分变量。
    /// </summary>
    public override Expression Eval() => Asinh(Variable);
}

/// <summary>
/// (EN) Linearity rule: the integral of a sum equals the sum of the integrals of its terms.
/// (ZH) 线性规则：和的积分等于各项积分之和。
/// </summary>
public sealed record AddRule : IntegrationRule
{
    /// <summary>
    /// (EN) One rule per additive term; each substep is evaluated and the results are summed.
    /// (ZH) 每个加法项对应一条规则；逐一求值后求和。
    /// </summary>
    public required IReadOnlyList<IntegrationRule> Substeps { get; init; }

    /// <summary>
    /// (EN) Evaluates every substep and returns their sum; an empty list yields zero.
    /// (ZH) 求值所有子步骤并返回其和；空列表返回 0。
    /// </summary>
    public override Expression Eval()
    {
        Expression? result = null;
        foreach (var step in Substeps)
        {
            var evaled = step.Eval();
            result = result is null ? evaled : Add(result, evaled);
        }
        return result ?? Zero;
    }

    /// <summary>
    /// (EN) True if any additive term could not be integrated.
    /// (ZH) 只要有一个加法项无法积分即为 true。
    /// </summary>
    public override bool ContainsDontKnow => Substeps.Any(s => s.ContainsDontKnow);
}

/// <summary>
/// (EN) Constant multiple rule: ∫ c·f(x) dx = c·∫ f(x) dx, pulling a constant factor out of the
/// integral.
/// (ZH) 常数倍规则 ∫ c·f(x) dx = c·∫ f(x) dx：把常数因子提到积分号外。
/// </summary>
public sealed record ConstantTimesRule : IntegrationRule
{
    /// <summary>
    /// (EN) The constant factor c extracted from the integrand.
    /// (ZH) 从被积表达式中提取出的常数因子 c。
    /// </summary>
    public required Expression Constant { get; init; }

    /// <summary>
    /// (EN) The remaining non-constant factor whose integral is delegated to <c>Substeps</c>.
    /// (ZH) 剩余的非恒定因子，其积分交由 <c>Substeps</c> 完成。
    /// </summary>
    public required Expression Other { get; init; }

    /// <summary>
    /// (EN) Rule that integrates <c>Other</c>.
    /// (ZH) 用于对 <c>Other</c> 积分的规则。
    /// </summary>
    public required IntegrationRule Substeps { get; init; }

    /// <summary>
    /// (EN) Returns c multiplied by the integral of the non-constant factor.
    /// (ZH) 返回 c 与非恒定因子积分之积。
    /// </summary>
    public override Expression Eval() => Multiply(Constant, Substeps.Eval());

    /// <summary>
    /// (EN) True if the remaining factor could not be integrated.
    /// (ZH) 若剩余因子无法积分则为 true。
    /// </summary>
    public override bool ContainsDontKnow => Substeps.ContainsDontKnow;
}

/// <summary>
/// (EN) Applies an algebraic or trigonometric rewrite of the integrand before integrating; the
/// rewritten form drives the substeps while the final result is just the substep result.
/// (ZH) 积分前先对被积表达式做代数或三角恒等变形；变形后的表达式驱动子步骤，最终结果直接取自子步骤。
/// </summary>
public sealed record RewriteRule : IntegrationRule
{
    /// <summary>
    /// (EN) The rewritten integrand that the substeps operate on.
    /// (ZH) 子步骤实际作用的、变形后的被积表达式。
    /// </summary>
    public required Expression Rewritten { get; init; }

    /// <summary>
    /// (EN) Rule that integrates the rewritten form.
    /// (ZH) 对变形后表达式积分的规则。
    /// </summary>
    public required IntegrationRule Substeps { get; init; }

    /// <summary>
    /// (EN) Returns the substep result unchanged, since the rewrite only changes how the integral
    /// is computed, not its value.
    /// (ZH) 原样返回子步骤的结果：变形只改变积分的计算方式，不改变其值。
    /// </summary>
    public override Expression Eval() => Substeps.Eval();

    /// <summary>
    /// (EN) True if the rewritten integral could not be completed.
    /// (ZH) 若变形后的积分仍无法完成则为 true。
    /// </summary>
    public override bool ContainsDontKnow => Substeps.ContainsDontKnow;
}

/// <summary>
/// (EN) Integration by substitution: after integrating in the substituted variable the result is
/// mapped back by replacing <c>UVar</c> with <c>UFunc</c>.
/// (ZH) 换元积分：先在换元后的变量上积分，再把结果中的 <c>UVar</c> 替换回 <c>UFunc</c>。
/// </summary>
public sealed record URule : IntegrationRule
{
    /// <summary>
    /// (EN) The substitution variable introduced by the change of variable.
    /// (ZH) 换元引入的替代变量。
    /// </summary>
    public required Expression UVar { get; init; }

    /// <summary>
    /// (EN) The expression of the new variable in terms of the original one, substituted back
    /// into the result.
    /// (ZH) 新变量用原变量表示的表达式，最终会被代回结果。
    /// </summary>
    public required Expression UFunc { get; init; }

    /// <summary>
    /// (EN) Rule that integrates the transformed integrand in the substituted variable.
    /// (ZH) 在替代变量上对变换后的被积表达式积分的规则。
    /// </summary>
    public required IntegrationRule Substeps { get; init; }

    /// <summary>
    /// (EN) Evaluates the substeps and substitutes <c>UVar</c> back by <c>UFunc</c>.
    /// (ZH) 求值子步骤，并把 <c>UVar</c> 换回 <c>UFunc</c>。
    /// </summary>
    public override Expression Eval()
    {
        var result = Substeps.Eval();
        return Structure.Substitute(UVar, UFunc, result);
    }

    /// <summary>
    /// (EN) True if the transformed integral could not be completed.
    /// (ZH) 若变换后的积分无法完成则为 true。
    /// </summary>
    public override bool ContainsDontKnow => Substeps.ContainsDontKnow;
}

/// <summary>
/// (EN) Integration by parts: ∫ u dv = u·v - ∫ v du, where <c>VStep</c> produces v and
/// <c>SecondStep</c> produces ∫ v du.
/// (ZH) 分部积分：∫ u dv = u·v - ∫ v du，其中 <c>VStep</c> 给出 v，<c>SecondStep</c> 给出 ∫ v du。
/// </summary>
public sealed record PartsRule : IntegrationRule
{
    /// <summary>
    /// (EN) The factor u that is differentiated in the parts formula.
    /// (ZH) 分部积分中作微分的那一项 u。
    /// </summary>
    public required Expression U { get; init; }

    /// <summary>
    /// (EN) The factor dv that is integrated in the parts formula.
    /// (ZH) 分部积分中作积分的那一项 dv。
    /// </summary>
    public required Expression Dv { get; init; }

    /// <summary>
    /// (EN) Rule producing v = ∫ dv.
    /// (ZH) 给出 v = ∫ dv 的规则。
    /// </summary>
    public required IntegrationRule VStep { get; init; }

    /// <summary>
    /// (EN) Rule producing the remaining integral ∫ v du.
    /// (ZH) 给出剩余积分 ∫ v du 的规则。
    /// </summary>
    public required IntegrationRule SecondStep { get; init; }

    /// <summary>
    /// (EN) Returns u·v minus the value of the remaining integral.
    /// (ZH) 返回 u·v 减去剩余积分的值。
    /// </summary>
    public override Expression Eval()
    {
        var v = VStep.Eval();
        return Subtract(Multiply(U, v), SecondStep.Eval());
    }

    /// <summary>
    /// (EN) True if either the v computation or the remaining integral is unresolved.
    /// (ZH) 若 v 的计算或剩余积分任一未解决则为 true。
    /// </summary>
    public override bool ContainsDontKnow => VStep.ContainsDontKnow || SecondStep.ContainsDontKnow;
}

/// <summary>
/// (EN) Handles cyclic integration by parts, where the original integral reappears after several
/// rounds with coefficient c; the equation I = S - c·I is solved as I = S/(1-c).
/// (ZH) 处理循环分部积分：原始积分在若干轮后以系数 c 重新出现，由等式 I = S - c·I 解出 I = S/(1-c)。
/// </summary>
public sealed record CyclicPartsRule : IntegrationRule
{
    /// <summary>
    /// (EN) The successive parts steps forming the cycle; terms are combined with alternating signs.
    /// (ZH) 构成循环的各步分部积分；各项以交替符号组合。
    /// </summary>
    public required IReadOnlyList<PartsRule> PartsRules { get; init; }

    /// <summary>
    /// (EN) The coefficient c with which the original integral reappears; the derivation requires
    /// c ≠ 1 so that the division below stays finite.
    /// (ZH) 原始积分重现时的系数 c；推导要求 c ≠ 1，以保证下面的除法有意义。
    /// </summary>
    public required Expression Coefficient { get; init; }

    /// <summary>
    /// (EN) Sums the alternating boundary terms and divides by (1 - c) to obtain the closed form.
    /// (ZH) 对交替出现的边界项求和，再除以 (1 - c) 得到闭式结果。
    /// </summary>
    public override Expression Eval()
    {
        Expression result = Zero;
        int sign = 1;
        foreach (var rule in PartsRules)
        {
            var term = sign > 0
                ? Multiply(rule.U, rule.VStep.Eval())
                : Negate(Multiply(rule.U, rule.VStep.Eval()));
            result = Add(result, term);
            sign *= -1;
        }
        return Divide(result, Subtract(One, Coefficient));
    }

    /// <summary>
    /// (EN) True if any step of the cycle is unresolved.
    /// (ZH) 若循环中任一步骤未解决则为 true。
    /// </summary>
    public override bool ContainsDontKnow => PartsRules.Any(r => r.ContainsDontKnow);
}

/// <summary>
/// (EN) Holds several candidate rules for the same integral; the first alternative is the preferred
/// derivation and the remaining ones are fallbacks considered by the solver.
/// (ZH) 为同一积分持有多个候选规则；第一个是首选推导，其余是求解器考虑的备选方案。
/// </summary>
public sealed record AlternativeRule : IntegrationRule
{
    /// <summary>
    /// (EN) Candidate rules in preference order; must contain at least one element.
    /// (ZH) 按优先级排列的候选规则；至少包含一个元素。
    /// </summary>
    public required IReadOnlyList<IntegrationRule> Alternatives { get; init; }

    /// <summary>
    /// (EN) Returns the result of the first (preferred) alternative.
    /// (ZH) 返回第一个（首选）备选的结果。
    /// </summary>
    public override Expression Eval() => Alternatives[0].Eval();

    /// <summary>
    /// (EN) True if any alternative is unresolved.
    /// (ZH) 若任一备选未解决则为 true。
    /// </summary>
    public override bool ContainsDontKnow => Alternatives.Any(a => a.ContainsDontKnow);
}

/// <summary>
/// (EN) Marker rule for an integral the solver could not evaluate. It keeps the derivation total and
/// reports the failure through <c>ContainsDontKnow</c>.
/// (ZH) 求解器无法求值的积分的标记规则。它使推导保持完整，并通过 <c>ContainsDontKnow</c> 报告失败。
/// </summary>
public sealed record DontKnowRule : AtomicRule
{
    /// <summary>
    /// (EN) Always <c>true</c>: this rule always denotes an unresolved integral.
    /// (ZH) 恒为 <c>true</c>：该规则始终表示未解决的积分。
    /// </summary>
    public override bool ContainsDontKnow => true;

    /// <summary>
    /// (EN) Returns an unevaluated symbolic placeholder: a Log-style node carrying
    /// (Integrand, Variable), which downstream code renders as the unresolved integral.
    /// (ZH) 返回未求值的符号占位节点：一个携带 (Integrand, Variable) 的 Log 型节点，下游代码将其
    /// 呈现为未解决的积分。
    /// </summary>
    public override Expression Eval()
    {
        return new Expression.FunctionN(FunctionNType.Log, new[] { Integrand, Variable });
    }
}

// ──────────────────────────────────────────────
//  More advanced atomic rules
// ──────────────────────────────────────────────

/// <summary>
/// (EN) ∫ 1/(a + b·x²) dx, evaluated with atan or with a logarithm depending on the sign of the
/// denominator.
/// (ZH) ∫ 1/(a + b·x²) dx，根据分母的符号选择 atan 形式或对数形式。
/// </summary>
public sealed record ArctanRule : AtomicRule
{
    /// <summary>
    /// (EN) Constant term a of the denominator.
    /// (ZH) 分母的常数项 a。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) Coefficient b of the quadratic term.
    /// (ZH) 二次项系数 b。
    /// </summary>
    public required Expression B { get; init; }

    /// <summary>
    /// (EN) Evaluates ∫ 1/(a+b·x²) dx. For numeric a,b the branch is chosen from their signs so the
    ///      result stays real: an arctangent when a·b &gt; 0, and a logarithm (artanh) when a·b &lt; 0.
    ///      For symbolic signs it falls back to the arctangent form.
    /// (ZH) 求 ∫ 1/(a+b·x²) dx。当 a、b 为数值时，根据符号选择分支以保证结果为实数：a·b &gt; 0 用反正切，
    ///      a·b &lt; 0 用对数（artanh）；符号未知时退化为反正切形式。
    /// </summary>
    public override Expression Eval()
    {
        var x = Variable;

        // (EN) Numeric signs let us pick a real-valued branch. (ZH) 数值符号使我们能选择实值分支。
        if (A is Expression.Number an && B is Expression.Number bn)
        {
            // (EN) Use the numeric absolute values so the result contains no symbolic Abs nodes.
            // (ZH) 使用数值绝对值，使结果不含符号 Abs 节点。
            var p = AbsOf(A);               // (EN) |a|. (ZH) |a|。
            var q = AbsOf(B);               // (EN) |b|. (ZH) |b|。
            var sqrtPQ = Sqrt(Multiply(p, q));
            bool sameSign = an.Value.IsNegative == bn.Value.IsNegative;

            if (sameSign)
            {
                // (EN) ∫ dx/(a+bx²) = s/√(|a||b|)·atan(x·√(|b|/|a|)), with s = sign(a). (ZH) ∫ dx/(a+bx²) = s/√(|a||b|)·atan(x·√(|b|/|a|))，s = sign(a)。
                var body = Multiply(Divide(One, sqrtPQ),
                    Atan(Multiply(x, Sqrt(Divide(q, p)))));
                return an.Value.IsNegative ? Negate(body) : body;
            }

            // (EN) ∫ dx/(a+bx²) = s/(2√(|a||b|))·ln((√|a|+x√|b|)/(√|a|-x√|b|)). (ZH) ∫ dx/(a+bx²) = s/(2√(|a||b|))·ln((√|a|+x√|b|)/(√|a|-x√|b|))。
            var sp = Sqrt(p);
            var sq = Sqrt(q);
            var ratio = Divide(Add(sp, Multiply(x, sq)), Subtract(sp, Multiply(x, sq)));
            var logBody = Multiply(Divide(One, Multiply(Two, sqrtPQ)), Ln(ratio));
            return an.Value.IsNegative ? Negate(logBody) : logBody;
        }

        // (EN) Symbolic fallback: use the arctangent form. (ZH) 符号未知时回退：使用反正切形式。
        var sqrtBA = Sqrt(Divide(B, A));
        return Divide(One, Multiply(A, sqrtBA)) * Atan(Multiply(sqrtBA, x));
    }

    /// <summary>
    /// (EN) Absolute value that folds a numeric argument exactly, otherwise keeps the Abs node.
    /// (ZH) 对数值参数精确折叠的绝对值，否则保留 Abs 节点。
    /// </summary>
    private static Expression AbsOf(Expression e) =>
        e is Expression.Number n ? new Expression.Number(Rational.Abs(n.Value)) : Abs(e);
}

/// <summary>
/// (EN) Rule for a nested power ∫ (c·(a+b·x)^d)^e dx; it currently assumes a = 0 and b = 1, so it
/// reduces to the plain power rule applied to <c>BaseVal</c>.
/// (ZH) 嵌套幂 ∫ (c·(a+b·x)^d)^e dx 的规则；目前假定 a = 0、b = 1，因而退化为对 <c>BaseVal</c>
/// 直接应用幂规则。
/// </summary>
public sealed record NestedPowRule : AtomicRule
{
    /// <summary>
    /// (EN) Base expression (a+b·x).
    /// (ZH) 底表达式 (a+b·x)。
    /// </summary>
    public required Expression BaseVal { get; init; }

    /// <summary>
    /// (EN) Inner exponent.
    /// (ZH) 内层指数。
    /// </summary>
    public required Expression InnerExp { get; init; }

    /// <summary>
    /// (EN) Outer exponent.
    /// (ZH) 外层指数。
    /// </summary>
    public required Expression OuterExp { get; init; }

    /// <summary>
    /// (EN) Returns ln(BaseVal) when n = -1, otherwise BaseVal^(n+1)/(n+1) with n the outer exponent.
    /// (ZH) 当 n = -1 时返回 ln(BaseVal)，否则返回 BaseVal^(n+1)/(n+1)，其中 n 为外层指数。
    /// </summary>
    public override Expression Eval()
    {
        // (EN) ∫ (a+bx)^n dx = (a+bx)^(n+1) / (b*(n+1)). (ZH) ∫ (a+bx)^n dx = (a+bx)^(n+1) / (b*(n+1))。
        // (EN) For now assume a=0, b=1, so it's just PowerRule. (ZH) 目前假定 a=0, b=1，因此退化为 PowerRule。
        var n = OuterExp;
        if (Expression.IsMinusOne(n))
            return Ln(BaseVal);
        return Pow(BaseVal, n + One) / (n + One);
    }
}

/// <summary>
/// (EN) Rule for integer powers/products of trig and hyperbolic functions (sin^m·cos^n,
///      tan^m·sec^n, sinh^m·cosh^n, ...); the antiderivative is computed once by
///      <c>TrigIntegrals</c> and stored in <c>Result</c>.
/// (ZH) 三角与双曲函数整数次幂/乘积（sin^m·cos^n、tan^m·sec^n、sinh^m·cosh^n 等）的规则；原函数由
///      <c>TrigIntegrals</c> 计算一次并存入 <c>Result</c>。
/// </summary>
public sealed record TrigPowerRule : AtomicRule
{
    /// <summary>
    /// (EN) The precomputed antiderivative of the integrand.
    /// (ZH) 预先计算的被积函数原函数。
    /// </summary>
    public required Expression Result { get; init; }

    /// <summary>
    /// (EN) Returns the stored antiderivative.
    /// (ZH) 返回已存储的原函数。
    /// </summary>
    public override Expression Eval() => Result;
}

/// <summary>
/// (EN) ∫ 1/√(a+b·x+c·x²) dx = (1/√c)·ln|2cx + b + 2√c·√(a+bx+cx²)|.
/// (ZH) 二次式平方根倒数积分：∫ 1/√(a+b·x+c·x²) dx = (1/√c)·ln|2cx + b + 2√c·√(a+bx+cx²)|。
/// </summary>
public sealed record ReciprocalSqrtQuadraticRule : AtomicRule
{
    /// <summary>
    /// (EN) Constant term a of the quadratic.
    /// (ZH) 二次式的常数项 a。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) Linear coefficient b of the quadratic.
    /// (ZH) 二次式的一次项系数 b。
    /// </summary>
    public required Expression B { get; init; }

    /// <summary>
    /// (EN) Quadratic coefficient c; the formula requires c &gt; 0 so that √c is real.
    /// (ZH) 二次项系数 c；公式要求 c &gt; 0 以保证 √c 为实数。
    /// </summary>
    public required Expression C { get; init; }

    /// <summary>
    /// (EN) Applies the formula above with x = Variable.
    /// (ZH) 以 x = Variable 应用上述公式。
    /// </summary>
    public override Expression Eval()
    {
        var x = Variable;
        // (EN) ∫ 1/√(a+bx+cx²) dx = 1/√c * ln|2cx+b + 2√c*√(a+bx+cx²)|.
        // (ZH) ∫ 1/√(a+bx+cx²) dx = 1/√c * ln|2cx+b + 2√c*√(a+bx+cx²)|。
        var sqrtC = Sqrt(C);
        var inner = Two * C * x + B + Two * sqrtC * Sqrt(A + B * x + C * x * x);
        return Divide(One, sqrtC) * Ln(inner);
    }
}

/// <summary>
/// (EN) Rational function integration via partial fractions. The heavy lifting is done inside the
/// solver, so the rule only carries the numerator and denominator.
/// (ZH) 通过部分分式进行有理函数积分。实际计算在求解器内部完成，本规则只保存分子与分母。
/// </summary>
public sealed record RatintRule : AtomicRule
{
    /// <summary>
    /// (EN) Numerator polynomial of the rational integrand.
    /// (ZH) 有理被积函数的分子多项式。
    /// </summary>
    public required Expression Numerator { get; init; }

    /// <summary>
    /// (EN) Denominator polynomial of the rational integrand.
    /// (ZH) 有理被积函数的分母多项式。
    /// </summary>
    public required Expression Denominator { get; init; }

    /// <summary>
    /// (EN) Returns the integrand unchanged: this rule is merely a marker and the actual
    /// partial-fraction result is produced by the solver.
    /// (ZH) 原样返回被积表达式：本规则只是标记，真正的部分分式结果由求解器给出。
    /// </summary>
    public override Expression Eval() => Integrand; // Placeholder - handled inline in solver
}

/// <summary>
/// (EN) ∫ 1/(x-a) dx = ln|x-a|.
/// (ZH) 简单对数积分规则 ∫ 1/(x-a) dx = ln|x-a|。
/// </summary>
public sealed record SimpleLogRule : AtomicRule
{
    /// <summary>
    /// (EN) The linear term x-a.
    /// (ZH) 线性项 x-a。
    /// </summary>
    public required Expression LinearTerm { get; init; }

    /// <summary>
    /// (EN) Returns ln(LinearTerm).
    /// (ZH) 返回 ln(LinearTerm)。
    /// </summary>
    public override Expression Eval() => Ln(LinearTerm);
}

/// <summary>
/// (EN) ∫ 1/(x-a)^k dx = (x-a)^(1-k)/(1-k), valid for k ≠ 1.
/// (ZH) 简单幂积分规则 ∫ 1/(x-a)^k dx = (x-a)^(1-k)/(1-k)，要求 k ≠ 1。
/// </summary>
public sealed record SimplePowerRule : AtomicRule
{
    /// <summary>
    /// (EN) The linear term x-a.
    /// (ZH) 线性项 x-a。
    /// </summary>
    public required Expression LinearTerm { get; init; }

    /// <summary>
    /// (EN) The exponent k; k = 1 would make the denominator vanish and is handled by the logarithm
    /// rule instead.
    /// (ZH) 指数 k；k = 1 会使分母为零，此种情况改由对数规则处理。
    /// </summary>
    public required Expression Exponent { get; init; }

    /// <summary>
    /// (EN) Returns (x-a)^(1-k)/(1-k) using <c>LinearTerm</c> and <c>Exponent</c>.
    /// (ZH) 使用 <c>LinearTerm</c> 与 <c>Exponent</c> 返回 (x-a)^(1-k)/(1-k)。
    /// </summary>
    public override Expression Eval()
    {
        var k = Exponent;
        return Pow(LinearTerm, One - k) / (One - k);
    }
}

// ──────────────────────────────────────────────
//  Special function rules
// ──────────────────────────────────────────────

/// <summary>
/// (EN) ∫ e^(-x²) dx = √π/2 · erf(x).
/// (ZH) 误差函数积分规则 ∫ e^(-x²) dx = √π/2 · erf(x)。
/// </summary>
public sealed record ErfRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns the symbolic expression (√π/2)·erf(x); erf is not evaluated numerically here,
    /// only represented as a function node.
    /// (ZH) 返回符号表达式 (√π/2)·erf(x)；此处不对 erf 作数值求值，仅表示为函数节点。
    /// </summary>
    public override Expression Eval()
    {
        // (EN) Symbolic result: sqrt(pi)/2 * erf(x). (ZH) 符号结果：sqrt(pi)/2 * erf(x)。
        // (EN) Since we don't have erf built in, return the symbolic representation. (ZH) 因未内置 erf，仅返回符号表示。
        return (Sqrt(Pi) / Two) * new Expression.Function(FunctionType.Erf, Variable);
    }
}

/// <summary>
/// (EN) Piecewise integration: a different rule is attached to each sub-domain of the integrand.
/// (ZH) 分段积分：被积表达式的每个子区间对应一条不同的积分规则。
/// </summary>
public sealed record PiecewiseRule : IntegrationRule
{
    /// <summary>
    /// (EN) The (rule, condition) pairs making up the piecewise definition; the first piece is the
    /// one currently evaluated.
    /// (ZH) 构成分段定义的 (规则, 条件) 对；当前只求值第一个分段。
    /// </summary>
    public required IReadOnlyList<(IntegrationRule Rule, Expression Condition)> Pieces { get; init; }

    /// <summary>
    /// (EN) Returns the result of the first piece; the conditions are not yet consulted.
    /// (ZH) 返回第一个分段的结果；条件暂未参与判断。
    /// </summary>
    public override Expression Eval()
    {
        // (EN) For now, just return the first piece. (ZH) 目前仅返回第一个分段。
        return Pieces[0].Rule.Eval();
    }

    /// <summary>
    /// (EN) True if any piece is unresolved.
    /// (ZH) 若任一分段未解决则为 true。
    /// </summary>
    public override bool ContainsDontKnow => Pieces.Any(p => p.Rule.ContainsDontKnow);
}

// ──────────────────────────────────────────────
//  Special function rules (sin(x)/x → Si(x) etc.)
// ──────────────────────────────────────────────

/// <summary>
/// (EN) ∫ sin(x)/x dx = Si(x).
/// (ZH) 正弦积分函数规则 ∫ sin(x)/x dx = Si(x)。
/// </summary>
public sealed record SiRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns the Si(x) function node in the integration variable.
    /// (ZH) 返回以积分变量为自变量的 Si(x) 函数节点。
    /// </summary>
    public override Expression Eval() => new Expression.Function(FunctionType.Si, Variable);
}

/// <summary>
/// (EN) ∫ cos(x)/x dx = Ci(x).
/// (ZH) 余弦积分函数规则 ∫ cos(x)/x dx = Ci(x)。
/// </summary>
public sealed record CiRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns the Ci(x) function node in the integration variable.
    /// (ZH) 返回以积分变量为自变量的 Ci(x) 函数节点。
    /// </summary>
    public override Expression Eval() => new Expression.Function(FunctionType.Ci, Variable);
}

/// <summary>
/// (EN) ∫ sinh(x)/x dx = Shi(x).
/// (ZH) 双曲正弦积分函数规则 ∫ sinh(x)/x dx = Shi(x)。
/// </summary>
public sealed record ShiRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns the Shi(x) function node in the integration variable.
    /// (ZH) 返回以积分变量为自变量的 Shi(x) 函数节点。
    /// </summary>
    public override Expression Eval() => new Expression.Function(FunctionType.Shi, Variable);
}

/// <summary>
/// (EN) ∫ cosh(x)/x dx = Chi(x).
/// (ZH) 双曲余弦积分函数规则 ∫ cosh(x)/x dx = Chi(x)。
/// </summary>
public sealed record ChiRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns the Chi(x) function node in the integration variable.
    /// (ZH) 返回以积分变量为自变量的 Chi(x) 函数节点。
    /// </summary>
    public override Expression Eval() => new Expression.Function(FunctionType.Chi, Variable);
}

/// <summary>
/// (EN) ∫ eˣ/x dx = Ei(x).
/// (ZH) 指数积分函数规则 ∫ eˣ/x dx = Ei(x)。
/// </summary>
public sealed record EiRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns the Ei(x) function node in the integration variable.
    /// (ZH) 返回以积分变量为自变量的 Ei(x) 函数节点。
    /// </summary>
    public override Expression Eval() => new Expression.Function(FunctionType.Ei, Variable);
}

/// <summary>
/// (EN) ∫ 1/ln(x) dx = Li(x).
/// (ZH) 对数积分函数规则 ∫ 1/ln(x) dx = Li(x)。
/// </summary>
public sealed record LiRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns the Li(x) function node in the integration variable.
    /// (ZH) 返回以积分变量为自变量的 Li(x) 函数节点。
    /// </summary>
    public override Expression Eval() => new Expression.Function(FunctionType.Li, Variable);
}

/// <summary>
/// (EN) ∫ sin(x²) dx = √(π/2)·FresnelS(√(2/π)·x).
/// (ZH) 菲涅耳正弦积分规则 ∫ sin(x²) dx = √(π/2)·FresnelS(√(2/π)·x)。
/// </summary>
public sealed record FresnelSRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns √(π/2)·FresnelS(√(2/π)·x); the argument scaling converts the standard Fresnel
    /// definition to the ∫sin(x²) convention.
    /// (ZH) 返回 √(π/2)·FresnelS(√(2/π)·x)；参数的缩放把标准菲涅耳定义换算到 ∫sin(x²) 的约定。
    /// </summary>
    public override Expression Eval()
    {
        var sqrt2pi = Sqrt(Two / Pi);
        return Sqrt(Pi / Two) * new Expression.Function(FunctionType.FresnelS, sqrt2pi * Variable);
    }
}

/// <summary>
/// (EN) ∫ cos(x²) dx = √(π/2)·FresnelC(√(2/π)·x).
/// (ZH) 菲涅耳余弦积分规则 ∫ cos(x²) dx = √(π/2)·FresnelC(√(2/π)·x)。
/// </summary>
public sealed record FresnelCRule : AtomicRule
{
    /// <summary>
    /// (EN) Returns √(π/2)·FresnelC(√(2/π)·x); the argument scaling converts the standard Fresnel
    /// definition to the ∫cos(x²) convention.
    /// (ZH) 返回 √(π/2)·FresnelC(√(2/π)·x)；参数的缩放把标准菲涅耳定义换算到 ∫cos(x²) 的约定。
    /// </summary>
    public override Expression Eval()
    {
        var sqrt2pi = Sqrt(Two / Pi);
        return Sqrt(Pi / Two) * new Expression.Function(FunctionType.FresnelC, sqrt2pi * Variable);
    }
}

// ──────────────────────────────────────────────
//  Orthogonal polynomial rules
// ──────────────────────────────────────────────

/// <summary>
/// (EN) Base type for rules that integrate a specific orthogonal polynomial family P_n(x) by
/// exploiting that family's recurrence or derivative identity.
/// (ZH) 通过正交多项式族的递推关系或导数恒等式对其积分 P_n(x) 的规则基类型。
/// </summary>
public abstract record OrthogonalPolyRule : AtomicRule
{
    /// <summary>
    /// (EN) The polynomial degree n of the integrand.
    /// (ZH) 被积多项式的次数 n。
    /// </summary>
    public required Expression N { get; init; }

    /// <summary>
    /// (EN) Builds the symbolic function node for the given family and degree; shared by the
    /// concrete polynomial rules. (ZH) 为给定多项式族与次数构造符号函数节点；供各具体多项式规则共用。
    /// </summary>
    /// <param name="type">(EN) The orthogonal polynomial family. (ZH) 正交多项式族。</param>
    /// <param name="degree">(EN) The polynomial degree. (ZH) 多项式的次数。</param>
    /// <returns>(EN) The function node <c>type(degree, Variable)</c>. (ZH) 函数节点 <c>type(degree, Variable)</c>。</returns>
    protected Expression PolyEval(FunctionNType type, Expression degree) =>
        new Expression.FunctionN(type, new[] { degree, Variable });
}

/// <summary>
/// (EN) ∫ P_n(x) dx = (P_{n+1}(x) - P_{n-1}(x)) / (2n+1) for Legendre polynomials.
/// (ZH) Legendre 多项式积分规则 ∫ P_n(x) dx = (P_{n+1}(x) - P_{n-1}(x)) / (2n+1)。
/// </summary>
public sealed record LegendreRule : OrthogonalPolyRule
{
    /// <summary>
    /// (EN) Returns (P_{n+1} - P_{n-1}) / (2n+1) using the Legendre recurrence identity.
    /// (ZH) 利用 Legendre 递推恒等式返回 (P_{n+1} - P_{n-1}) / (2n+1)。
    /// </summary>
    public override Expression Eval()
    {
        var n = N;
        var p1 = PolyEval(FunctionNType.LegendreP, n + One);
        var pm1 = PolyEval(FunctionNType.LegendreP, n - One);
        return (p1 - pm1) / (Two * n + One);
    }
}

/// <summary>
/// (EN) ∫ T_n(x) dx = ½(T_{n+1}/(n+1) - T_{n-1}/(n-1)) for Chebyshev polynomials of the first kind.
/// (ZH) 第一类 Chebyshev 多项式积分规则 ∫ T_n(x) dx = ½(T_{n+1}/(n+1) - T_{n-1}/(n-1))。
/// </summary>
public sealed record ChebyshevTRule : OrthogonalPolyRule
{
    /// <summary>
    /// (EN) Returns ½(T_{n+1}/(n+1) - T_{n-1}/(n-1)); n = 1 makes the second term singular.
    /// (ZH) 返回 ½(T_{n+1}/(n+1) - T_{n-1}/(n-1))；n = 1 时第二项奇异。
    /// </summary>
    public override Expression Eval()
    {
        var n = N;
        var t1 = PolyEval(FunctionNType.ChebyshevT, n + One) / (n + One);
        var tm1 = PolyEval(FunctionNType.ChebyshevT, n - One) / (n - One);
        return (t1 - tm1) / Two;
    }
}

/// <summary>
/// (EN) ∫ U_n(x) dx = T_{n+1}(x)/(n+1) for Chebyshev polynomials of the second kind.
/// (ZH) 第二类 Chebyshev 多项式积分规则 ∫ U_n(x) dx = T_{n+1}(x)/(n+1)。
/// </summary>
public sealed record ChebyshevURule : OrthogonalPolyRule
{
    /// <summary>
    /// (EN) Returns T_{n+1}/(n+1) using the U_n ↔ T_{n+1} relation.
    /// (ZH) 利用 U_n ↔ T_{n+1} 的关系返回 T_{n+1}/(n+1)。
    /// </summary>
    public override Expression Eval()
    {
        var n = N;
        return PolyEval(FunctionNType.ChebyshevT, n + One) / (n + One);
    }
}

/// <summary>
/// (EN) ∫ H_n(x) dx = H_{n+1}(x)/(2(n+1)) for Hermite polynomials.
/// (ZH) Hermite 多项式积分规则 ∫ H_n(x) dx = H_{n+1}(x)/(2(n+1))。
/// </summary>
public sealed record HermiteRule : OrthogonalPolyRule
{
    /// <summary>
    /// (EN) Returns H_{n+1}/(2(n+1)) using the Hermite derivative identity.
    /// (ZH) 利用 Hermite 导数恒等式返回 H_{n+1}/(2(n+1))。
    /// </summary>
    public override Expression Eval()
    {
        var n = N;
        return PolyEval(FunctionNType.HermiteH, n + One) / (Two * (n + One));
    }
}

/// <summary>
/// (EN) ∫ L_n(x) dx = L_n(x) - L_{n+1}(x) for Laguerre polynomials.
/// (ZH) Laguerre 多项式积分规则 ∫ L_n(x) dx = L_n(x) - L_{n+1}(x)。
/// </summary>
public sealed record LaguerreRule : OrthogonalPolyRule
{
    /// <summary>
    /// (EN) Returns L_n - L_{n+1} using the Laguerre recurrence identity.
    /// (ZH) 利用 Laguerre 递推恒等式返回 L_n - L_{n+1}。
    /// </summary>
    public override Expression Eval()
    {
        var n = N;
        var ln = PolyEval(FunctionNType.LaguerreL, n);
        var ln1 = PolyEval(FunctionNType.LaguerreL, n + One);
        return ln - ln1;
    }
}

/// <summary>
/// (EN) ∫ L_n^k(x) dx = -L_{n+1}^(k-1)(x) for associated (generalized) Laguerre polynomials.
/// (ZH) 连带（广义）Laguerre 多项式积分规则 ∫ L_n^k(x) dx = -L_{n+1}^(k-1)(x)。
/// </summary>
public sealed record AssocLaguerreRule : OrthogonalPolyRule
{
    /// <summary>
    /// (EN) The order parameter k of the associated Laguerre polynomial.
    /// (ZH) 连带 Laguerre 多项式的阶参数 k。
    /// </summary>
    public required Expression K { get; init; }

    /// <summary>
    /// (EN) Builds the function node for the associated family using the shifted order k-1.
    /// (ZH) 使用平移后的阶 k-1 构造连带多项式族的函数节点。
    /// </summary>
    private Expression PolyEvalWithK(FunctionNType type, Expression degree) =>
        new Expression.FunctionN(type, new[] { degree, K - One, Variable });

    /// <summary>
    /// (EN) Returns -L_{n+1}^(k-1)(x).
    /// (ZH) 返回 -L_{n+1}^(k-1)(x)。
    /// </summary>
    public override Expression Eval()
    {
        var n = N;
        return -PolyEvalWithK(FunctionNType.AssocLaguerreL, n + One);
    }
}

/// <summary>
/// (EN) ∫ C_n^(a)(x) dx = C_{n+1}^(a-1)(x)/(2(a-1)) for Gegenbauer (ultraspherical) polynomials.
/// (ZH) Gegenbauer（超球）多项式积分规则 ∫ C_n^(a)(x) dx = C_{n+1}^(a-1)(x)/(2(a-1))。
/// </summary>
public sealed record GegenbauerRule : OrthogonalPolyRule
{
    /// <summary>
    /// (EN) The parameter a of the Gegenbauer polynomial; a = 1 makes the denominator vanish.
    /// (ZH) Gegenbauer 多项式的参数 a；a = 1 时分母为零。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) Returns C_{n+1}/(2(a-1)) using the Gegenbauer recurrence identity.
    /// (ZH) 利用 Gegenbauer 递推恒等式返回 C_{n+1}/(2(a-1))。
    /// </summary>
    public override Expression Eval()
    {
        var n = N;
        return PolyEval(FunctionNType.GegenbauerC, n + One) / (Two * (A - One));
    }
}

/// <summary>
/// (EN) ∫ P_n^(a,b)(x) dx = 2·P_{n+1}^(a-1,b-1)(x)/(n+a+b) for Jacobi polynomials.
/// (ZH) Jacobi 多项式积分规则 ∫ P_n^(a,b)(x) dx = 2·P_{n+1}^(a-1,b-1)(x)/(n+a+b)。
/// </summary>
public sealed record JacobiRule : OrthogonalPolyRule
{
    /// <summary>
    /// (EN) First Jacobi parameter a.
    /// (ZH) Jacobi 第一参数 a。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) Second Jacobi parameter b.
    /// (ZH) Jacobi 第二参数 b。
    /// </summary>
    public required Expression B { get; init; }

    /// <summary>
    /// (EN) Returns 2·P_{n+1}/(n+a+b) using the Jacobi recurrence identity.
    /// (ZH) 利用 Jacobi 递推恒等式返回 2·P_{n+1}/(n+a+b)。
    /// </summary>
    public override Expression Eval()
    {
        var n = N;
        return Two * PolyEval(FunctionNType.JacobiP, n + One) / (n + A + B);
    }
}

// ── Special function rules matching SymPy ──────────────────────

/// <summary>
/// (EN) ∫ exp(-(ax+b)²)·erf(y·(ax+b)) dx = -2√π/a · T(√2·(ax+b), y), where T(u, y) is the Owens T
/// function.
/// (ZH) ∫ exp(-(ax+b)²)·erf(y·(ax+b)) dx = -2√π/a · T(√2·(ax+b), y)，其中 T(u, y) 为 Owens T 函数。
/// </summary>
public sealed record OwensTRule : AtomicRule
{
    /// <summary>
    /// (EN) Coefficient a of the linear argument ax+b; the result divides by a, so a ≠ 0.
    /// (ZH) 线性参数 ax+b 的系数 a；结果需除以 a，故 a ≠ 0。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) The offset b of the linear argument ax+b.
    /// (ZH) 线性参数 ax+b 的常数项 b。
    /// </summary>
    public required Expression B { get; init; }

    /// <summary>
    /// (EN) The second argument y of the Owens T function.
    /// (ZH) Owens T 函数的第二个参数 y。
    /// </summary>
    public required Expression Y { get; init; }

    /// <summary>
    /// (EN) Returns -2√π/a · T(√2·(ax+b), y).
    /// (ZH) 返回 -2√π/a · T(√2·(ax+b), y)。
    /// </summary>
    public override Expression Eval()
    {
        var v = Variable;
        var a = A;
        // (EN) T(√2·(a·x+b), y). (ZH) T(√2·(a·x+b), y)。
        var tArg = Sqrt(Two) * (a * v + B);
        var t = new Expression.FunctionN(FunctionNType.OwensT, new[] { tArg, Y });
        return Negate(Two * Sqrt(Pi) / a * t);
    }
}

/// <summary>
/// (EN) ∫ polylog(b, a·x)/x dx = polylog(b+1, a·x): the polylogarithm order is raised by one.
/// (ZH) ∫ polylog(b, a·x)/x dx = polylog(b+1, a·x)：多重对数函数的阶提高一阶。
/// </summary>
public sealed record PolylogRule : AtomicRule
{
    /// <summary>
    /// (EN) The scale factor a multiplying the variable inside the polylogarithm.
    /// (ZH) 多重对数函数中变量前的缩放系数 a。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) The order b of the polylogarithm; the result uses order b+1.
    /// (ZH) 多重对数函数的阶 b；结果使用阶 b+1。
    /// </summary>
    public required Expression B { get; init; }

    /// <summary>
    /// (EN) Returns the polylogarithm of order b+1 evaluated at a·x.
    /// (ZH) 返回在 a·x 处求值的 b+1 阶多重对数函数。
    /// </summary>
    public override Expression Eval()
    {
        var x = Variable;
        var inner = A * x;
        return new Expression.FunctionN(FunctionNType.Polylog, new[] { B + One, inner });
    }
}

/// <summary>
/// (EN) ∫ x^e · exp(a·x) dx = x^e · (-a·x)^(-e) · Γ(e+1, -a·x)/a, where e is a non-negative integer
/// and Γ is the upper incomplete gamma function.
/// (ZH) ∫ x^e · exp(a·x) dx = x^e · (-a·x)^(-e) · Γ(e+1, -a·x)/a，其中 e 为非负整数，
/// Γ 为上不完全 gamma 函数。
/// </summary>
public sealed record UpperGammaRule : AtomicRule
{
    /// <summary>
    /// (EN) The exponent coefficient a of exp(a·x); the result divides by a, so a ≠ 0.
    /// (ZH) exp(a·x) 的指数系数 a；结果需除以 a，故 a ≠ 0。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) The power e of x; must be a non-negative integer.
    /// (ZH) x 的幂次 e；必须为非负整数。
    /// </summary>
    public required Expression E { get; init; }

    /// <summary>
    /// (EN) Returns x^e·(-a·x)^(-e)·Γ(e+1, -a·x)/a.
    /// (ZH) 返回 x^e·(-a·x)^(-e)·Γ(e+1, -a·x)/a。
    /// </summary>
    public override Expression Eval()
    {
        var x = Variable;
        var a = A;
        var e = E;
        var arg = Negate(a) * x;
        var gamma = new Expression.FunctionN(FunctionNType.UpperGamma, new[] { e + One, arg });
        return Pow(x, e) * Pow(arg, Negate(e)) * gamma / a;
    }
}

/// <summary>
/// (EN) ∫ 1/√(a - d·sin²(x)) dx = F(x, d/a)/√a, where F(φ, m) is the elliptic integral of the first
/// kind.
/// (ZH) ∫ 1/√(a - d·sin²(x)) dx = F(x, d/a)/√a，其中 F(φ, m) 为第一类椭圆积分。
/// </summary>
public sealed record EllipticFRule : AtomicRule
{
    /// <summary>
    /// (EN) The scale a; the result divides by √a, so a &gt; 0.
    /// (ZH) 缩放因子 a；结果需除以 √a，故 a &gt; 0。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) The coefficient d of sin²(x); the elliptic parameter is m = d/a.
    /// (ZH) sin²(x) 的系数 d；椭圆参数为 m = d/a。
    /// </summary>
    public required Expression D { get; init; }

    /// <summary>
    /// (EN) Returns F(x, d/a)/√a.
    /// (ZH) 返回 F(x, d/a)/√a。
    /// </summary>
    public override Expression Eval()
    {
        var x = Variable;
        var m = D / A;
        var f = new Expression.FunctionN(FunctionNType.EllipticF, new[] { x, m });
        return f / Sqrt(A);
    }
}

/// <summary>
/// (EN) ∫ √(a - d·sin²(x)) dx = E(x, d/a)·√a, where E(φ, m) is the elliptic integral of the second
/// kind.
/// (ZH) ∫ √(a - d·sin²(x)) dx = E(x, d/a)·√a，其中 E(φ, m) 为第二类椭圆积分。
/// </summary>
public sealed record EllipticERule : AtomicRule
{
    /// <summary>
    /// (EN) The scale a; it must be non-negative so that √a is real.
    /// (ZH) 缩放因子 a；必须非负以保证 √a 为实数。
    /// </summary>
    public required Expression A { get; init; }

    /// <summary>
    /// (EN) The coefficient d of sin²(x); the elliptic parameter is m = d/a.
    /// (ZH) sin²(x) 的系数 d；椭圆参数为 m = d/a。
    /// </summary>
    public required Expression D { get; init; }

    /// <summary>
    /// (EN) Returns E(x, d/a)·√a.
    /// (ZH) 返回 E(x, d/a)·√a。
    /// </summary>
    public override Expression Eval()
    {
        var x = Variable;
        var m = D / A;
        var e = new Expression.FunctionN(FunctionNType.EllipticE, new[] { x, m });
        return e * Sqrt(A);
    }
}
