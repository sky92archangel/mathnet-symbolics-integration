using MathNet.Symbolics.Integration.Core;
using static MathNet.Symbolics.Integration.Core.Operators;

namespace MathNet.Symbolics.Integration;

/// <summary>
/// (EN) Core symbolic integration engine.  Applies a sequence of strategies
///      (atomic rules, sum splitting, constant extraction, u-substitution,
///      rational-function decomposition, integration by parts) to find an
///      antiderivative for a given integrand and variable.
/// (ZH) 符号积分核心引擎。按顺序尝试多种策略（原子规则、求和拆分、常数提取、
///      换元积分、有理函数分解、分部积分）来寻找给定被积表达式关于指定变量的原函数。
/// </summary>
internal class IntegrationSolver
{
    /// <summary>
    /// (EN) Upper bound on the operator-node count of an integrand the solver will attempt. Large
    ///      expressions are produced by intermediate rewrite/parts steps; refusing them keeps the
    ///      search from suffering exponential expression blow-up.
    /// (ZH) 求解器愿意尝试的被积表达式算符节点数上限。中间的重写/分部积分步骤会产生巨大的表达式；
    ///      拒绝它们可避免搜索遭受表达式规模的指数级膨胀。
    /// </summary>
    private const int MaxIntegrandNodes = 1200;

    private readonly int _maxDepth;
    private int _depth;

    /// <summary>
    /// (EN) Remaining work budget: every recursion consumes one unit, forcing termination even when
    ///      the strategy search branches heavily on integrals it cannot solve.
    /// (ZH) 剩余工作预算：每次递归消耗一个单位，即使策略搜索在无法求解的积分上大量分支也能保证终止。
    /// </summary>
    private int _budget = 20_000;

    /// <summary>
    /// (EN) True when the current subtree bottomed out on the depth or budget guard. Results computed
    ///      while starved may be artifacts of the limit instead of genuine failures, so they are not
    ///      memoized; any result whose subtree never starved is cached (success or failure alike).
    /// (ZH) 当前子树是否触发了深度或预算上限。处于上限状态时得到的结果可能只是受限制的产物而非真正的
    ///      失败，因此不记忆；凡是子树从未触发上限的结果（无论成功或失败）都会被缓存。
    /// </summary>
    private bool _starved;

    /// <summary>
    /// (EN) Memoized successful sub-results, keyed by (integrand, variable), so that the same
    ///      subintegral reached along different search paths is solved only once.
    /// (ZH) 按 (被积式, 变量) 记忆已成功求解的子结果，使不同搜索路径到达的同一子积分只求解一次。
    /// </summary>
    private readonly Dictionary<(Expression, Expression), IntegrationRule> _cache = new();

    /// <summary>
    /// (EN) Subproblems on the current recursion path; reaching one again means a cycle and is
    ///      reported as unsolvable, which breaks integrals that reappear under integration by parts.
    /// (ZH) 当前递归路径上的子问题；再次到达即构成环，报告为不可解，从而打破分部积分中重现的积分。
    /// </summary>
    private readonly HashSet<(Expression, Expression)> _active = new();

    /// <summary>
    /// (EN) Creates the solver with a configurable recursion depth limit.
    /// (ZH) 创建求解器，可配置递归深度上限。
    /// </summary>
    /// <param name="maxDepth">(EN) Maximum recursion depth before giving up. (ZH) 放弃前的最大递归深度。</param>
    public IntegrationSolver(int maxDepth = 12)
    {
        _maxDepth = maxDepth;
    }

    /// <summary>
    /// (EN) Find the antiderivative of <paramref name="integrand"/> with respect to
    ///      <paramref name="variable"/>, returning a rule tree that can be evaluated. Successful
    ///      results are memoized; cyclic subproblems and budget exhaustion yield a DontKnowRule.
    /// (ZH) 求 <paramref name="integrand"/> 关于 <paramref name="variable"/> 的原函数，返回可求值的
    ///      规则树。成功结果会被记忆；子问题成环或预算耗尽则返回 DontKnowRule。
    /// </summary>
    public IntegrationRule Solve(Expression integrand, Expression variable)
    {
        var key = (integrand, variable);
        if (_cache.TryGetValue(key, out var cached)) return cached;
        if (_active.Contains(key))
            return new DontKnowRule { Integrand = integrand, Variable = variable };
        // (EN) Refuse pathologically large integrands (artifacts of earlier steps). (ZH) 拒绝异常庞大的被积式（早期步骤的产物）。
        if (Structure.CountOperators(integrand) > MaxIntegrandNodes)
            return new DontKnowRule { Integrand = integrand, Variable = variable };
        if (_depth >= _maxDepth || _budget <= 0)
        {
            _starved = true;
            return new DontKnowRule { Integrand = integrand, Variable = variable };
        }

        _budget--;
        _depth++;
        _active.Add(key);
        // (EN) Track starvation for this subtree independently of any ancestor. (ZH) 独立跟踪本子树的受限状态，不受祖先影响。
        var outerStarved = _starved;
        _starved = false;
        IntegrationRule result;
        try
        {
            result = SolveCore(integrand, variable);
        }
        finally
        {
            _active.Remove(key);
            _depth--;
        }
        var starvedHere = _starved;
        _starved = outerStarved || starvedHere;
        // (EN) Only a result whose own subtree never starved is budget-independent and safe to reuse.
        // (ZH) 只有自身子树从未触发上限的结果才与预算无关，可安全复用。
        if (!starvedHere) _cache[key] = result;
        return result;
    }

    /// <summary>
    /// (EN) Applies the strategy chain to a subproblem; split out from <see cref="Solve"/> so that
    ///      memoization, cycle detection and the depth/budget guards wrap every attempt.
    /// (ZH) 对子问题应用策略链；从 <see cref="Solve"/> 中拆出，使记忆化、环检测与深度/预算守卫能包裹
    ///      每一次尝试。
    /// </summary>
    private IntegrationRule SolveCore(Expression integrand, Expression variable)
    {
        // (EN) 1. Atomic rules (direct function matching). (ZH) 1. 原子规则（直接函数匹配）。
        var rule = MatchAtomicRules(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 2. Sum rule: ∫(f+g) = ∫f + ∫g. (ZH) 2. 求和规则：∫(f+g) = ∫f + ∫g。
        rule = MatchSumRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3. Constant extraction: ∫ a*f(x) = a*∫f. (ZH) 3. 常数提取：∫ a*f(x) = a*∫f。
        rule = MatchConstantTimesRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3b. Integer powers/products of trig & hyperbolic functions. (ZH) 3b. 三角与双曲函数的整数次幂/乘积。
        rule = TryTrigPowersRule(integrand, variable);
        if (rule is not null) return rule;

        // (EN) 3b-2. Product-to-sum for sin/cos with different linear arguments. (ZH) 3b-2. 不同线性参数的 sin/cos 积化和差。
        rule = TryTrigProductToSumRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3b-3. e^(ax)·sin(bx) / e^(ax)·cos(bx) cyclic form. (ZH) 3b-3. e^(ax)·sin(bx) / e^(ax)·cos(bx) 循环形式。
        rule = TryExpTimesTrigRule(integrand, variable);
        if (rule is not null) return rule;

        // (EN) 3c. Rational function with a quadratic denominator. (ZH) 3c. 二次分母的有理函数。
        rule = TryQuadraticDenomRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-2. Polynomial long division for improper rational functions. (ZH) 3c-2. 假分式的多项式长除法。
        rule = TryPolynomialDivisionRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-3. Full rational-function integration (partial fractions). (ZH) 3c-3. 通用有理函数积分（部分分式）。
        rule = TryRationalFunctionRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-4. Weierstrass substitution for a rational function of sin(x)/cos(x). (ZH) 3c-4. sin(x)/cos(x) 有理函数的 Weierstrass 代换。
        rule = TryWeierstrassRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-5. Exponential substitution t = e^x. (ZH) 3c-5. 指数换元 t = e^x。
        rule = TryExpSubstitutionRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-6. Square-root substitution t = √x. (ZH) 3c-6. 根式换元 t = √x。
        rule = TrySqrtSubstitutionRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-7. Trigonometric substitution x = sin θ for √(1-x²) forms. (ZH) 3c-7. √(1-x²) 形式的三角换元 x = sin θ。
        rule = TryTrigSqrtSubstitutionRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-8. Fractional-linear square-root substitution √((a x+b)/(c x+d)). (ZH) 3c-8. 根式线性换元 √((a x+b)/(c x+d))。
        rule = TrySqrtFractionalLinearRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-9. Euler substitution for √(a+b x+c x²). (ZH) 3c-9. √(a+b x+c x²) 的 Euler 代换。
        rule = TryEulerSubstitutionRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-10. Chebyshev substitution for binomial differentials. (ZH) 3c-10. 二项微分的切比雪夫换元。
        rule = TryChebyshevSubstitutionRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 3c-11. Nested affine power ((a+bx)^d)^e → (a+bx)^(d·e). (ZH) 3c-11. 嵌套仿射幂 ((a+bx)^d)^e → (a+bx)^(d·e)。
        rule = TryNestedPowRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) If the integrand is a genuine rational function that partial fractions could not solve,
        //      the later heuristics (substitution/parts) will not help and can blow up; stop here.
        // (ZH) 若被积式确为有理函数但部分分式无法求解，后续启发式（换元/分部）也无效且可能爆炸；在此停止。
        if (RationalIntegrator.TryToRationalFunction(integrand, variable, out _, out var deniedDen)
            && Polynomial.Degree(deniedDen) > 0)
            return new DontKnowRule { Integrand = integrand, Variable = variable };

        // (EN) 3d. Expand polynomials/products of sums, then integrate term by term. (ZH) 3d. 展开多项式/和式的乘积，再逐项积分。
        rule = TryExpandRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 4. Substitution (u-sub): f(g(x))*g'(x). (ZH) 4. 换元积分：f(g(x))*g'(x)。
        rule = TrySubstitutionRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 5. Rational function integration. (ZH) 5. 有理函数积分。
        rule = TryRationalRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 6. Integration by parts (LIATE). (ZH) 6. 分部积分（LIATE）。
        rule = TryPartsRule(integrand, variable);
        if (rule is not null && !rule.ContainsDontKnow) return rule;

        // (EN) 7. Fall back. (ZH) 7. 回退。
        return rule ?? new DontKnowRule { Integrand = integrand, Variable = variable };
    }

    /// <summary>
    /// (EN) Try to match the integrand against a library of known atomic (single-step) rules.
    /// (ZH) 尝试将被积表达式与已知原子（单步）规则库进行匹配。
    /// </summary>
    private IntegrationRule? MatchAtomicRules(Expression integrand, Expression variable)
    {
        // (EN) ∫ a dx (constant). (ZH) ∫ a dx（常数）。
        if (!Structure.ContainsVariable(integrand, variable))
            return new ConstantRule
            {
                Integrand = integrand, Variable = variable, Constant = integrand
            };

        // (EN) Distributional rules: δ⁽ⁿ⁾(a+bx) and Heaviside(mx+b)·g. (ZH) 分布规则：δ⁽ⁿ⁾(a+bx) 与 Heaviside(mx+b)·g。
        if (TryMatchDiracDelta(integrand, variable, out var diracRule))
            return diracRule;
        if (TryMatchHeaviside(integrand, variable) is { } heavisideRule)
            return heavisideRule;

        // (EN) Pattern: sin(x), cos(x), tan(x), etc. (ZH) 模式：sin(x), cos(x), tan(x) 等。
        if (integrand is Expression.Function f)
        {
            // (EN) Direct: f(x). (ZH) 直接形式：f(x)。
            if (f.Argument.Equals(variable))
            {
                var rule = MatchDirectTrig(f, variable);
                if (rule is not null) return rule;
            }
            // (EN) Linear argument: f(a*x + b). (ZH) 线性参数：f(a*x + b)。
            else if (TryGetLinearCoeffs(f.Argument, variable, out var coeffA, out var coeffB))
            {
                var result = TryLinearFunctionRule(f, coeffA, coeffB, variable);
                if (result is not null) return result;
            }
        }

        // (EN) ∫ x^n dx (power rule). (ZH) ∫ x^n dx（幂规则）。
        if (TryPowerMatch(integrand, variable, out var powerRule))
            return powerRule;

        // (EN) ∫ 1/x dx. (ZH) ∫ 1/x dx。
        if (integrand is Expression.Power { Base: var pb, Exponent: var pe } &&
            pb.Equals(variable) && Expression.IsMinusOne(pe))
        {
            return new ReciprocalRule { Integrand = integrand, Variable = variable, Base = variable };
        }

        // (EN) ∫ 1/√(1-x²) dx  (ArcsinRule). (ZH) ∫ 1/√(1-x²) dx（ArcsinRule）。
        if (TryMatchArcsin(integrand, variable, out var arcsinRule))
            return arcsinRule;

        // (EN) ∫ 1/√(ax²+bx+c) dx. (ZH) ∫ 1/√(ax²+bx+c) dx。
        if (TryMatchReciprocalSqrtQuadratic(integrand, variable, out var sqrtRule))
            return sqrtRule;

        // (EN) ∫ √(ax²+bx+c) dx  (SqrtQuadraticRule). (ZH) ∫ √(ax²+bx+c) dx（SqrtQuadraticRule）。
        if (TryMatchSqrtQuadratic(integrand, variable, out var sqrtQuadRule))
            return sqrtQuadRule;

        // (EN) ∫ (px+q)/√(ax²+bx+c) dx  (SqrtQuadraticDenomRule). (ZH) ∫ (px+q)/√(ax²+bx+c) dx（SqrtQuadraticDenomRule）。
        if (TryMatchSqrtQuadraticDenom(integrand, variable, out var sqrtDenomRule))
            return sqrtDenomRule;

        // (EN) ∫ P(x)·√(ax²+bx+c) dx  (SqrtQuadraticPolyRule). (ZH) ∫ P(x)·√(ax²+bx+c) dx（SqrtQuadraticPolyRule）。
        if (TryMatchSqrtQuadraticPoly(integrand, variable) is { } sqrtPolyRule)
            return sqrtPolyRule;

        // (EN) ∫ (P·x²+Q)/(x⁴+a·x²+b) dx  (BiquadraticRule). (ZH) ∫ (P·x²+Q)/(x⁴+a·x²+b) dx（BiquadraticRule）。
        if (TryMatchBiquadratic(integrand, variable) is { } biqRule)
            return biqRule;

        // (EN) ∫ 1/(a+bx²) dx  (ArctanRule). (ZH) ∫ 1/(a+bx²) dx（ArctanRule）。
        if (TryMatchArctan(integrand, variable, out var arctanRule))
            return arctanRule;

        // (EN) ∫ (a+b·x)^n dx  (AffinePowRule). (ZH) ∫ (a+b·x)^n dx（AffinePowRule）。
        if (TryMatchAffinePow(integrand, variable, out var affinePowRule))
            return affinePowRule;

        // (EN) ∫ exp(-x²) dx  (ErfRule). (ZH) ∫ exp(-x²) dx（ErfRule）。
        if (TryMatchErf(integrand, variable, out var erfRule))
            return erfRule;

        // (EN) ∫ sin(x)/x dx → Si(x),  ∫ eˣ/x dx → Ei(x). (ZH) ∫ sin(x)/x dx → Si(x)，∫ eˣ/x dx → Ei(x)。
        if (TryMatchSpecialFunction(integrand, variable, out var specialRule))
            return specialRule;

        // (EN) Orthogonal polynomials: ∫ P_n(x) dx, ∫ T_n(x) dx, etc. (ZH) 正交多项式：∫ P_n(x) dx、∫ T_n(x) dx 等。
        if (TryMatchOrthogonalPoly(integrand, variable, out var orthoRule))
            return orthoRule;

        return null;
    }

    /// <summary>
    /// (EN) Match ∫ x^n dx where the base is the integration variable.
    /// (ZH) 匹配 ∫ x^n dx，其中底数为积分变量。
    /// </summary>
    private bool TryPowerMatch(Expression integrand, Expression variable,
        out PowerRule? rule)
    {
        rule = null;
        if (integrand is Expression.Power p && p.Base.Equals(variable))
        {
            rule = new PowerRule
            {
                Integrand = integrand, Variable = variable,
                Base = p.Base, Exp = p.Exponent
            };
            return true;
        }
        // (EN) x^1 → x. (ZH) x^1 → x。
        if (integrand.Equals(variable))
        {
            rule = new PowerRule
            {
                Integrand = integrand, Variable = variable,
                Base = variable, Exp = One
            };
            return true;
        }
        return false;
    }

    /// <summary>
    /// (EN) Match the affine power ∫ (a+b·x)^n dx where the base is affine in the variable and the
    ///      exponent is a constant number.
    /// (ZH) 匹配仿射幂 ∫ (a+b·x)^n dx，其中底关于变量仿射、指数为常数。
    /// </summary>
    private bool TryMatchAffinePow(Expression integrand, Expression variable,
        out AffinePowRule? rule)
    {
        rule = null;
        if (integrand is not Expression.Power p) return false;
        if (p.Exponent is not Expression.Number) return false;
        // (EN) The base must be affine: a + b·x. (ZH) 底必须是仿射式 a + b·x。
        var b = TryExtractLinearCoeff(p.Base, variable);
        if (b is null || Expression.IsZero(b)) return false;
        rule = new AffinePowRule
        {
            Integrand = integrand, Variable = variable,
            Base = p.Base, Coeff = b, Exponent = p.Exponent
        };
        return true;
    }

    /// <summary>
    /// (EN) Match ∫ δ⁽ⁿ⁾(a+b·x) dx. (ZH) 匹配 ∫ δ⁽ⁿ⁾(a+b·x) dx。
    /// </summary>
    private bool TryMatchDiracDelta(Expression integrand, Expression variable,
        out DiracDeltaRule? rule)
    {
        rule = null;
        Expression? arg = null;
        int n = 0;

        if (integrand is Expression.Function df && df.Op == FunctionType.DiracDelta)
        {
            arg = df.Argument;
        }
        else if (integrand is Expression.FunctionN dn && dn.Op == FunctionNType.DiracDelta
            && dn.Arguments.Count == 2 && dn.Arguments[1] is Expression.Number nn
            && nn.Value.IsInteger && !nn.Value.IsNegative)
        {
            arg = dn.Arguments[0];
            n = nn.Value.ToInt32();
        }
        if (arg is null) return false;

        // (EN) arg = constTerm + coeffX·x. (ZH) arg = 常数项 + coeffX·x。
        if (!TryGetLinearCoeffs(arg, variable, out var coeffX, out var constTerm) || Expression.IsZero(coeffX))
            return false;
        rule = new DiracDeltaRule
        {
            Integrand = integrand, Variable = variable, N = n,
            A = constTerm, B = coeffX
        };
        return true;
    }

    /// <summary>
    /// (EN) Match ∫ Heaviside(m·x+b)·g(x) dx. (ZH) 匹配 ∫ Heaviside(m·x+b)·g(x) dx。
    /// </summary>
    private IntegrationRule? TryMatchHeaviside(Expression integrand, Expression variable)
    {
        Expression? harg = null;
        Expression g = One;

        if (integrand is Expression.Function hf && hf.Op == FunctionType.Heaviside)
        {
            harg = hf.Argument;
        }
        else if (integrand is Expression.Product prod)
        {
            int idx = -1;
            for (int i = 0; i < prod.Factors.Count; i++)
                if (prod.Factors[i] is Expression.Function hf2 && hf2.Op == FunctionType.Heaviside)
                { idx = i; harg = hf2.Argument; break; }
            if (idx < 0) return null;
            var rest = prod.Factors.Where((_, i) => i != idx).ToList();
            g = rest.Count == 1 ? rest[0] : new Expression.Product(rest);
        }
        else
        {
            return null;
        }

        if (!TryGetLinearCoeffs(harg!, variable, out var m, out var b) || Expression.IsZero(m)) return null;
        var substep = Solve(g, variable);
        if (substep.ContainsDontKnow) return null;

        return new HeavisideRule
        {
            Integrand = integrand, Variable = variable,
            HArg = harg!,
            IBnd = Divide(Negate(b), m),
            Substeps = substep
        };
    }

    /// <summary>
    /// (EN) Match ∫ 1/(a + b·x²) dx pattern for the arctan rule.
    /// (ZH) 匹配 ∫ 1/(a + b·x²) dx 模式以应用 arctan 规则。
    /// </summary>
    private bool TryMatchArctan(Expression integrand, Expression variable,
        out ArctanRule? rule)
    {
        rule = null;
        // (EN) Pattern: 1 / (a + b*x^2)  or  1 / (a - b*x^2). (ZH) 模式：1 / (a + b*x^2) 或 1 / (a - b*x^2)。
        if (integrand is Expression.Power p &&
            Expression.IsMinusOne(p.Exponent) &&
            p.Base is Expression.Sum sum)
        {
            var terms = sum.Terms;
            if (terms.Count == 2)
            {
                Expression? a = null, bx2 = null;
                foreach (var t in terms)
                {
                    if (!Structure.ContainsVariable(t, variable))
                        a = t;
                    else if (IsX2Term(t, variable))
                        bx2 = t;
                }

                if (a is not null && bx2 is not null)
                {
                    Expression bCoeff = One;
                    if (bx2 is Expression.Product prod && prod.Factors.Count >= 1)
                        bCoeff = prod.Factors[0];

                    rule = new ArctanRule
                    {
                        Integrand = integrand, Variable = variable,
                        A = a, B = bCoeff
                    };
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// (EN) Detect ∫ 1/√(a + bx + cx²) dx pattern.
    /// (ZH) 检测 ∫ 1/√(a + bx + cx²) dx 模式。
    /// </summary>
    private bool TryMatchReciprocalSqrtQuadratic(Expression integrand, Expression variable,
        out ReciprocalSqrtQuadraticRule? rule)
    {
        rule = null;

        // (EN) Pattern 1: (a + bx + cx²)^(-1/2)  i.e. Power(..., -1/2). (ZH) 模式 1：(a + bx + cx²)^(-1/2)，即 Power(..., -1/2)。
        if (integrand is Expression.Power p &&
            p.Exponent is Expression.Number { Value: var expVal } &&
            expVal.Numerator == -1 && expVal.Denominator == 2)
        {
            if (TryExtractQuadratic(p.Base, variable, out var a, out var b, out var c))
            {
                rule = new ReciprocalSqrtQuadraticRule
                {
                    Integrand = integrand, Variable = variable,
                    A = a, B = b, C = c
                };
                return true;
            }
        }

        // (EN) Pattern 2: 1/√(...) → Pow(Sqrt(...), -1). (ZH) 模式 2：1/√(...) → Pow(Sqrt(...), -1)。
        if (integrand is Expression.Power p2 && Expression.IsMinusOne(p2.Exponent) &&
            p2.Base is Expression.Power sqrt && sqrt.Exponent is Expression.Number { Value: var half }
            && half.Numerator == 1 && half.Denominator == 2)
        {
            if (TryExtractQuadratic(sqrt.Base, variable, out var a2, out var b2, out var c2))
            {
                rule = new ReciprocalSqrtQuadraticRule
                {
                    Integrand = integrand, Variable = variable,
                    A = a2, B = b2, C = c2
                };
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// (EN) Match ∫ √(ax²+bx+c) dx → formula using the reciprocal sqrt quadratic.
    /// (ZH) 匹配 ∫ √(ax²+bx+c) dx → 使用平方根倒数二次式公式。
    /// </summary>
    private bool TryMatchSqrtQuadratic(Expression integrand, Expression variable,
        out SqrtQuadraticRule? rule)
    {
        rule = null;

        // (EN) Pattern: √(a+bx+cx²) = Power(..., 1/2). (ZH) 模式：√(a+bx+cx²) = Power(..., 1/2)。
        if (integrand is Expression.Power p &&
            p.Exponent is Expression.Number { Value: var expVal } &&
            expVal.Numerator == 1 && expVal.Denominator == 2)
        {
            if (TryExtractQuadratic(p.Base, variable, out var a, out var b, out var c))
            {
                var recipRule = new ReciprocalSqrtQuadraticRule
                {
                    Integrand = integrand, Variable = variable,
                    A = a, B = b, C = c
                };
                rule = new SqrtQuadraticRule
                {
                    Integrand = integrand, Variable = variable,
                    A = a, B = b, C = c,
                    ReciprocalStep = recipRule
                };
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// (EN) Match ∫ (P·x²+Q)/(x⁴+a·x²+b) dx with b &gt; 0 and 2√b &gt; a. (ZH) 匹配 b &gt; 0 且 2√b &gt; a 的 ∫ (P·x²+Q)/(x⁴+a·x²+b) dx。
    /// </summary>
    private static IntegrationRule? TryMatchBiquadratic(Expression integrand, Expression variable)
    {
        if (!RationalIntegrator.TryToRationalFunction(integrand, variable, out var nc, out var dc)) return null;
        if (Polynomial.Degree(dc) != 4) return null;
        // (EN) Even quartic: x⁴ + a·x² + b (no x³, no x terms). (ZH) 偶四次式：x⁴+a·x²+b（无 x³、x 项）。
        if (dc.Count < 5 || !dc[3].IsZero || !dc[1].IsZero) return null;
        var c = dc[4];
        if (c.IsZero) return null;
        var a = dc[2];
        var b = dc[0];
        if (b.IsZero || b.IsNegative) return null;
        // (EN) Need 2√b > a: automatic when a ≤ 0, else 4b > a². (ZH) 需 2√b > a：a ≤ 0 自动成立，否则 4b > a²。
        if (a.IsPositive && !(b * (Rational)4 - a * a).IsPositive) return null;

        // (EN) Even numerator of degree ≤ 2. (ZH) 次数 ≤ 2 的偶分子。
        if (Polynomial.Degree(nc) > 2) return null;
        if (nc.Count > 1 && !nc[1].IsZero) return null;
        var p = nc.Count > 2 ? nc[2] : Rational.Zero;
        var q = nc.Count > 0 ? nc[0] : Rational.Zero;

        return new BiquadraticRule
        {
            Integrand = integrand, Variable = variable,
            A = new Expression.Number(a / c), B = new Expression.Number(b / c),
            P = new Expression.Number(p), Q = new Expression.Number(q),
            Leading = new Expression.Number(c)
        };
    }

    /// <summary>
    /// (EN) Match ∫ P(x)·√(a+bx+cx²) dx by turning it into ∫ P(x)·Q/√Q dx and using the reduction.
    /// (ZH) 通过把 ∫ P(x)·√(a+bx+cx²) dx 改写为 ∫ P(x)·Q/√Q dx 并使用递推来匹配。
    /// </summary>
    private static IntegrationRule? TryMatchSqrtQuadraticPoly(Expression integrand, Expression variable)
    {
        if (integrand is not Expression.Product prod) return null;

        int idx = -1;
        Expression? quad = null;
        for (int i = 0; i < prod.Factors.Count; i++)
        {
            if (prod.Factors[i] is Expression.Power fp && fp.Exponent is Expression.Number he
                && he.Value.Numerator == 1 && he.Value.Denominator == 2
                && TryExtractQuadratic(fp.Base, variable, out _, out _, out var qc)
                && qc is Expression.Number qcn && qcn.Value.IsPositive)
            { idx = i; quad = fp.Base; break; }
        }
        if (idx < 0 || quad is null) return null;

        var rest = prod.Factors.Where((_, i) => i != idx).ToList();
        var pExpr = rest.Count == 1 ? rest[0] : new Expression.Product(rest);
        if (!TryToPoly(pExpr, variable, out var pc)) return null;

        if (!TryExtractQuadratic(quad, variable, out var ct, out var lt, out var qt)) return null;
        if (ct is not Expression.Number cn || lt is not Expression.Number ln || qt is not Expression.Number qn) return null;

        // (EN) Numerator becomes P·Q. (ZH) 分子变为 P·Q。
        var full = Polynomial.Multiply(pc, new List<Rational> { cn.Value, ln.Value, qn.Value });
        return new SqrtQuadraticPolyRule
        {
            Integrand = integrand, Variable = variable,
            Num = full, A = qn.Value, B = ln.Value, C = cn.Value
        };
    }

    /// <summary>
    /// (EN) Match ∫ (p·x+q)/√(a+bx+cx²) dx for c &gt; 0. (ZH) 匹配 c &gt; 0 的 ∫ (p·x+q)/√(a+bx+cx²) dx。
    /// </summary>
    private bool TryMatchSqrtQuadraticDenom(Expression integrand, Expression variable,
        out SqrtQuadraticDenomRule? rule)
    {
        rule = null;
        if (integrand is not Expression.Product prod) return false;

        int idx = -1;
        for (int i = 0; i < prod.Factors.Count; i++)
        {
            if (prod.Factors[i] is Expression.Power fp && fp.Exponent is Expression.Number fe
                && fe.Value.Numerator == -1 && fe.Value.Denominator == 2
                && TryExtractQuadratic(fp.Base, variable, out _, out _, out var qc)
                && qc is Expression.Number qcn && qcn.Value.IsPositive)
            { idx = i; break; }
        }
        if (idx < 0) return false;

        var quad = ((Expression.Power)prod.Factors[idx]).Base;
        var rest = prod.Factors.Where((_, i) => i != idx).ToList();
        var num = rest.Count == 1 ? rest[0] : new Expression.Product(rest);
        if (!TryExtractQuadratic(quad, variable, out var a, out var b, out var c)) return false;
        if (!TryExtractAffine(num, variable, out var p, out var q)) return false;
        if (Expression.IsZero(p) && Expression.IsZero(q)) return false;

        rule = new SqrtQuadraticDenomRule
        {
            Integrand = integrand, Variable = variable,
            A = a, B = b, C = c, P = p, Q = q
        };
        return true;
    }

    /// <summary>
    /// (EN) Match ∫ 1/√(1-x²) dx = asin(x), ∫ 1/√(a-b*x²) dx = asin(x√(b/a))/√b.
    /// (ZH) 匹配 ∫ 1/√(1-x²) dx = asin(x)，∫ 1/√(a-b*x²) dx = asin(x√(b/a))/√b。
    /// </summary>
    private bool TryMatchArcsin(Expression integrand, Expression variable,
        out ArcsinRule? rule)
    {
        rule = null;
        // (EN) Pattern: (1 - x²)^(-1/2)  or  (a - b*x²)^(-1/2). (ZH) 模式：(1 - x²)^(-1/2) 或 (a - b*x²)^(-1/2)。
        if (integrand is Expression.Power p &&
            p.Exponent is Expression.Number { Value: var expVal } &&
            expVal.Numerator == -1 && expVal.Denominator == 2 &&
            p.Base is Expression.Sum sum && sum.Terms.Count == 2)
        {
            Expression? constantTerm = null, x2Term = null;
            foreach (var t in sum.Terms)
            {
                if (!Structure.ContainsVariable(t, variable))
                    constantTerm = t;
                else if (IsX2Term(t, variable))
                    x2Term = t;
            }
            if (constantTerm is not null && x2Term is not null)
            {
                // (EN) Verify the x² coefficient is negative: a - b*x². (ZH) 确认 x² 系数为负：a - b*x²。
                bool negativeCoeff = x2Term is Expression.Product prod &&
                    prod.Factors.Count >= 2 &&
                    prod.Factors[0] is Expression.Number neg && neg.Value.IsMinusOne;
                if (!negativeCoeff)
                {
                    // (EN) Also check if x² term is just -x² directly. (ZH) 也检查 x² 项是否直接就是 -x²。
                    if (x2Term is Expression.Power)
                        return false; // (EN) +x², not -x². (ZH) 是 +x² 而非 -x²。
                }
                rule = new ArcsinRule
                {
                    Integrand = integrand, Variable = variable
                };
                return true;
            }
        }
        // (EN) Pattern: 1/√(1-x²)  via Sqrt. (ZH) 模式：通过 Sqrt 表示的 1/√(1-x²)。
        if (integrand is Expression.Power p2 && Expression.IsMinusOne(p2.Exponent) &&
            p2.Base is Expression.Power sqrt && sqrt.Exponent is Expression.Number { Value: var half2 }
            && half2.Numerator == 1 && half2.Denominator == 2 &&
            sqrt.Base is Expression.Sum sum2 && sum2.Terms.Count == 2)
        {
            Expression? ct = null, x2 = null;
            foreach (var t in sum2.Terms)
            {
                if (!Structure.ContainsVariable(t, variable)) ct = t;
                else if (IsX2Term(t, variable)) x2 = t;
            }
            if (ct is not null && x2 is not null)
            {
                // (EN) Verify negative coefficient. (ZH) 确认系数为负。
                bool negativeCoeff = x2 is Expression.Product prod2 &&
                    prod2.Factors.Count >= 2 &&
                    prod2.Factors[0] is Expression.Number neg2 && neg2.Value.IsMinusOne;
                if (!negativeCoeff && x2 is Expression.Power)
                    return false;
                rule = new ArcsinRule { Integrand = integrand, Variable = variable };
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// (EN) Match ∫ exp(-x²) dx = √π/2 · erf(x).
    /// (ZH) 匹配 ∫ exp(-x²) dx = √π/2 · erf(x)。
    /// </summary>
    private bool TryMatchErf(Expression integrand, Expression variable,
        out ErfRule? rule)
    {
        rule = null;
        // (EN) Pattern: exp(a·x² + b·x + c) with numeric a,b,c and a ≠ 0. (ZH) 模式：exp(a·x²+b·x+c)，a,b,c 为数值且 a ≠ 0。
        if (integrand is not Expression.Function { Op: FunctionType.Exp, Argument: var arg }) return false;

        if (!TryExtractQuadraticLoose(arg, variable, out var a, out var b, out var c)) return false;
        if (a is not Expression.Number an || b is not Expression.Number || c is not Expression.Number) return false;
        if (an.Value.IsZero) return false;

        rule = new ErfRule { Integrand = integrand, Variable = variable, A = a, B = b, C = c };
        return true;
    }

    /// <summary>
    /// (EN) Extracts (a, b, c) from a numeric polynomial a·x²+b·x+c of degree at most 2, allowing
    ///      missing lower-order terms (used by the exponential/erf rule).
    /// (ZH) 从至多二次的数值多项式 a·x²+b·x+c 中提取 (a, b, c)，允许缺少低阶项（供指数/erf 规则使用）。
    /// </summary>
    private static bool TryExtractQuadraticLoose(Expression expr, Expression v,
        out Expression a, out Expression b, out Expression c)
    {
        a = Zero; b = Zero; c = Zero;
        // (EN) Expand first so nested constant·sum factors distribute (e.g. -1·(2x-1) → -2x+1).
        // (ZH) 先展开，使嵌套的「常数·和式」因式分配（如 -1·(2x-1) → -2x+1）。
        expr = ExpandFully(expr, MaxIntegrandNodes) ?? expr;
        bool found = false;
        foreach (var t in Algebraic.Summands(expr))
        {
            Rational coeff = Rational.One;
            int deg = 0;
            foreach (var f in Algebraic.Factors(t))
            {
                if (!Structure.ContainsVariable(f, v))
                {
                    if (f is Expression.Number fn) coeff *= fn.Value;
                    else return false;
                }
                else if (f.Equals(v)) deg += 1;
                else if (f is Expression.Power p && p.Base.Equals(v)
                    && p.Exponent is Expression.Number ne && ne.Value.IsInteger && ne.Value.ToInt32() >= 0)
                    deg += ne.Value.ToInt32();
                else return false;
            }
            if (deg > 2) return false;
            var num = new Expression.Number(coeff);
            if (deg == 0) c = Add(c, num);
            else if (deg == 1) b = Add(b, num);
            else { a = Add(a, num); found = true; }
        }
        return found;
    }


    /// <summary>
    /// (EN) Match special function integrals: Si, Ci, Shi, Chi, Ei, Li, Fresnel,
    ///      Polylog, UpperGamma, OwensT, EllipticF, EllipticE.
    /// (ZH) 匹配特殊函数积分：Si、Ci、Shi、Chi、Ei、Li、Fresnel、
    ///      Polylog、UpperGamma、OwensT、EllipticF、EllipticE。
    /// </summary>
    private bool TryMatchSpecialFunction(Expression integrand, Expression variable,
        out IntegrationRule? rule)
    {
        rule = null;

        // (EN) ── Product-based patterns. (ZH) ── 基于乘积的模式。
        if (integrand is Expression.Product prod && prod.Factors.Count >= 2)
        {
            // (EN) --- Si / Ci / Shi / Chi / Ei patterns: func(x) / x ---
            // (ZH) --- Si / Ci / Shi / Chi / Ei 模式：func(x) / x ---
            Expression? funcPart = null, recipPart = null;
            foreach (var f in prod.Factors)
            {
                if (f is Expression.Power { Base: var pb, Exponent: var pe } &&
                    pb.Equals(variable) && Expression.IsMinusOne(pe))
                    recipPart = f;
                else if (f is Expression.Function fn && fn.Argument.Equals(variable) &&
                         fn.Op is FunctionType.Sin or FunctionType.Cos or FunctionType.Sinh
                                        or FunctionType.Cosh or FunctionType.Exp)
                    funcPart = f;
            }
            if (funcPart is not null && recipPart is not null)
            {
                rule = ((Expression.Function)funcPart).Op switch
                {
                    FunctionType.Sin => new SiRule { Integrand = integrand, Variable = variable },
                    FunctionType.Cos => new CiRule { Integrand = integrand, Variable = variable },
                    FunctionType.Sinh => new ShiRule { Integrand = integrand, Variable = variable },
                    FunctionType.Cosh => new ChiRule { Integrand = integrand, Variable = variable },
                    FunctionType.Exp => new EiRule { Integrand = integrand, Variable = variable },
                    _ => null,
                };
                return rule is not null;
            }

            // (EN) --- PolylogRule: polylog(b, a·x) / x ---
            // (ZH) --- PolylogRule: polylog(b, a·x) / x ---
            if (TryMatchPolylog(prod, variable, out rule))
                return true;

            // (EN) --- UpperGammaRule: x^n · exp(a·x) ---
            // (ZH) --- UpperGammaRule: x^n · exp(a·x) ---
            if (TryMatchUpperGamma(prod, variable, out rule))
                return true;

            // (EN) --- OwensTRule: exp(-(ax+b)²) · erf(y·(ax+b)) ---
            // (ZH) --- OwensTRule: exp(-(ax+b)²) · erf(y·(ax+b)) ---
            if (TryMatchOwensT(prod, variable, out rule))
                return true;
        }

        // (EN) ── Power-based patterns. (ZH) ── 基于幂的模式。

        // (EN) 1/ln(x) → Li(x). (ZH) 1/ln(x) → Li(x)。
        if (integrand is Expression.Power { Base: var lb, Exponent: var le } &&
            lb is Expression.Function { Op: FunctionType.Ln, Argument: var lnArg } &&
            lnArg.Equals(variable) && Expression.IsMinusOne(le))
        {
            rule = new LiRule { Integrand = integrand, Variable = variable };
            return true;
        }

        // (EN) sin(x²) / cos(x²) → Fresnel. (ZH) sin(x²) / cos(x²) → Fresnel。
        if (integrand is Expression.Function fresFn &&
            fresFn.Argument is Expression.Power pw && pw.Base.Equals(variable) &&
            pw.Exponent is Expression.Number ne && ne.Value.ToInt32() == 2)
        {
            rule = fresFn.Op switch
            {
                FunctionType.Sin => new FresnelSRule { Integrand = integrand, Variable = variable },
                FunctionType.Cos => new FresnelCRule { Integrand = integrand, Variable = variable },
                _ => null,
            };
            return rule is not null;
        }

        // (EN) --- EllipticFRule: 1/√(a - d·sin²(x)) ---
        // (ZH) --- EllipticFRule: 1/√(a - d·sin²(x)) ---
        if (TryMatchEllipticF(integrand, variable, out rule))
            return true;

        // (EN) --- EllipticERule: √(a - d·sin²(x)) ---
        // (ZH) --- EllipticERule: √(a - d·sin²(x)) ---
        if (TryMatchEllipticE(integrand, variable, out rule))
            return true;

        return false;
    }

    /// <summary>
    /// (EN) Match ∫ exp(-(ax+b)²) · erf(y·(ax+b)) dx for Owens T function.
    /// (ZH) 匹配 ∫ exp(-(ax+b)²) · erf(y·(ax+b)) dx 以应用 Owens T 函数。
    /// </summary>
    private bool TryMatchOwensT(Expression.Product prod, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        Expression? expBase = null, erfBase = null, y = null;
        Expression? a = null, b = null;

        foreach (var f in prod.Factors)
        {
            // (EN) exp(-(ax+b)²). (ZH) exp(-(ax+b)²)。
            if (f is Expression.Function { Op: FunctionType.Exp, Argument: var arg })
            {
                // (EN) -(ax+b)² is represented as Product([-1, Pow(Sum(ax, b), 2)]).
                // (ZH) -(ax+b)² 表示为 Product([-1, Pow(Sum(ax, b), 2)])。
                if (arg is Expression.Product neg &&
                    neg.Factors.Count == 2 &&
                    neg.Factors[0] is Expression.Number { Value.IsMinusOne: true } &&
                    neg.Factors[1] is Expression.Power pow &&
                    pow.Exponent is Expression.Number { Value: var ev } &&
                    ev.IsInteger && ev.ToInt32() == 2 &&
                    TryMatchLinearSum(pow.Base, v, out var ea, out var eb))
                {
                    expBase = f;
                    a = ea; b = eb;
                }
            }

            // (EN) erf(y·(ax+b)). (ZH) erf(y·(ax+b))。
            if (f is Expression.Function { Op: FunctionType.Erf, Argument: var erfArg })
            {
                if (TryMatchLinearSum(erfArg, v, out var erfa, out var erfb))
                {
                    // (EN) y=1, ax+b case. (ZH) y=1, ax+b 情形。
                    if (erfa is Expression.Number erfn && erfn.Value.IsOne &&
                        (erfb is Expression.Number erfn2 && erfn2.Value.IsZero))
                    {
                        erfBase = f; y = One;
                    }
                    else
                    {
                        erfBase = f;
                        y = erfa;
                    }
                }
                else if (erfArg is Expression.Product ep &&
                         ep.Factors.Count == 2 &&
                         TryMatchLinearSum(ep.Factors[^1], v, out var erfa2, out var erfb2))
                {
                    y = ep.Factors[0];
                    erfBase = f;
                }
            }
        }

        if (expBase is not null && erfBase is not null && a is not null && b is not null && y is not null)
        {
            // (EN) Skip y=1 (already handled by ErfRule). (ZH) 跳过 y=1（已由 ErfRule 处理）。
            if (y is Expression.Number yn && yn.Value.IsOne)
                return false;
            rule = new OwensTRule
            {
                Integrand = prod, Variable = v,
                A = a, B = b, Y = y
            };
            return true;
        }
        return false;
    }

    /// <summary>
    /// (EN) Match ∫ polylog(b, a·x) / x dx.
    /// (ZH) 匹配 ∫ polylog(b, a·x) / x dx。
    /// </summary>
    private bool TryMatchPolylog(Expression.Product prod, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        Expression? polyPart = null, recipPart = null;
        Expression? a = null, b = null;

        foreach (var f in prod.Factors)
        {
            if (f is Expression.Power { Base: var pb, Exponent: var pe } &&
                pb.Equals(v) && Expression.IsMinusOne(pe))
                recipPart = f;
            else if (f is Expression.FunctionN fn && fn.Op == FunctionNType.Polylog &&
                     fn.Arguments.Count == 2)
            {
                // (EN) fn.Arguments = [b, inner], inner = a*v. (ZH) fn.Arguments = [b, inner]，inner = a*v。
                b = fn.Arguments[0];
                if (fn.Arguments[1] is Expression.Product axProd &&
                    axProd.Factors.Count == 2 &&
                    axProd.Factors[^1].Equals(v))
                {
                    // (EN) a is the other factor. (ZH) a 是另一个因子。
                    a = axProd.Factors.Count == 2
                        ? (axProd.Factors[0].Equals(v) ? One : axProd.Factors[0])
                        : null;
                }
                else if (fn.Arguments[1].Equals(v))
                {
                    a = One;
                }
                polyPart = f;
            }
        }
        if (polyPart is not null && recipPart is not null && a is not null && b is not null)
        {
            rule = new PolylogRule
            {
                Integrand = prod, Variable = v, A = a, B = b
            };
            return true;
        }
        return false;
    }

    /// <summary>
    /// (EN) Match ∫ x^n · exp(a·x) dx  (n ≥ 0 integer) for the upper incomplete gamma function.
    /// (ZH) 匹配 ∫ x^n · exp(a·x) dx（n ≥ 0 整数）以应用上不完全 Gamma 函数。
    /// </summary>
    private bool TryMatchUpperGamma(Expression.Product prod, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        Expression? e = null, a = null;
        bool hasExp = false, hasPow = false;

        foreach (var f in prod.Factors)
        {
            // (EN) x^n  (n ≥ 0 integer). (ZH) x^n（n ≥ 0 整数）。
            if (f is Expression.Power { Base: var pb, Exponent: var pe } &&
                pb.Equals(v) && pe is Expression.Number { Value: var nv } &&
                nv.IsInteger && nv.ToInt32() >= 0)
            {
                hasPow = true;
                e = pe;
            }
            else if (f.Equals(v))
            {
                hasPow = true;
                e = One;
            }
            // (EN) exp(a·x). (ZH) exp(a·x)。
            else if (f is Expression.Function { Op: FunctionType.Exp, Argument: var expArg })
            {
                if (expArg is Expression.Product expProd &&
                    expProd.Factors.Count == 2 &&
                    expProd.Factors[^1].Equals(v))
                {
                    a = expProd.Factors[0];
                    hasExp = true;
                }
                else if (expArg.Equals(v))
                {
                    a = One;
                    hasExp = true;
                }
            }
        }

        if (hasPow && hasExp && a is not null && e is not null)
        {
            // (EN) Only use UpperGamma when n ≥ 2 — simpler cases use PartsRule.
            // (ZH) 仅在 n ≥ 2 时使用 UpperGamma——较简单的情形由 PartsRule 处理。
            if (e is Expression.Number ne && ne.Value.IsInteger && ne.Value.ToInt32() < 2)
                return false;
            rule = new UpperGammaRule
            {
                Integrand = prod, Variable = v, A = a, E = e
            };
            return true;
        }
        return false;
    }

    /// <summary>
    /// (EN) Match ∫ 1/√(a - d·sin²(x)) dx for the elliptic integral of the first kind.
    /// (ZH) 匹配 ∫ 1/√(a - d·sin²(x)) dx 以应用第一类椭圆积分。
    /// </summary>
    private bool TryMatchEllipticF(Expression integrand, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        // (EN) Pattern: Power(Power(a - d*sin(v)², 1/2), -1)   i.e. 1/sqrt(...)
        // (ZH) 模式：Power(Power(a - d*sin(v)², 1/2), -1)   即 1/sqrt(...)
        // (EN) or Power(a - d*sin(v)², -1/2). (ZH) 或 Power(a - d*sin(v)², -1/2)。
        Expression? baseExpr = null;
        if (integrand is Expression.Power tp &&
            tp.Exponent is Expression.Number tpn && tpn.Value.IsMinusOne &&
            tp.Base is Expression.Power sqrt && 
            sqrt.Exponent is Expression.Number { Value: var hv } &&
            hv.Numerator == 1 && hv.Denominator == 2)
        {
            baseExpr = sqrt.Base;
        }
        else if (integrand is Expression.Power tp2 &&
                 tp2.Exponent is Expression.Number { Value: var hv2 } &&
                 hv2.Numerator == -1 && hv2.Denominator == 2)
        {
            baseExpr = tp2.Base;
        }
        if (baseExpr is not null)
            return TryMatchEllipticArgs(baseExpr, v, out rule);
        return false;
    }

    /// <summary>
    /// (EN) Match ∫ √(a - d·sin²(x)) dx for the elliptic integral of the second kind.
    /// (ZH) 匹配 ∫ √(a - d·sin²(x)) dx 以应用第二类椭圆积分。
    /// </summary>
    private bool TryMatchEllipticE(Expression integrand, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        Expression? baseExpr = null;
        if (integrand is Expression.Power sqrt &&
            sqrt.Exponent is Expression.Number { Value: var hv } &&
            hv.Numerator == 1 && hv.Denominator == 2)
        {
            baseExpr = sqrt.Base;
        }
        if (baseExpr is not null)
            return TryMatchEllipticArgs(baseExpr, v, out rule, isE: true);
        return false;
    }

    /// <summary>
    /// (EN) Try to match a - d·sin(x)² form from a base expression, returning rule.
    /// (ZH) 尝试从底表达式匹配 a - d·sin(x)² 形式，返回规则。
    /// </summary>
    private bool TryMatchEllipticArgs(Expression baseExpr, Expression v,
        out IntegrationRule? rule, bool isE = false)
    {
        rule = null;
        if (baseExpr is Expression.Sum sum && sum.Terms.Count == 2)
        {
            // (EN) Find constant term 'a' and the -d*sin(v)² term. (ZH) 找到常数项 'a' 和 -d*sin(v)² 项。
            Expression? aConst = null, d = null;
            foreach (var t in sum.Terms)
            {
                if (!Structure.ContainsVariable(t, v))
                {
                    aConst = t;
                }
                else if (t is Expression.Product negProd && negProd.Factors.Count == 2 &&
                         negProd.Factors[0] is Expression.Number { Value.IsMinusOne: true })
                {
                    // (EN) -d*sin²(v). (ZH) -d*sin²(v)。
                    var rest = negProd.Factors[1];
                    if (rest is Expression.Power sinPow &&
                        sinPow.Exponent is Expression.Number { Value: var sv } &&
                        sv.IsInteger && sv.ToInt32() == 2 &&
                        sinPow.Base is Expression.Function { Op: FunctionType.Sin, Argument: var sa } &&
                        sa.Equals(v))
                    {
                        d = One;  // (EN) coefficient = 1 (since it's -1*sin²). (ZH) 系数 = 1（因为它是 -1*sin²）。
                    }
                    else if (rest is Expression.Product dProd &&
                             dProd.Factors.Count == 2 &&
                             dProd.Factors[1] is Expression.Power dSinPow &&
                             dSinPow.Exponent is Expression.Number { Value: var sv2 } &&
                             sv2.IsInteger && sv2.ToInt32() == 2 &&
                             dSinPow.Base is Expression.Function { Op: FunctionType.Sin, Argument: var sa2 } &&
                             sa2.Equals(v))
                    {
                        d = dProd.Factors[0]; // (EN) d coefficient. (ZH) d 系数。
                    }
                }
                else if (t is Expression.Product negProd2 &&
                         negProd2.Factors.Count == 3 &&
                         negProd2.Factors[0] is Expression.Number { Value.IsMinusOne: true })
                {
                    // (EN) -d*sin²(v). (ZH) -d*sin²(v)。
                    if (negProd2.Factors[2] is Expression.Power dSinPow2 &&
                        dSinPow2.Exponent is Expression.Number { Value: var sv3 } &&
                        sv3.IsInteger && sv3.ToInt32() == 2 &&
                        dSinPow2.Base is Expression.Function { Op: FunctionType.Sin, Argument: var sa3 } &&
                        sa3.Equals(v))
                    {
                        d = negProd2.Factors[1];
                    }
                }
            }
            if (aConst is not null && d is not null)
            {
                // (EN) Constraint: a != d. (ZH) 约束条件：a != d。
                if (aConst.Equals(d))
                    return false;
                if (isE)
                    rule = new EllipticERule { Integrand = baseExpr, Variable = v, A = aConst, D = d };
                else
                    rule = new EllipticFRule { Integrand = baseExpr, Variable = v, A = aConst, D = d };
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// (EN) Try to match a sum as a·v + b, i.e. a linear expression in v.
    ///      Returns a,b where the sum equals a*v + b.
    /// (ZH) 尝试将和式匹配为 a·v + b 形式（v 的线性表达式）。
    ///      返回 a,b 使得和式等于 a*v + b。
    /// </summary>
    private bool TryMatchLinearSum(Expression expr, Expression v,
        out Expression a, out Expression b)
    {
        a = Zero; b = Zero;
        if (expr.Equals(v))
        {
            a = One; b = Zero;
            return true;
        }
        if (expr is Expression.Sum sum)
        {
            Expression? aVal = null, bVal = null;
            foreach (var t in sum.Terms)
            {
                if (!Structure.ContainsVariable(t, v))
                    bVal = t;  // constant term
                else if (t.Equals(v))
                    aVal = (aVal is null) ? One : (aVal + One);
                else if (t is Expression.Product pt && pt.Factors.Count == 2 &&
                         pt.Factors[^1].Equals(v))
                {
                    var coeff = pt.Factors[0];
                    aVal = (aVal is null) ? coeff : (aVal + coeff);
                }
                else return false;
            }
            if (aVal is not null || bVal is not null)
            {
                a = aVal ?? Zero;
                b = bVal ?? Zero;
                return true;
            }
        }
        if (expr is Expression.Product pt2 && pt2.Factors.Count == 2 &&
            pt2.Factors[^1].Equals(v))
        {
            a = pt2.Factors[0];
            b = Zero;
            return true;
        }
        return false;
    }

    /// <summary>
    /// (EN) Match orthogonal polynomials: P_n(x), T_n(x), H_n(x), L_n(x), etc.
    /// (ZH) 匹配正交多项式：P_n(x)、T_n(x)、H_n(x)、L_n(x) 等。
    /// </summary>
    private bool TryMatchOrthogonalPoly(Expression integrand, Expression variable,
        out IntegrationRule? rule)
    {
        rule = null;
        if (integrand is Expression.FunctionN fn && fn.Arguments.Count >= 1 &&
            fn.Arguments[^1].Equals(variable))
        {
            var n = fn.Arguments[0];
            // (EN) For two-parameter polynomials, extract a,b from remaining args.
            // (ZH) 对于双参数多项式，从剩余参数中提取 a、b。
            Expression? a = fn.Arguments.Count >= 2 ? fn.Arguments[1] : null;
            Expression? b = fn.Arguments.Count >= 3 ? fn.Arguments[2] : null;

            rule = fn.Op switch
            {
                FunctionNType.LegendreP => new LegendreRule
                    { Integrand = integrand, Variable = variable, N = n },
                FunctionNType.ChebyshevT => new ChebyshevTRule
                    { Integrand = integrand, Variable = variable, N = n },
                FunctionNType.ChebyshevU => new ChebyshevURule
                    { Integrand = integrand, Variable = variable, N = n },
                FunctionNType.HermiteH => new HermiteRule
                    { Integrand = integrand, Variable = variable, N = n },
                FunctionNType.LaguerreL => new LaguerreRule
                    { Integrand = integrand, Variable = variable, N = n },
                FunctionNType.GegenbauerC => a is not null
                    ? new GegenbauerRule { Integrand = integrand, Variable = variable, N = n, A = a }
                    : null,
                FunctionNType.JacobiP => a is not null && b is not null
                    ? new JacobiRule { Integrand = integrand, Variable = variable, N = n, A = a, B = b }
                    : null,
                FunctionNType.AssocLaguerreL when fn.Arguments.Count >= 2
                    => new AssocLaguerreRule
                    {
                        Integrand = integrand, Variable = variable,
                        N = n, K = fn.Arguments[1]
                    },
                _ => null,
            };
            return rule is not null;
        }
        return false;
    }

    /// <summary>
    /// (EN) Check if an expression is of the form x² or b·x².
    /// (ZH) 检查表达式是否为 x² 或 b·x² 形式。
    /// </summary>
    private static bool IsX2Term(Expression e, Expression v) => e switch
    {
        Expression.Power tp => tp.Base.Equals(v) &&
            tp.Exponent is Expression.Number { Value.IsInteger: true } n && n.Value.ToInt32() == 2,
        Expression.Product prod => prod.Factors.Count >= 2 &&
            prod.Factors[^1] is Expression.Power tp2 && tp2.Base.Equals(v) &&
            tp2.Exponent is Expression.Number { Value.IsInteger: true } n2 && n2.Value.ToInt32() == 2,
        _ => false
    };

    /// <summary>
    /// (EN) Try to parse a quadratic expression: a + b*x + c*x².
    /// (ZH) 尝试解析二次表达式：a + b*x + c*x²。
    /// </summary>
    private static bool TryExtractQuadratic(Expression expr, Expression v,
        out Expression a, out Expression b, out Expression c)
    {
        a = Zero; b = Zero; c = Zero;
        var terms = Algebraic.Summands(expr);
        bool foundA = false, foundB = false, foundC = false;

        foreach (var t in terms)
        {
            if (!Structure.ContainsVariable(t, v))
            {
                a = t; foundA = true;
            }
            else if (t is Expression.Power tp && tp.Base.Equals(v) &&
                     tp.Exponent is Expression.Number ne && ne.Value.IsInteger && ne.Value.ToInt32() == 2)
            {
                c = One; foundC = true;
            }
            else if (t is Expression.Product prod &&
                     prod.Factors.Any(f => f is Expression.Power tp2 && tp2.Base.Equals(v) &&
                         tp2.Exponent is Expression.Number ne2 && ne2.Value.IsInteger && ne2.Value.ToInt32() == 2))
            {
                var nonPower = prod.Factors.Where(f => !(f is Expression.Power)).ToList();
                c = nonPower.Count == 1 ? nonPower[0] : new Expression.Product(nonPower);
                foundC = true;
            }
            else if (t.Equals(v))
            {
                b = One; foundB = true;
            }
            else if (t is Expression.Product prod2 && prod2.Factors.Any(f => f.Equals(v)))
            {
                var others = prod2.Factors.Where(f => !f.Equals(v)).ToList();
                b = others.Count == 1 ? others[0] : new Expression.Product(others);
                foundB = true;
            }
            else
            {
                // (EN) Any other variable term (e.g. x³) means this is not a quadratic. (ZH) 其它含变量项（如 x³）表示这不是二次式。
                return false;
            }
        }

        // (EN) Require a genuine non-zero quadratic term and at least one lower-order term, otherwise
        //      the sqrt/reciprocal-sqrt formulas (which divide by √c) are invalid.
        // (ZH) 必须存在非零二次项且至少存在一个低阶项，否则含 √c 的平方根公式无意义。
        return foundC && !Expression.IsZero(c) && (foundA || foundB);
    }

    /// <summary>
    /// (EN) Match ∫(f+g) dx = ∫f dx + ∫g dx by splitting the sum into additive terms.
    /// (ZH) 匹配 ∫(f+g) dx = ∫f dx + ∫g dx，将和式拆分为加法项分别积分。
    /// </summary>
    private IntegrationRule? MatchSumRule(Expression integrand, Expression variable)
    {
        var summands = Algebraic.Summands(integrand);
        if (summands.Count <= 1) return null;

        var substeps = summands.Select(t => Solve(t, variable)).ToList();
        return new AddRule
        {
            Integrand = integrand, Variable = variable, Substeps = substeps
        };
    }

    /// <summary>
    /// (EN) Match ∫ a·f(x) dx = a·∫ f(x) dx by extracting constant factors.
    /// (ZH) 匹配 ∫ a·f(x) dx = a·∫ f(x) dx，提取常数因子到积分号外。
    /// </summary>
    private IntegrationRule? MatchConstantTimesRule(Expression integrand, Expression variable)
    {
        var factors = Algebraic.Factors(integrand);
        Expression? coeff = null;
        var varTerms = new List<Expression>();

        foreach (var f in factors)
        {
            if (!Structure.ContainsVariable(f, variable))
                coeff = coeff is null ? f : Multiply(coeff, f);
            else
                varTerms.Add(f);
        }

        if (coeff is not null && varTerms.Count > 0 && !Expression.IsOne(coeff))
        {
            var remaining = varTerms.Count == 1
                ? varTerms[0]
                : new Expression.Product(varTerms);
            var subrule = Solve(remaining, variable);
            return new ConstantTimesRule
            {
                Integrand = integrand, Variable = variable,
                Constant = coeff, Other = remaining, Substeps = subrule
            };
        }
        return null;
    }

    // ── Substitution (u-sub) strategy ────────────────────────────
    // (EN) Try u-substitution: f(g(x))*g'(x) dx → ∫ f(u) du.
    // (ZH) 尝试换元积分：f(g(x))*g'(x) dx → ∫ f(u) du。

    /// <summary>
    /// (EN) Integrate integer powers/products of trig &amp; hyperbolic functions via the closed-form
    ///      routines in <see cref="TrigIntegrals"/>.
    /// (ZH) 借助 <see cref="TrigIntegrals"/> 中的闭式例程积分三角与双曲函数的整数次幂/乘积。
    /// </summary>
    private static IntegrationRule? TryTrigPowersRule(Expression integrand, Expression variable)
    {
        if (TrigIntegrals.TryIntegrate(integrand, variable, out var result))
            return new TrigPowerRule { Integrand = integrand, Variable = variable, Result = result };
        return null;
    }

    // ── Trigonometric product-to-sum ─────────────────────────────
    // (EN) sin(A)cos(B), sin(A)sin(B), cos(A)cos(B) with different linear arguments, rewritten via
    //      product-to-sum then integrated term by term.
    // (ZH) 不同线性参数的 sin(A)cos(B)、sin(A)sin(B)、cos(A)cos(B)，用积化和差改写后逐项积分。

    /// <summary>
    /// (EN) Try the trig product-to-sum rewrite for a product of exactly two sin/cos factors.
    /// (ZH) 对恰含两个 sin/cos 因子的乘积尝试积化和差改写。
    /// </summary>
    private IntegrationRule? TryTrigProductToSumRule(Expression integrand, Expression variable)
    {
        if (integrand is not Expression.Product prod || prod.Factors.Count != 2) return null;
        if (prod.Factors[0] is not Expression.Function f0 || !IsSinCos(f0.Op)) return null;
        if (prod.Factors[1] is not Expression.Function f1 || !IsSinCos(f1.Op)) return null;
        if (!Structure.ContainsVariable(f0.Argument, variable)
            || !Structure.ContainsVariable(f1.Argument, variable)) return null;

        var a = f0.Argument;
        var b = f1.Argument;
        bool f0Sin = f0.Op == FunctionType.Sin;
        bool f1Sin = f1.Op == FunctionType.Sin;
        var sum = Add(a, b);
        var diff = Subtract(a, b);
        var half = Divide(One, Two);

        // (EN) sinA·cosB = ½[sin(A+B)+sin(A-B)]; cosA·sinB = ½[sin(A+B)-sin(A-B)];
        //      sinA·sinB = ½[cos(A-B)-cos(A+B)]; cosA·cosB = ½[cos(A-B)+cos(A+B)].
        // (ZH) 积化和差四式。
        Expression rewritten = (f0Sin, f1Sin) switch
        {
            (true, false) => Multiply(half, Add(Sin(sum), Sin(diff))),
            (false, true) => Multiply(half, Subtract(Sin(sum), Sin(diff))),
            (true, true) => Multiply(half, Subtract(Cos(diff), Cos(sum))),
            (false, false) => Multiply(half, Add(Cos(diff), Cos(sum))),
        };

        var substep = Solve(rewritten, variable);
        if (substep.ContainsDontKnow) return null;
        return new RewriteRule
        {
            Integrand = integrand, Variable = variable,
            Rewritten = rewritten, Substeps = substep
        };
    }

    /// <summary>
    /// (EN) Whether the op is sine or cosine. (ZH) 是否为正弦或余弦。
    /// </summary>
    private static bool IsSinCos(FunctionType op) =>
        op == FunctionType.Sin || op == FunctionType.Cos;

    // ── e^(a·x)·sin(b·x) / e^(a·x)·cos(b·x) ────────────────────

    /// <summary>
    /// (EN) Match ∫ e^(a·x)·sin(b·x) dx and ∫ e^(a·x)·cos(b·x) dx (pure a·x and b·x arguments).
    /// (ZH) 匹配 ∫ e^(a·x)·sin(b·x) dx 与 ∫ e^(a·x)·cos(b·x) dx（参数为纯 a·x、b·x）。
    /// </summary>
    private static IntegrationRule? TryExpTimesTrigRule(Expression integrand, Expression variable)
    {
        if (integrand is not Expression.Product prod || prod.Factors.Count != 2) return null;
        Expression.Function? expFn = null, trigFn = null;
        foreach (var f in prod.Factors)
        {
            if (f is not Expression.Function fn) return null;
            if (fn.Op == FunctionType.Exp) expFn = fn;
            else if (IsSinCos(fn.Op)) trigFn = fn;
        }
        if (expFn is null || trigFn is null) return null;

        // (EN) Require pure a·x and b·x arguments (no offsets) so the closed form applies directly.
        // (ZH) 要求参数为纯 a·x、b·x（无偏移），以便直接套用闭式。
        if (!TryGetLinearCoeffs(expFn.Argument, variable, out var a, out var bExp) || !Expression.IsZero(bExp)) return null;
        if (!TryGetLinearCoeffs(trigFn.Argument, variable, out var b, out var bTrig) || !Expression.IsZero(bTrig)) return null;
        if (Expression.IsZero(a) || Expression.IsZero(b)) return null;

        return new ExpTimesTrigRule
        {
            Integrand = integrand, Variable = variable,
            A = a, B = b, IsCos = trigFn.Op == FunctionType.Cos
        };
    }

    // ── Quadratic-denominator rational rule ──────────────────────
    // (EN) ∫ (p·x+q)/(a·x²+b·x+c) dx with a linear-or-constant numerator.
    // (ZH) ∫ (p·x+q)/(a·x²+b·x+c) dx，分子为线性或常数。

    /// <summary>
    /// (EN) Match a rational function whose denominator is quadratic and numerator at most linear.
    /// (ZH) 匹配分母为二次式、分子至多一次的有理函数。
    /// </summary>
    private static IntegrationRule? TryQuadraticDenomRule(Expression integrand, Expression variable)
    {
        Expression? num, den;
        if (integrand is Expression.Power pd && Expression.IsMinusOne(pd.Exponent)
            && pd.Base is Expression.Sum)
        {
            num = One; den = pd.Base;
        }
        else if (integrand is Expression.Product prod)
        {
            var factors = prod.Factors.ToList();
            int idx = factors.FindIndex(f => f is Expression.Power fp
                && Expression.IsMinusOne(fp.Exponent) && fp.Base is Expression.Sum);
            if (idx < 0) return null;
            den = ((Expression.Power)factors[idx]).Base;
            var rest = factors.Where((_, i) => i != idx).ToList();
            num = rest.Count == 0 ? One : rest.Count == 1 ? rest[0] : new Expression.Product(rest);
        }
        else
        {
            return null;
        }

        // (EN) TryExtractQuadratic returns (constant, linear, quadratic); map to standard ax²+bx+c.
        // (ZH) TryExtractQuadratic 返回 (常数, 一次, 二次)；映射为标准形式 ax²+bx+c。
        if (!TryExtractQuadratic(den, variable, out var constTerm, out var linTerm, out var quadTerm)) return null;
        if (!TryExtractAffine(num, variable, out var p, out var q)) return null;
        if (Expression.IsZero(p) && Expression.IsZero(q)) return null;

        return new QuadraticDenomRule
        {
            Integrand = integrand, Variable = variable,
            A = quadTerm, B = linTerm, C = constTerm, P = p, Q = q
        };
    }

    /// <summary>
    /// (EN) Splits an at-most-linear polynomial into p·x + q. Returns false when it is not affine.
    /// (ZH) 将至多一次的表达式拆为 p·x + q；不是仿射式时返回 false。
    /// </summary>
    private static bool TryExtractAffine(Expression expr, Expression v,
        out Expression p, out Expression q)
    {
        p = Zero; q = Zero;
        if (!Structure.ContainsVariable(expr, v)) { p = Zero; q = expr; return true; }
        if (expr.Equals(v)) { p = One; q = Zero; return true; }

        if (expr is Expression.Sum sum)
        {
            foreach (var t in sum.Terms)
            {
                if (!Structure.ContainsVariable(t, v)) { q = Add(q, t); continue; }
                if (t.Equals(v)) { p = Add(p, One); continue; }
                if (t is Expression.Product tp && tp.Factors.Count(f => f.Equals(v)) == 1)
                {
                    var others = tp.Factors.Where(f => !f.Equals(v)).ToList();
                    var cc = others.Count == 1 ? others[0] : new Expression.Product(others);
                    if (Structure.ContainsVariable(cc, v)) return false;
                    p = Add(p, cc);
                    continue;
                }
                return false;
            }
            return true;
        }

        if (expr is Expression.Product prod && prod.Factors.Count(f => f.Equals(v)) == 1)
        {
            var others = prod.Factors.Where(f => !f.Equals(v)).ToList();
            var cc = others.Count == 1 ? others[0] : new Expression.Product(others);
            if (Structure.ContainsVariable(cc, v)) return false;
            p = cc; q = Zero;
            return true;
        }

        return false;
    }

    // ── Polynomial long division ─────────────────────────────────
    // (EN) Rewrites an improper rational function as quotient + remainder/denominator so each part
    //      becomes integrable (e.g. x²/(1+x²) = 1 - 1/(1+x²)).
    // (ZH) 将假分式改写为「商 + 余式/分母」，使各部分可积（如 x²/(1+x²) = 1 - 1/(1+x²)）。

    /// <summary>
    /// (EN) Try full rational-function integration: polynomial division + factorisation + partial
    ///      fractions (numeric rational coefficients only).
    /// (ZH) 尝试通用有理函数积分：多项式除法 + 因式分解 + 部分分式（仅数值有理系数）。
    /// </summary>
    private static IntegrationRule? TryRationalFunctionRule(Expression integrand, Expression variable)
    {
        if (!RationalIntegrator.TryToRationalFunction(integrand, variable, out var nc, out var dc)) return null;
        if (Polynomial.Degree(dc) <= 0) return null;
        if (!RationalIntegrator.TryIntegrate(nc, dc, variable, out var result)) return null;

        return new RationalFunctionRule
        {
            Integrand = integrand, Variable = variable, Result = result
        };
    }

    // ── Weierstrass substitution ─────────────────────────────────
    // (EN) A rational function of sin(x)/cos(x) is integrated with t = tan(x/2), which turns it into a
    //      rational function in t; the result is re-substituted back to x.
    // (ZH) sin(x)/cos(x) 的有理函数用 t = tan(x/2) 化为 t 的有理函数积分，再回代到 x。

    /// <summary>
    /// (EN) Try the Weierstrass substitution for a rational function of sin(x)/cos(x).
    /// (ZH) 对 sin(x)/cos(x) 的有理函数尝试 Weierstrass 代换。
    /// </summary>
    private IntegrationRule? TryWeierstrassRule(Expression integrand, Expression variable)
    {
        if (!Structure.ContainsVariable(integrand, variable)) return null;
        if (!IsRationalInSinCos(integrand, variable, out bool sawTrig) || !sawTrig) return null;

        var t = Symbol("__t__");
        var onePlusT2 = Add(One, t * t);
        var sinExpr = Divide(Multiply(Two, t), onePlusT2);
        var cosExpr = Divide(Subtract(One, t * t), onePlusT2);

        var f = Structure.Substitute(Sin(variable), sinExpr, integrand);
        f = Structure.Substitute(Cos(variable), cosExpr, f);
        if (Structure.ContainsVariable(f, variable)) return null;

        // (EN) Include dx/dt = 2/(1+t²) and canonicalise into polynomials in t. (ZH) 计入 dx/dt = 2/(1+t²)，并规范化为 t 的多项式。
        var g = Multiply(f, Divide(Two, onePlusT2));
        if (!RationalIntegrator.TryToRationalFunction(g, t, out var gn, out var gd)) return null;
        if (!RationalIntegrator.TryIntegrate(gn, gd, t, out var resultInT)) return null;

        return new WeierstrassRule
        {
            Integrand = integrand, Variable = variable, T = t, ResultInT = resultInT
        };
    }

    /// <summary>
    /// (EN) Whether e is a rational combination of sin(v)/cos(v) with v the integration variable (no
    ///      other variable-dependent functions).
    /// (ZH) e 是否为 sin(v)/cos(v)（v 为积分变量）的有理组合（不含其它依赖变量的函数）。
    /// </summary>
    private static bool IsRationalInSinCos(Expression e, Expression v, out bool sawTrig)
    {
        sawTrig = false;
        switch (e)
        {
            case Expression.Function f:
                if (f.Op == FunctionType.Sin || f.Op == FunctionType.Cos)
                {
                    sawTrig = true;
                    return f.Argument.Equals(v);
                }
                return !Structure.ContainsVariable(e, v);
            case Expression.FunctionN:
                return !Structure.ContainsVariable(e, v);
            case Expression.Sum s:
                foreach (var t in s.Terms)
                    if (!IsRationalInSinCos(t, v, out var st)) return false;
                    else sawTrig |= st;
                return true;
            case Expression.Product p:
                foreach (var factor in p.Factors)
                    if (!IsRationalInSinCos(factor, v, out var st)) return false;
                    else sawTrig |= st;
                return true;
            case Expression.Power pw:
                if (!IsRationalInSinCos(pw.Base, v, out var b)) return false;
                if (!IsRationalInSinCos(pw.Exponent, v, out var ex)) return false;
                sawTrig = b || ex;
                return true;
            default:
                return true;
        }
    }

    // ── Exponential substitution t = e^x ─────────────────────────
    // (EN) A rational function of e^x (with exponentials e^(k·x)) is integrated with t = e^x, which
    //      applies dx = dt/t and turns it into a rational function in t.
    // (ZH) e^x 的有理函数（指数形如 e^(k·x)）用 t = e^x 积分，利用 dx = dt/t 化为 t 的有理函数。

    /// <summary>
    /// (EN) Try the substitution t = e^x for a rational function of exponentials e^(k·x).
    /// (ZH) 对指数 e^(k·x) 的有理函数尝试换元 t = e^x。
    /// </summary>
    private IntegrationRule? TryExpSubstitutionRule(Expression integrand, Expression variable)
    {
        if (!Structure.ContainsVariable(integrand, variable)) return null;
        var t = Symbol("__t__");
        if (!TryRewriteExp(integrand, variable, t, out var f)) return null;
        if (Structure.ContainsVariable(f, variable)) return null;
        // (EN) dx = dt/t. (ZH) dx = dt/t。
        var g = Multiply(f, Divide(One, t));
        return BuildBackSubstitution(integrand, variable, t, g, Exp(variable));
    }

    /// <summary>
    /// (EN) Rewrites every e^(k·x) as t^k (k rational), rejecting any other variable dependence.
    /// (ZH) 将每个 e^(k·x) 改写为 t^k（k 为有理数），并拒绝其它形式的变量依赖。
    /// </summary>
    private static bool TryRewriteExp(Expression e, Expression v, Expression t, out Expression f)
    {
        f = e;
        if (e is Expression.Function fn && fn.Op == FunctionType.Exp)
        {
            if (!TryGetLinearCoeffs(fn.Argument, v, out var a, out var b) || !Expression.IsZero(b)) return false;
            f = Expression.IsOne(a) ? t : Pow(t, a);
            return true;
        }
        if (e is Expression.Function or Expression.FunctionN)
            return !Structure.ContainsVariable(e, v);
        if (!Structure.ContainsVariable(e, v)) return true;

        switch (e)
        {
            case Expression.Sum s:
                {
                    var terms = new List<Expression>(s.Terms.Count);
                    foreach (var term in s.Terms)
                    {
                        if (!TryRewriteExp(term, v, t, out var rf)) return false;
                        terms.Add(rf);
                    }
                    f = new Expression.Sum(terms);
                    return true;
                }
            case Expression.Product p:
                {
                    var factors = new List<Expression>(p.Factors.Count);
                    foreach (var factor in p.Factors)
                    {
                        if (!TryRewriteExp(factor, v, t, out var rf)) return false;
                        factors.Add(rf);
                    }
                    f = new Expression.Product(factors);
                    return true;
                }
            case Expression.Power pw:
                if (!TryRewriteExp(pw.Base, v, t, out var rb)) return false;
                if (!TryRewriteExp(pw.Exponent, v, t, out var re)) return false;
                f = new Expression.Power(rb, re);
                return true;
            default:
                return false;
        }
    }

    // ── Square-root substitution t = √x ──────────────────────────
    // (EN) A rational function of x and √x is integrated with t = √x (x = t², dx = 2t·dt).
    // (ZH) x 与 √x 的有理函数用 t = √x 积分（x = t²，dx = 2t·dt）。

    /// <summary>
    /// (EN) Try the substitution t = √x for a rational function of x and √x.
    /// (ZH) 对 x 与 √x 的有理函数尝试换元 t = √x。
    /// </summary>
    private IntegrationRule? TrySqrtSubstitutionRule(Expression integrand, Expression variable)
    {
        if (!Structure.ContainsVariable(integrand, variable)) return null;
        var t = Symbol("__t__");
        if (!TryRewriteSqrt(integrand, variable, t, out var f)) return null;
        if (Structure.ContainsVariable(f, variable)) return null;
        // (EN) dx = 2t·dt. (ZH) dx = 2t·dt。
        var g = Multiply(f, Multiply(Two, t));
        return BuildBackSubstitution(integrand, variable, t, g, Sqrt(variable));
    }

    /// <summary>
    /// (EN) Rewrites x^n as t^(2n) (x = t²), rejecting other variable-dependent functions.
    /// (ZH) 将 x^n 改写为 t^(2n)（x = t²），并拒绝其它依赖变量的函数。
    /// </summary>
    private static bool TryRewriteSqrt(Expression e, Expression v, Expression t, out Expression f)
    {
        f = e;
        if (e.Equals(v)) { f = t * t; return true; }
        if (e is Expression.Function or Expression.FunctionN)
            return !Structure.ContainsVariable(e, v);
        if (!Structure.ContainsVariable(e, v)) return true;

        switch (e)
        {
            case Expression.Sum s:
                {
                    var terms = new List<Expression>(s.Terms.Count);
                    foreach (var term in s.Terms)
                    {
                        if (!TryRewriteSqrt(term, v, t, out var rf)) return false;
                        terms.Add(rf);
                    }
                    f = new Expression.Sum(terms);
                    return true;
                }
            case Expression.Product p:
                {
                    var factors = new List<Expression>(p.Factors.Count);
                    foreach (var factor in p.Factors)
                    {
                        if (!TryRewriteSqrt(factor, v, t, out var rf)) return false;
                        factors.Add(rf);
                    }
                    f = new Expression.Product(factors);
                    return true;
                }
            case Expression.Power pw:
                if (pw.Base.Equals(v) && pw.Exponent is Expression.Number)
                {
                    // (EN) x^n → t^(2n). (ZH) x^n → t^(2n)。
                    f = Pow(t, Multiply(Two, pw.Exponent));
                    return true;
                }
                if (!TryRewriteSqrt(pw.Base, v, t, out var rb)) return false;
                if (!TryRewriteSqrt(pw.Exponent, v, t, out var re)) return false;
                f = new Expression.Power(rb, re);
                return true;
            default:
                return false;
        }
    }

    // ── Trigonometric substitution x = sin θ for √(1-x²) forms ──
    // (EN) Integrals rational in x and (1-x²)^e are handled with x = sin θ: (1-x²)^e = cos^(2e) θ and
    //      dx = cos θ dθ, giving a trig rational integrand; the result is re-substituted θ = asin(x).
    // (ZH) 关于 x 与 (1-x²)^e 有理的积分用 x = sin θ 处理：(1-x²)^e = cos^(2e) θ，dx = cos θ dθ，
    //      得到三角有理式；最后回代 θ = asin(x)。

    /// <summary>
    /// (EN) Trigonometric kind selected for a √(quadratic) substitution. (ZH) 用于 √(二次式) 换元的三角类型。
    /// </summary>
    private enum SqrtTrigKind { None, OneMinusX2, OnePlusX2, X2MinusOne }

    /// <summary>
    /// (EN) Try a trigonometric substitution for an integrand rational in x and √(1-x²)/√(1+x²)/√(x²-1)
    ///      powers: x = sin θ, x = tan θ or x = sec θ respectively.
    /// (ZH) 对关于 x 与 √(1-x²)/√(1+x²)/√(x²-1) 幂有理的被积式尝试三角换元：分别取 x = sin θ、
    ///      x = tan θ、x = sec θ。
    /// </summary>
    private IntegrationRule? TryTrigSqrtSubstitutionRule(Expression integrand, Expression variable)
    {
        if (!Structure.ContainsVariable(integrand, variable)) return null;
        var kind = DetectSqrtTrigKind(integrand, variable);
        if (kind == SqrtTrigKind.None) return null;

        var theta = Symbol("__theta__");
        if (!TryRewriteTrigSqrt(integrand, variable, theta, kind, out var f, out bool saw)) return null;
        if (!saw || Structure.ContainsVariable(f, variable)) return null;

        var g = Multiply(f, TrigSqrtJacobian(kind, theta));
        var substep = Solve(g, theta);
        if (substep.ContainsDontKnow) return null;

        var back = kind switch
        {
            SqrtTrigKind.OneMinusX2 => Asin(variable),
            SqrtTrigKind.OnePlusX2 => Atan(variable),
            _ => Acos(Divide(One, variable)),
        };
        return new BackSubstitutionRule
        {
            Integrand = integrand, Variable = variable, T = theta,
            ResultInT = substep.Eval(), BackExpr = back
        };
    }

    /// <summary>
    /// (EN) Scans for a power whose base is 1-x², 1+x² or x²-1; returns its kind (None if absent or mixed).
    /// (ZH) 扫描底为 1-x²、1+x² 或 x²-1 的幂，返回其类型（不存在或混合时为 None）。
    /// </summary>
    private static SqrtTrigKind DetectSqrtTrigKind(Expression e, Expression v)
    {
        SqrtTrigKind found = SqrtTrigKind.None;
        bool Mixed(Expression x)
        {
            switch (x)
            {
                case Expression.Power p when p.Exponent is Expression.Number:
                    {
                        var k = KindOfQuadratic(p.Base, v);
                        if (k != SqrtTrigKind.None)
                        {
                            if (found != SqrtTrigKind.None && found != k) return false;
                            found = k;
                        }
                        break;
                    }
                case Expression.Sum s:
                    foreach (var t in s.Terms) if (!Mixed(t)) return false;
                    return true;
                case Expression.Product pr:
                    foreach (var t in pr.Factors) if (!Mixed(t)) return false;
                    return true;
            }
            if (x is Expression.Power pw) { if (!Mixed(pw.Base)) return false; if (!Mixed(pw.Exponent)) return false; }
            return true;
        }
        if (!Mixed(e)) return SqrtTrigKind.None;
        return found;
    }

    /// <summary>
    /// (EN) Kind of a quadratic base: 1-x², 1+x² or x²-1 (None otherwise). (ZH) 二次底的类型。
    /// </summary>
    private static SqrtTrigKind KindOfQuadratic(Expression base_, Expression v)
    {
        if (!TryExtractQuadratic(base_, v, out var ct, out var lt, out var qt)) return SqrtTrigKind.None;
        if (!Expression.IsZero(lt)) return SqrtTrigKind.None;
        if (Expression.IsOne(ct) && Expression.IsMinusOne(qt)) return SqrtTrigKind.OneMinusX2;
        if (Expression.IsOne(ct) && Expression.IsOne(qt)) return SqrtTrigKind.OnePlusX2;
        if (Expression.IsMinusOne(ct) && Expression.IsOne(qt)) return SqrtTrigKind.X2MinusOne;
        return SqrtTrigKind.None;
    }

    /// <summary>
    /// (EN) Rewrites x^n as trigOf(θ)^n and (quadratic)^e as the corresponding cos/sin power.
    /// (ZH) 将 x^n 改写为 trigOf(θ)^n，将 (二次式)^e 改写为对应的 cos/sin 幂。
    /// </summary>
    private static bool TryRewriteTrigSqrt(Expression e, Expression v, Expression theta,
        SqrtTrigKind kind, out Expression f, out bool saw)
    {
        f = e; saw = false;

        if (e is Expression.Power p && KindOfQuadratic(p.Base, v) == kind && p.Exponent is Expression.Number)
        {
            var twoE = Multiply(Two, p.Exponent);
            f = kind switch
            {
                SqrtTrigKind.OneMinusX2 => Pow(Cos(theta), twoE),
                SqrtTrigKind.OnePlusX2 => Pow(Cos(theta), Negate(twoE)),
                _ => Multiply(Pow(Sin(theta), twoE), Pow(Cos(theta), Negate(twoE))),
            };
            saw = true;
            return true;
        }
        if (e.Equals(v)) { f = TrigX(kind, theta); return true; }
        if (e is Expression.Power px && px.Base.Equals(v) && px.Exponent is Expression.Number)
        {
            f = Pow(TrigX(kind, theta), px.Exponent);
            return true;
        }
        if (e is Expression.Function or Expression.FunctionN)
            return !Structure.ContainsVariable(e, v);
        if (!Structure.ContainsVariable(e, v)) return true;

        switch (e)
        {
            case Expression.Sum s:
                {
                    var terms = new List<Expression>(s.Terms.Count);
                    foreach (var term in s.Terms)
                    {
                        if (!TryRewriteTrigSqrt(term, v, theta, kind, out var rf, out var ss)) return false;
                        saw |= ss; terms.Add(rf);
                    }
                    f = new Expression.Sum(terms);
                    return true;
                }
            case Expression.Product pr:
                {
                    var factors = new List<Expression>(pr.Factors.Count);
                    foreach (var factor in pr.Factors)
                    {
                        if (!TryRewriteTrigSqrt(factor, v, theta, kind, out var rf, out var ss)) return false;
                        saw |= ss; factors.Add(rf);
                    }
                    f = new Expression.Product(factors);
                    return true;
                }
            case Expression.Power pw:
                if (!TryRewriteTrigSqrt(pw.Base, v, theta, kind, out var rb, out var sb)) return false;
                if (!TryRewriteTrigSqrt(pw.Exponent, v, theta, kind, out var re, out var se)) return false;
                saw = sb || se; f = new Expression.Power(rb, re);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// (EN) The expression x equals under the substitution. (ZH) 换元下 x 的表达式。
    /// </summary>
    private static Expression TrigX(SqrtTrigKind kind, Expression theta) => kind switch
    {
        SqrtTrigKind.OneMinusX2 => Sin(theta),
        SqrtTrigKind.OnePlusX2 => Multiply(Sin(theta), Pow(Cos(theta), MinusOne)),
        _ => Pow(Cos(theta), MinusOne),
    };

    /// <summary>
    /// (EN) Jacobian dx/dθ for the substitution. (ZH) 换元的雅可比 dx/dθ。
    /// </summary>
    private static Expression TrigSqrtJacobian(SqrtTrigKind kind, Expression theta) => kind switch
    {
        SqrtTrigKind.OneMinusX2 => Cos(theta),
        SqrtTrigKind.OnePlusX2 => Pow(Cos(theta), Expression.Int32(-2)),
        _ => Multiply(Sin(theta), Pow(Cos(theta), Expression.Int32(-2))),
    };

    // ── Euler substitution ───────────────────────────────────────
    // (EN) Rational integrands in x and √(a+b·x+c·x²) are rationalised with Euler's first substitution
    //      (u = √R + √a·x, used when a is a perfect square) or second substitution
    //      (u = √R + √c·x, used when c is a perfect square).
    // (ZH) 关于 x 与 √(a+b·x+c·x²) 的有理被积式用 Euler 第一代换（u = √R + √a·x，a 为完全平方时）
    //      或第二代换（u = √R + √c·x，c 为完全平方时）有理化。

    /// <summary>
    /// (EN) Try Euler's substitution for a rational integrand in x and √(quadratic).
    /// (ZH) 对关于 x 与 √(二次式) 有理的被积式尝试 Euler 代换。
    /// </summary>
    private IntegrationRule? TryEulerSubstitutionRule(Expression integrand, Expression variable)
    {
        if (!Structure.ContainsVariable(integrand, variable)) return null;
        if (!FindEulerSqrt(integrand, variable, out var rBase, out var a, out var b, out var c)) return null;
        if (a is not Expression.Number an || b is not Expression.Number bn || c is not Expression.Number cn) return null;

        var u = Symbol("__u__");
        Expression sqrtCoef, xOfU;
        bool useSecond = cn.Value.IsPositive && TrySqrtRational(cn.Value, out _);
        if (useSecond)
        {
            TrySqrtRational(cn.Value, out var sc);
            sqrtCoef = new Expression.Number(sc);
        }
        else if (an.Value.IsPositive && TrySqrtRational(an.Value, out var sa))
        {
            sqrtCoef = new Expression.Number(sa);
        }
        else
        {
            return null;
        }

        if (useSecond)
        {
            // (EN) x = (u² - a)/(b + 2√c·u). (ZH) x = (u² - a)/(b + 2√c·u)。
            xOfU = Divide(Subtract(u * u, a), Add(b, Multiply(Multiply(Two, sqrtCoef), u)));
        }
        else
        {
            // (EN) x = (u² - c)/(2√a·u + b). (ZH) x = (u² - c)/(2√a·u + b)。
            xOfU = Divide(Subtract(u * u, c), Add(Multiply(Multiply(Two, sqrtCoef), u), b));
        }
        // (EN) √R = u - √coef·x. (ZH) √R = u - √coef·x。
        var sOfU = Subtract(u, Multiply(sqrtCoef, xOfU));

        if (!TryRewriteEuler(integrand, variable, rBase, sOfU, xOfU, out var f, out bool saw) || !saw) return null;
        if (Structure.ContainsVariable(f, variable)) return null;

        var dxdu = Differentiate.Diff(xOfU, u);
        if (dxdu is null) return null;
        var g = Multiply(f, dxdu);
        if (!RationalIntegrator.TryToRationalFunction(g, u, out var gn, out var gd)) return null;
        if (!RationalIntegrator.TryIntegrate(gn, gd, u, out var resultInU)) return null;

        // (EN) u = √R + √coef·x. (ZH) u = √R + √coef·x。
        var backExpr = Add(Pow(rBase, Expression.Half), Multiply(sqrtCoef, variable));
        return new BackSubstitutionRule
        {
            Integrand = integrand, Variable = variable, T = u, ResultInT = resultInU, BackExpr = backExpr
        };
    }

    /// <summary>
    /// (EN) Finds a √(quadratic) power node and returns the quadratic base and its (a,b,c). (ZH) 查找 √(二次式) 幂节点并返回二次底及其 (a,b,c)。
    /// </summary>
    private static bool FindEulerSqrt(Expression e, Expression v,
        out Expression rBase, out Expression a, out Expression b, out Expression c)
    {
        rBase = a = b = c = One;
        if (e is Expression.Power p && p.Exponent is Expression.Number ne && ne.Value.Denominator == 2
            && !ne.Value.IsInteger && Structure.ContainsVariable(p.Base, v)
            && TryExtractQuadratic(p.Base, v, out var ct, out var lt, out var qt))
        {
            rBase = p.Base; a = ct; b = lt; c = qt;
            return true;
        }
        switch (e)
        {
            case Expression.Sum s:
                foreach (var term in s.Terms)
                    if (FindEulerSqrt(term, v, out rBase, out a, out b, out c)) return true;
                break;
            case Expression.Product pr:
                foreach (var factor in pr.Factors)
                    if (FindEulerSqrt(factor, v, out rBase, out a, out b, out c)) return true;
                break;
            case Expression.Power pw:
                if (FindEulerSqrt(pw.Base, v, out rBase, out a, out b, out c)) return true;
                if (FindEulerSqrt(pw.Exponent, v, out rBase, out a, out b, out c)) return true;
                break;
            case Expression.Function fn:
                if (FindEulerSqrt(fn.Argument, v, out rBase, out a, out b, out c)) return true;
                break;
        }
        return false;
    }

    /// <summary>
    /// (EN) Rewrites (rBase)^(m/2) as (u-√coef·x)^m and x as x(u). (ZH) 将 (rBase)^(m/2) 改写为 (u-√coef·x)^m、x 改写为 x(u)。
    /// </summary>
    private static bool TryRewriteEuler(Expression e, Expression v, Expression rBase,
        Expression sOfU, Expression xOfU, out Expression f, out bool saw)
    {
        f = e; saw = false;
        if (e is Expression.Power p && p.Base.Equals(rBase) && p.Exponent is Expression.Number ne
            && ne.Value.Denominator == 2 && !ne.Value.IsInteger)
        {
            f = Pow(sOfU, Multiply(Two, p.Exponent));
            saw = true;
            return true;
        }
        if (e.Equals(v)) { f = xOfU; return true; }
        if (e is Expression.Power px && px.Base.Equals(v) && px.Exponent is Expression.Number)
        {
            f = Pow(xOfU, px.Exponent);
            return true;
        }
        if (e is Expression.Function or Expression.FunctionN)
            return !Structure.ContainsVariable(e, v);
        if (!Structure.ContainsVariable(e, v)) return true;

        switch (e)
        {
            case Expression.Sum s:
                {
                    var terms = new List<Expression>(s.Terms.Count);
                    foreach (var term in s.Terms)
                    {
                        if (!TryRewriteEuler(term, v, rBase, sOfU, xOfU, out var rf, out var ss)) return false;
                        saw |= ss; terms.Add(rf);
                    }
                    f = new Expression.Sum(terms);
                    return true;
                }
            case Expression.Product pr:
                {
                    var factors = new List<Expression>(pr.Factors.Count);
                    foreach (var factor in pr.Factors)
                    {
                        if (!TryRewriteEuler(factor, v, rBase, sOfU, xOfU, out var rf, out var ss)) return false;
                        saw |= ss; factors.Add(rf);
                    }
                    f = new Expression.Product(factors);
                    return true;
                }
            case Expression.Power pw:
                if (!TryRewriteEuler(pw.Base, v, rBase, sOfU, xOfU, out var rb, out var sb)) return false;
                if (!TryRewriteEuler(pw.Exponent, v, rBase, sOfU, xOfU, out var re, out var se)) return false;
                saw = sb || se; f = new Expression.Power(rb, re);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// (EN) Rational square root of a non-negative rational that is a perfect square. (ZH) 非负有理数且为完全平方时的有理平方根。
    /// </summary>
    private static bool TrySqrtRational(Rational r, out Rational result)
    {
        result = Rational.Zero;
        if (r.IsNegative) return false;
        var n = r.Numerator;
        var d = r.Denominator;
        var sn = IntegerSqrt(n);
        var sd = IntegerSqrt(d);
        if (sn < 0 || sd < 0) return false;
        result = new Rational(sn, sd);
        return true;
    }

    /// <summary>
    /// (EN) Integer square root, or -1 when n is not a perfect square. (ZH) 整数平方根；n 非完全平方时返回 -1。
    /// </summary>
    private static System.Numerics.BigInteger IntegerSqrt(System.Numerics.BigInteger n)
    {
        if (n < 0) return -1;
        if (n < 2) return n;
        var x = (System.Numerics.BigInteger)Math.Sqrt((double)n);
        while (x * x > n) x--;
        while ((x + 1) * (x + 1) <= n) x++;
        return x * x == n ? x : -1;
    }

    // ── Chebyshev substitution ───────────────────────────────────
    // (EN) Integrates c·x^m·(a+b·x^n)^p (Chebyshev's binomial differential) when p, (m+1)/n or
    //      (m+1)/n + p is an integer, using u = x^(1/L), u = (a+b·x^n)^(1/q) or
    //      u = (a+b·x^n)^(1/q)·x^(-n/q) respectively.
    // (ZH) 当 p、(m+1)/n 或 (m+1)/n + p 为整数时，积分 c·x^m·(a+b·x^n)^p（切比雪夫二项微分），
    //      分别用 u = x^(1/L)、u = (a+b·x^n)^(1/q) 或 u = (a+b·x^n)^(1/q)·x^(-n/q) 换元。

    /// <summary>
    /// (EN) Try the Chebyshev substitution for a binomial differential. (ZH) 对二项微分尝试切比雪夫换元。
    /// </summary>
    private IntegrationRule? TryChebyshevSubstitutionRule(Expression integrand, Expression variable)
    {
        if (!Structure.ContainsVariable(integrand, variable)) return null;
        if (!TryParseBinomialDifferential(integrand, variable, out var c, out var m, out var a, out var b, out var n, out var p))
            return null;
        // (EN) Integer p is a polynomial/rational handled elsewhere. (ZH) 整数 p 为多项式/有理式，别处已处理。
        if (p.IsInteger) return null;

        var u = Symbol("__u__");
        int q = (int)p.Denominator;
        int P = (int)p.Numerator;
        var r = (m + (Rational)1) / n;

        Expression? transformed = null;
        Expression? uFunc = null;

        if (r.IsInteger)
        {
            // (EN) Case 2: u = (a+b·x^n)^(1/q); transformed = c·q/(b·n)·u^(P+q-1)·((u^q-a)/b)^(r-1).
            // (ZH) 情形 2。
            int ri = (int)r.Numerator;
            transformed = Multiply(
                new Expression.Number(c * (Rational)q / (b * (Rational)n)),
                Multiply(
                    Pow(u, P + q - 1),
                    Pow(Divide(Subtract(Pow(u, q), Num(a)), Num(b)), ri - 1)));
            uFunc = Pow(ReferenceBase(a, b, n, variable), Divide(One, Num(q)));
        }
        else
        {
            var sInt = r + p;
            if (sInt.IsInteger)
            {
                // (EN) Case 3: u = (a+b·x^n)^(1/q)·x^(-n/q); transformed = -c·q/(a·n)·u^(P+q-1)·(a/(u^q-b))^(s+1).
                // (ZH) 情形 3。
                int si = (int)sInt.Numerator;
                transformed = Multiply(
                    new Expression.Number(-(c * (Rational)q / (a * (Rational)n))),
                    Multiply(
                        Pow(u, P + q - 1),
                        Pow(Divide(Num(a), Subtract(Pow(u, q), Num(b))), si + 1)));
                var baseExpr = ReferenceBase(a, b, n, variable);
                uFunc = Multiply(Pow(baseExpr, Divide(One, Num(q))),
                    Pow(variable, Negate(Divide(Num(n), Num(q)))));
            }
        }

        if (transformed is null || uFunc is null) return null;
        var substep = Solve(transformed, u);
        if (substep.ContainsDontKnow) return null;

        return new BackSubstitutionRule
        {
            Integrand = integrand, Variable = variable, T = u,
            ResultInT = substep.Eval(), BackExpr = uFunc
        };
    }

    /// <summary>
    /// (EN) Match a nested affine power ((a+b·x)^d)^e and rewrite it to (a+b·x)^(d·e).
    /// (ZH) 匹配嵌套仿射幂 ((a+b·x)^d)^e 并改写为 (a+b·x)^(d·e)。
    /// </summary>
    private IntegrationRule? TryNestedPowRule(Expression integrand, Expression variable)
    {
        if (integrand is not Expression.Power outer) return null;
        if (outer.Base is not Expression.Power inner) return null;
        if (outer.Exponent is not Expression.Number || inner.Exponent is not Expression.Number) return null;
        // (EN) Only an affine inner base is safe (matches SymPy's nested_pow_rule). (ZH) 仅内层为仿射底才安全（与 SymPy nested_pow_rule 一致）。
        if (TryExtractLinearCoeff(inner.Base, variable) is null) return null;

        var newExp = Multiply(inner.Exponent, outer.Exponent);
        if (newExp is not Expression.Number) return null;
        var rewritten = Pow(inner.Base, newExp);
        if (rewritten.Equals(integrand)) return null;

        var substep = Solve(rewritten, variable);
        if (substep.ContainsDontKnow) return null;
        return new RewriteRule
        {
            Integrand = integrand, Variable = variable,
            Rewritten = rewritten, Substeps = substep
        };
    }

    /// <summary>
    /// (EN) Parses integrand = c·x^m·(a+b·x^n)^p with numeric a,b,c and rational m,n,p. (ZH) 解析 integrand = c·x^m·(a+b·x^n)^p。
    /// </summary>
    private static bool TryParseBinomialDifferential(Expression integrand, Expression v,
        out Rational c, out Rational m, out Rational a, out Rational b, out Rational n, out Rational p)
    {
        c = Rational.One; m = Rational.Zero; a = Rational.Zero; b = Rational.Zero; n = Rational.Zero; p = Rational.Zero;
        bool seenBase = false;

        foreach (var f in Algebraic.Factors(integrand))
        {
            if (!Structure.ContainsVariable(f, v))
            {
                if (f is not Expression.Number cn) return false;
                c *= cn.Value;
                continue;
            }
            if (f.Equals(v)) { m += Rational.One; continue; }
            if (f is Expression.Power pv && pv.Base.Equals(v) && pv.Exponent is Expression.Number ev)
            {
                m += ev.Value; continue;
            }
            // (EN) (a + b·x^n)^p factor. (ZH) (a + b·x^n)^p 因子。
            if (f is Expression.Power bp && bp.Base is Expression.Sum bs && bp.Exponent is Expression.Number pe)
            {
                if (!TryExtractMonomial(bs, v, out var aa, out var bb, out var nn)) return false;
                if (!seenBase) { a = aa; b = bb; n = nn; p = pe.Value; seenBase = true; }
                else
                {
                    if (n != nn) return false;
                    p += pe.Value;
                }
                continue;
            }
            return false;
        }
        return seenBase && !b.IsZero && !n.IsZero;
    }

    /// <summary>
    /// (EN) Extracts (a, b, n) from a + b·x^n (constant term plus a single monomial). (ZH) 从 a + b·x^n 提取 (a, b, n)。
    /// </summary>
    private static bool TryExtractMonomial(Expression sum, Expression v,
        out Rational a, out Rational b, out Rational n)
    {
        a = Rational.Zero; b = Rational.Zero; n = Rational.Zero;
        bool seenVar = false;
        foreach (var t in Algebraic.Summands(sum))
        {
            if (!Structure.ContainsVariable(t, v))
            {
                if (t is not Expression.Number cn) return false;
                a += cn.Value;
                continue;
            }
            if (seenVar) return false;
            seenVar = true;
            // (EN) t = b·x^n with numeric b and rational n. (ZH) t = b·x^n。
            Rational coeff = Rational.One; Rational exp = Rational.Zero;
            foreach (var f in Algebraic.Factors(t))
            {
                if (!Structure.ContainsVariable(f, v))
                {
                    if (f is not Expression.Number fn) return false;
                    coeff *= fn.Value;
                }
                else if (f.Equals(v)) exp += Rational.One;
                else if (f is Expression.Power pv && pv.Base.Equals(v) && pv.Exponent is Expression.Number ev)
                    exp += ev.Value;
                else return false;
            }
            if (exp.IsZero) return false;
            b = coeff; n = exp;
        }
        return seenVar;
    }

    /// <summary>
    /// (EN) Builds the reference base a + b·x^n as an expression. (ZH) 构造参考底 a + b·x^n。
    /// </summary>
    private static Expression ReferenceBase(Rational a, Rational b, Rational n, Expression v)
        => Add(Num(a), Multiply(Num(b), Pow(v, Num(n))));

    /// <summary>
    /// (EN) Wraps a rational as a number expression. (ZH) 将有理数包装为数值表达式。
    /// </summary>
    private static Expression Num(Rational r) => new Expression.Number(r);

    // (EN) √((a·x+b)/(c·x+d)) is rationalised with t = √((a·x+b)/(c·x+d)), which makes x rational in t.
    // (ZH) 用 t = √((a·x+b)/(c·x+d)) 有理化 √((a·x+b)/(c·x+d))，使 x 成为 t 的有理式。

    /// <summary>
    /// (EN) Try the rationalising substitution for √((a·x+b)/(c·x+d)) forms.
    /// (ZH) 对 √((a·x+b)/(c·x+d)) 形式尝试有理化换元。
    /// </summary>
    private IntegrationRule? TrySqrtFractionalLinearRule(Expression integrand, Expression variable)
    {
        if (!Structure.ContainsVariable(integrand, variable)) return null;
        if (!FindFractionalSqrt(integrand, variable, out var a, out var b, out var c, out var d)) return null;

        var t = Symbol("__t__");
        var t2 = t * t;
        // (EN) x = (b - d·t²)/(c·t² - a). (ZH) x = (b - d·t²)/(c·t² - a)。
        var xOfT = Divide(Subtract(b, Multiply(d, t2)), Subtract(Multiply(c, t2), a));
        if (!TryRewriteSqrtFractional(integrand, variable, t, xOfT, a, b, c, d, out var f, out bool saw)
            || !saw || Structure.ContainsVariable(f, variable)) return null;

        var dxdt = Differentiate.Diff(xOfT, t);
        if (dxdt is null) return null;
        var g = Multiply(f, dxdt);
        if (!RationalIntegrator.TryToRationalFunction(g, t, out var gn, out var gd)) return null;
        if (!RationalIntegrator.TryIntegrate(gn, gd, t, out var resultInT)) return null;

        var back = Pow(Divide(Add(Multiply(a, variable), b), Add(Multiply(c, variable), d)), Expression.Half);
        return new BackSubstitutionRule
        {
            Integrand = integrand, Variable = variable, T = t, ResultInT = resultInT, BackExpr = back
        };
    }

    /// <summary>
    /// (EN) Finds a √((a x+b)/(c x+d)) node and returns its four linears' coefficients. (ZH) 查找 √((a x+b)/(c x+d)) 节点并返回四个线性系数。
    /// </summary>
    private static bool FindFractionalSqrt(Expression e, Expression v,
        out Expression a, out Expression b, out Expression c, out Expression d)
    {
        if (TryMatchFractionalSqrtNode(e, v, out a, out b, out c, out d, out _)) return true;
        a = b = c = d = One;

        // (EN) Recurse to find the node. (ZH) 递归查找节点。
        switch (e)
        {
            case Expression.Sum s:
                foreach (var term in s.Terms)
                    if (FindFractionalSqrt(term, v, out a, out b, out c, out d)) return true;
                break;
            case Expression.Product pr:
                foreach (var factor in pr.Factors)
                    if (FindFractionalSqrt(factor, v, out a, out b, out c, out d)) return true;
                break;
            case Expression.Power pw:
                if (FindFractionalSqrt(pw.Base, v, out a, out b, out c, out d)) return true;
                if (FindFractionalSqrt(pw.Exponent, v, out a, out b, out c, out d)) return true;
                break;
            case Expression.Function fn:
                if (FindFractionalSqrt(fn.Argument, v, out a, out b, out c, out d)) return true;
                break;
        }
        return false;
    }

    /// <summary>
    /// (EN) Matches a ((a x+b)/(c x+d))^(m/2) node and returns the four linear coefficients and the
    ///      exponent m/2.
    /// (ZH) 匹配 ((a x+b)/(c x+d))^(m/2) 节点并返回四个线性系数与指数 m/2。
    /// </summary>
    private static bool TryMatchFractionalSqrtNode(Expression e, Expression v,
        out Expression a, out Expression b, out Expression c, out Expression d, out Expression exp)
    {
        a = b = c = d = One; exp = Expression.Half;
        if (e is not Expression.Power sp || sp.Exponent is not Expression.Number h
            || h.Value.Denominator != 2) return false;
        if (sp.Base is not Expression.Product prod || prod.Factors.Count != 2) return false;

        int ri = -1;
        for (int i = 0; i < prod.Factors.Count; i++)
            if (prod.Factors[i] is Expression.Power dp && Expression.IsMinusOne(dp.Exponent)
                && dp.Base is Expression.Sum) { ri = i; break; }
        if (ri < 0) return false;

        var denExpr = ((Expression.Power)prod.Factors[ri]).Base;
        var numExpr = prod.Factors[1 - ri];
        if (!TryGetLinearCoeffs(numExpr, v, out a, out b)) return false;
        if (!TryGetLinearCoeffs(denExpr, v, out c, out d)) return false;
        exp = sp.Exponent;
        return !Expression.IsZero(c);
    }

    /// <summary>
    /// (EN) Rewrites the √((a x+b)/(c x+d)) node as t and x as x(t). (ZH) 将 √((a x+b)/(c x+d)) 节点改写为 t、x 改写为 x(t)。
    /// </summary>
    private static bool TryRewriteSqrtFractional(Expression e, Expression v, Expression t, Expression xOfT,
        Expression a, Expression b, Expression c, Expression d, out Expression f, out bool saw)
    {
        f = e; saw = false;
        if (TryMatchFractionalSqrtNode(e, v, out var na, out var nb, out var nc, out var nd, out var ne)
            && na.Equals(a) && nb.Equals(b) && nc.Equals(c) && nd.Equals(d))
        {
            // (EN) ((a x+b)/(c x+d))^(m/2) → t^m with t = √((a x+b)/(c x+d)). (ZH) 同左。
            f = Pow(t, Multiply(Two, ne));
            saw = true;
            return true;
        }
        if (e.Equals(v)) { f = xOfT; return true; }
        if (e is Expression.Power px && px.Base.Equals(v) && px.Exponent is Expression.Number)
        {
            f = Pow(xOfT, px.Exponent);
            return true;
        }
        if (e is Expression.Function or Expression.FunctionN)
            return !Structure.ContainsVariable(e, v);
        if (!Structure.ContainsVariable(e, v)) return true;

        switch (e)
        {
            case Expression.Sum s:
                {
                    var terms = new List<Expression>(s.Terms.Count);
                    foreach (var term in s.Terms)
                    {
                        if (!TryRewriteSqrtFractional(term, v, t, xOfT, a, b, c, d, out var rf, out var ss)) return false;
                        saw |= ss; terms.Add(rf);
                    }
                    f = new Expression.Sum(terms);
                    return true;
                }
            case Expression.Product pr:
                {
                    var factors = new List<Expression>(pr.Factors.Count);
                    foreach (var factor in pr.Factors)
                    {
                        if (!TryRewriteSqrtFractional(factor, v, t, xOfT, a, b, c, d, out var rf, out var ss)) return false;
                        saw |= ss; factors.Add(rf);
                    }
                    f = new Expression.Product(factors);
                    return true;
                }
            case Expression.Power pw:
                if (!TryRewriteSqrtFractional(pw.Base, v, t, xOfT, a, b, c, d, out var rb, out var sb)) return false;
                if (!TryRewriteSqrtFractional(pw.Exponent, v, t, xOfT, a, b, c, d, out var re, out var se)) return false;
                saw = sb || se; f = new Expression.Power(rb, re);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// (EN) Whether e is exactly the √((a x+b)/(c x+d)) node. (ZH) e 是否恰为 √((a x+b)/(c x+d)) 节点。
    /// </summary>

    /// <summary>
    /// (EN) Canonicalises g in t and integrates it; returns a BackSubstitutionRule on success.
    /// (ZH) 将 g 规范化为 t 的有理式并积分；成功时返回 BackSubstitutionRule。
    /// </summary>
    private static IntegrationRule? BuildBackSubstitution(Expression integrand, Expression variable,
        Expression t, Expression g, Expression backExpr)
    {
        Expression resultInT;
        if (RationalIntegrator.TryToRationalFunction(g, t, out var gn, out var gd)
            && RationalIntegrator.TryIntegrate(gn, gd, t, out var rationalResult))
        {
            resultInT = rationalResult;
        }
        else if (TryMatchSqrtQuadraticPoly(g, t) is { } sqrtPoly)
        {
            // (EN) Fallback for P(t)·√(quadratic) forms (e.g. from √(x+√x)). (ZH) P(t)·√(二次式) 形式的回退（如来自 √(x+√x)）。
            resultInT = sqrtPoly.Eval();
        }
        else
        {
            return null;
        }
        return new BackSubstitutionRule
        {
            Integrand = integrand, Variable = variable, T = t, ResultInT = resultInT, BackExpr = backExpr
        };
    }

    /// <summary>
    /// (EN) Try polynomial long division on an improper rational integrand.
    /// (ZH) 对假分式被积式尝试多项式长除法。
    /// </summary>
    private IntegrationRule? TryPolynomialDivisionRule(Expression integrand, Expression variable)
    {
        if (!RationalIntegrator.TryToRationalFunction(integrand, variable, out var nc, out var dc)) return null;

        int dn = Polynomial.Degree(nc), dd = Polynomial.Degree(dc);
        // (EN) Nothing to divide for a proper fraction or a constant denominator. (ZH) 真分式或常数分母无需除法。
        if (dd <= 0 || dn < dd) return null;

        var (qc, rc) = Polynomial.Divide(nc, dc);
        var rewritten = Add(
            Polynomial.ToExpression(qc, variable),
            Divide(Polynomial.ToExpression(rc, variable), Polynomial.ToExpression(dc, variable)));

        var substep = Solve(rewritten, variable);
        if (substep.ContainsDontKnow) return null;
        return new RewriteRule
        {
            Integrand = integrand, Variable = variable,
            Rewritten = rewritten, Substeps = substep
        };
    }

    /// <summary>
    /// (EN) Converts an expression to a numeric-coefficient polynomial in v (index = degree).
    ///      Returns false for non-polynomial or non-numeric coefficients.
    /// (ZH) 将表达式转换为 v 的数值系数多项式（下标为次数）。非多项式或非数值系数时返回 false。
    /// </summary>
    private static bool TryToPoly(Expression expr, Expression v, out List<Rational> coeffs)
    {
        coeffs = new List<Rational>();
        var expanded = ExpandFully(expr, MaxIntegrandNodes) ?? expr;
        foreach (var term in FlattenSum(expanded))
        {
            Rational c = Rational.One;
            int deg = 0;
            foreach (var f in AllFactors(term))
            {
                if (!Structure.ContainsVariable(f, v))
                {
                    if (f is Expression.Number n) c *= n.Value;
                    else return false;
                }
                else if (f.Equals(v)) deg += 1;
                else if (f is Expression.Power p && p.Base.Equals(v)
                    && p.Exponent is Expression.Number ne && ne.Value.IsInteger && ne.Value.ToInt32() >= 0)
                    deg += ne.Value.ToInt32();
                else return false;
            }
            while (coeffs.Count <= deg) coeffs.Add(Rational.Zero);
            coeffs[deg] += c;
        }
        return coeffs.Count > 0;
    }

    /// <summary>
    /// (EN) Try expanding the integrand and integrating the expanded polynomial.
    /// (ZH) 尝试展开被积式并对展开后的多项式积分。
    /// </summary>
    private IntegrationRule? TryExpandRule(Expression integrand, Expression variable)
    {
        if (!IsExpandable(integrand)) return null;

        var expanded = ExpandFully(integrand, maxNodes: MaxIntegrandNodes);
        if (expanded is null || expanded.Equals(integrand)) return null;

        // (EN) Combine like terms so the result is a clean polynomial. (ZH) 合并同类项，使结果为整洁的多项式。
        expanded = CollectLikeTerms(expanded, variable);

        var substep = Solve(expanded, variable);
        if (substep.ContainsDontKnow) return null;

        return new RewriteRule
        {
            Integrand = integrand, Variable = variable,
            Rewritten = expanded, Substeps = substep
        };
    }

    /// <summary>
    /// (EN) Whether the expression is worth expanding: a square-or-higher power of a sum, or a
    ///      product that contains a sum.
    /// (ZH) 表达式是否值得展开：和式的二次或更高次幂，或含和式的乘积。
    /// </summary>
    private static bool IsExpandable(Expression e) => e switch
    {
        Expression.Power p when p.Base is Expression.Sum && p.Exponent is Expression.Number { Value: var r }
            && r.IsInteger && r.ToInt32() >= 2 => true,
        Expression.Product prod => prod.Factors.Any(f => f is Expression.Sum),
        _ => false,
    };

    /// <summary>
    /// (EN) Collects like terms of a polynomial: each term is split into a variable-free coefficient
    ///      and a monomial (product of the variable-dependent factors); equal monomials are merged.
    /// (ZH) 合并多项式的同类项：将每一项拆为与变量无关的系数和单项式（含变量因子的乘积）；相同单项式合并。
    /// </summary>
    private static Expression CollectLikeTerms(Expression sum, Expression variable)
    {
        var groups = new Dictionary<Expression, Expression>();
        var order = new List<Expression>();
        foreach (var term in FlattenSum(sum))
        {
            Expression coeff = One;
            var monoFactors = new List<Expression>();
            foreach (var f in AllFactors(term))
            {
                if (Structure.ContainsVariable(f, variable)) monoFactors.Add(f);
                else coeff = Multiply(coeff, f);
            }

            Expression mono = One;
            foreach (var mf in monoFactors) mono = Multiply(mono, mf);

            if (groups.TryGetValue(mono, out var existing))
                groups[mono] = Add(existing, coeff);
            else
            {
                groups[mono] = coeff;
                order.Add(mono);
            }
        }

        Expression? result = null;
        foreach (var mono in order)
        {
            var coeff = groups[mono];
            if (Expression.IsZero(coeff)) continue;
            var termExpr = Expression.IsOne(mono) ? coeff : Multiply(coeff, mono);
            result = result is null ? termExpr : Add(result, termExpr);
        }
        return result ?? Zero;
    }

    // ── Polynomial expansion rewrite ─────────────────────────────
    // (EN) Expand a non-negative integer power of a sum, or a product containing sums, into a
    //     polynomial that the additive rule can integrate term by term.
    // (ZH) 将和式的非负整数次幂、或含和式的乘积展开为多项式，再由加法规则逐项积分。

    /// <summary>
    /// (EN) Try expanding the integrand and integrating the expanded polynomial.
    /// (ZH) 尝试展开被积式并对展开后的多项式积分。
    /// </summary>
    private static IEnumerable<Expression> FlattenSum(Expression e)
    {
        if (e is Expression.Sum s)
        {
            foreach (var t in s.Terms)
                foreach (var u in FlattenSum(t))
                    yield return u;
        }
        else
        {
            yield return e;
        }
    }

    /// <summary>
    /// (EN) Enumerates the multiplicative factors of an expression, recursively flattening nested
    ///      products. (ZH) 枚举表达式的乘法因式，并递归展平嵌套乘积。
    /// </summary>
    private static IEnumerable<Expression> AllFactors(Expression e)
    {
        if (e is Expression.Product p)
        {
            foreach (var f in p.Factors)
                foreach (var u in AllFactors(f))
                    yield return u;
        }
        else
        {
            yield return e;
        }
    }

    /// <summary>
    /// (EN) Repeatedly expands products of sums until a fixed point, bailing out if the expression
    ///      would exceed <paramref name="maxNodes"/> nodes.
    /// (ZH) 反复展开和式的乘积直至不动点；若表达式将超过 <paramref name="maxNodes"/> 个节点则放弃。
    /// </summary>
    private static Expression? ExpandFully(Expression e, int maxNodes)
    {
        var cur = e;
        for (int iter = 0; iter < 64; iter++)
        {
            var next = ExpandOnce(cur);
            if (next is null) return null;
            if (next.Equals(cur)) return cur;
            if (Structure.CountOperators(next) > maxNodes) return null;
            cur = next;
        }
        return null;
    }

    /// <summary>
    /// (EN) One expansion pass: distributes the first sum found in a product and turns a square (or
    ///      higher power) of a sum into a product of copies so the next pass can distribute it.
    /// (ZH) 一次展开：对乘积中遇到的第一个和式做分配律，并把和式的二次（或更高）幂改写为若干副本的
    ///      乘积，供下一次展开。
    /// </summary>
    private static Expression? ExpandOnce(Expression e)
    {
        switch (e)
        {
            case Expression.Sum sum:
                var newTerms = new List<Expression>(sum.Terms.Count);
                foreach (var t in sum.Terms)
                {
                    var et = ExpandOnce(t);
                    if (et is null) return null;
                    // (EN) Flatten nested sums so expansion reaches a fixed point. (ZH) 展平嵌套和式，使展开达到不动点。
                    if (et is Expression.Sum nested) newTerms.AddRange(nested.Terms);
                    else newTerms.Add(et);
                }
                return new Expression.Sum(newTerms);

            case Expression.Product prod:
                {
                    // (EN) Expand factors first and flatten nested products so terms are products of
                    //      atomic factors (needed for like-term collection).
                    // (ZH) 先展开各因式并展平嵌套乘积，使各项为原子因式的乘积（合并同类项所需）。
                    var expandedFactors = new List<Expression>(prod.Factors.Count);
                    foreach (var f in prod.Factors)
                    {
                        var ef = ExpandOnce(f);
                        if (ef is null) return null;
                        if (ef is Expression.Product ep) expandedFactors.AddRange(ep.Factors);
                        else expandedFactors.Add(ef);
                    }

                    int idx = -1;
                    for (int i = 0; i < expandedFactors.Count; i++)
                        if (expandedFactors[i] is Expression.Sum) { idx = i; break; }

                    if (idx < 0)
                        return new Expression.Product(expandedFactors);

                    var theSum = (Expression.Sum)expandedFactors[idx];
                    var others = expandedFactors.Where((_, i) => i != idx).ToList();
                    var terms = new List<Expression>(theSum.Terms.Count);
                    foreach (var t in theSum.Terms)
                        terms.Add(new Expression.Product(others.Append(t).ToList()));
                    return new Expression.Sum(terms);
                }

            case Expression.Power p when p.Base is Expression.Sum
                && p.Exponent is Expression.Number { Value: var r } && r.IsInteger && r.ToInt32() >= 2:
                {
                    // (EN) Rewrite (sum)^n as n copies (pairwise squaring to limit growth). (ZH) 将 (sum)^n 改写为 n 个副本（两两平方以抑制膨胀）。
                    int n = r.ToInt32();
                    var copies = new List<Expression>();
                    int remaining = n;
                    while (remaining > 0)
                    {
                        int take = Math.Min(remaining, 2);
                        var pair = new Expression.Product(Enumerable.Repeat(p.Base, take).ToList());
                        copies.Add(pair);
                        remaining -= take;
                    }
                    return copies.Count == 1 ? copies[0] : new Expression.Product(copies);
                }

            case Expression.Function fn:
                {
                    var arg = ExpandOnce(fn.Argument);
                    if (arg is null) return null;
                    return new Expression.Function(fn.Op, arg);
                }

            case Expression.FunctionN fnN:
                {
                    var args = new List<Expression>(fnN.Arguments.Count);
                    foreach (var a in fnN.Arguments)
                    {
                        var ea = ExpandOnce(a);
                        if (ea is null) return null;
                        args.Add(ea);
                    }
                    return new Expression.FunctionN(fnN.Op, args);
                }

            default:
                return e;
        }
    }

    /// <summary>
    /// (EN) Try u-substitution: find a candidate sub-expression whose derivative appears
    ///      as a factor in the integrand.
    /// (ZH) 尝试换元积分：寻找候选子表达式，其导数以因子形式出现在被积表达式中。
    /// </summary>
    private IntegrationRule? TrySubstitutionRule(Expression integrand, Expression variable)
    {
        var candidates = CollectPotentialSubstitutions(integrand, variable);
        foreach (var uExpr in candidates)
        {
            var du = Differentiate.Diff(uExpr, variable);
            if (du is not null && !Expression.IsOne(du))
            {
                var result = FactorOutDerivative(integrand, du, variable);
                if (result is not null)
                {
                    var (remaining, coeff) = result.Value;
                    var uVar = Symbol("__u__");
                    // (EN) Substitute uExpr (e.g. x^2) with uVar (e.g. __u__). (ZH) 将 uExpr（如 x^2）替换为 uVar（如 __u__）。
                    var fOfUNew = Structure.Substitute(uExpr, uVar, remaining);
                    // (EN) The remainder must become a function of u alone; otherwise the change of
                    //      variable is invalid and we must not use it.
                    // (ZH) 余项必须化为仅含 u 的函数；否则换元不成立，不能采用。
                    if (Structure.ContainsVariable(fOfUNew, variable)) continue;
                    var substep = Solve(fOfUNew, uVar);
                    if (!substep.ContainsDontKnow)
                    {
                        var rule = new URule
                        {
                            Integrand = integrand, Variable = variable,
                            UVar = uVar, UFunc = uExpr, Substeps = substep
                        };
                        // (EN) If coeff is not 1, wrap in constant rule. (ZH) 若 coeff 不为 1，则用常数规则包装。
                        if (!Expression.IsOne(coeff))
                            return new ConstantTimesRule
                            {
                                Integrand = integrand, Variable = variable,
                                Constant = coeff, Other = integrand, Substeps = rule
                            };
                        return rule;
                    }
                }
            }
        }
        return TryLinearSubstitution(integrand, variable);
    }

    /// <summary>
    /// (EN) Try linear substitution: replace a sum containing the variable with a single symbol.
    /// (ZH) 尝试线性换元：将包含变量的和式替换为单个符号。
    /// </summary>
    private IntegrationRule? TryLinearSubstitution(Expression integrand, Expression variable)
    {
        var candidates = Structure.CollectAll(integrand,
            e => e is Expression.Sum s && s.Terms.Any(t => t.Equals(variable)));
        var uVar = Symbol("__u__");
        foreach (var cand in candidates)
        {
            // (EN) Only an affine candidate gives a valid substitution: its derivative must be a
            //      variable-free constant a, and dx = du/a must be accounted for by a 1/a factor.
            //      Non-affine candidates (e.g. 1+x²+x) would silently produce wrong antiderivatives.
            // (ZH) 只有仿射候选才是合法换元：其导数必须是与变量无关的常数 a，且需用 1/a 因子抵消
            //      dx = du/a。非仿射候选（如 1+x²+x）会静默给出错误原函数。
            if (!TryGetLinearCoeffs(cand, variable, out var coeffA, out _)) continue;
            if (coeffA is null || Expression.IsZero(coeffA)) continue;

            var replaced = Structure.Substitute(cand, uVar, integrand);
            // (EN) The substitution must remove every occurrence of the variable. (ZH) 换元后必须消除所有变量出现。
            if (Structure.ContainsVariable(replaced, variable)) continue;

            var substep = Solve(replaced, uVar);
            if (substep.ContainsDontKnow) continue;

            var rule = new URule
            {
                Integrand = integrand, Variable = variable,
                UVar = uVar, UFunc = cand, Substeps = substep
            };
            // (EN) Include the 1/a factor from dx = du/a. (ZH) 计入 dx = du/a 带来的 1/a 因子。
            if (!Expression.IsOne(coeffA))
                return new ConstantTimesRule
                {
                    Integrand = integrand, Variable = variable,
                    Constant = Divide(One, coeffA), Other = integrand, Substeps = rule
                };
            return rule;
        }
        return null;
    }

    // ── Integration by parts (LIATE) strategy ────────────────────
    // (EN) ∫ u dv = u·v − ∫ v du, choosing u via LIATE priority.
    // (ZH) ∫ u dv = u·v − ∫ v du，按 LIATE 优先级选择 u。

    /// <summary>
    /// (EN) Try integration by parts using the LIATE priority heuristic to choose u and dv.
    /// (ZH) 尝试分部积分，使用 LIATE 优先级启发式方法选择 u 和 dv。
    /// </summary>
    private IntegrationRule? TryPartsRule(Expression integrand, Expression variable)
    {
        // (EN) Parts is normally applied to a product, but a single non-polynomial function
        //      (e.g. ln(x), asin(x), erf(x)) is also a valid case with dv = dx.
        // (ZH) 分部积分通常用于乘积，但单个非多项式函数（如 ln(x)、asin(x)、erf(x)）也是
        //      合法情形，此时取 dv = dx。
        List<Expression> factors;
        bool singleFunction;
        if (integrand is Expression.Product prod && prod.Factors.Count >= 2)
        {
            factors = prod.Factors.ToList();
            singleFunction = false;
        }
        else if (IsSinglePartsCandidate(integrand))
        {
            factors = new List<Expression> { integrand };
            singleFunction = true;
        }
        else
        {
            return null;
        }

        int bestPri = -1;
        Expression? u = null;
        int uIdx = -1;

        for (int i = 0; i < factors.Count; i++)
        {
            int pri = LiatePriority(factors[i], variable);
            if (pri > bestPri) { bestPri = pri; u = factors[i]; uIdx = i; }
        }

        if (u is null || uIdx < 0) return null;

        Expression dv;
        IntegrationRule vStep;
        if (singleFunction)
        {
            // (EN) dv = dx, so v = x via the constant rule. (ZH) dv = dx，故 v = x（常数规则）。
            dv = One;
            vStep = new ConstantRule { Integrand = One, Variable = variable, Constant = One };
        }
        else
        {
            var dvFactors = new List<Expression>(factors);
            dvFactors.RemoveAt(uIdx);
            dv = dvFactors.Count == 1 ? dvFactors[0] : new Expression.Product(dvFactors);
            vStep = Solve(dv, variable);
            if (vStep.ContainsDontKnow) return null;
        }

        var uPrime = Differentiate.Diff(u, variable);
        if (uPrime is null) return null;

        var v = vStep.Eval();
        // (EN) Bail out before multiplying by an already huge antiderivative. (ZH) 若原函数已过于庞大，则在相乘前放弃。
        if (Structure.CountOperators(v) > MaxIntegrandNodes) return null;
        var secondIntegrand = Multiply(v, uPrime);
        if (Structure.CountOperators(secondIntegrand) > MaxIntegrandNodes) return null;
        var secondStep = Solve(secondIntegrand, variable);
        if (secondStep.ContainsDontKnow) return null;

        return new PartsRule
        {
            Integrand = integrand, Variable = variable,
            U = u, Dv = dv,
            VStep = vStep, SecondStep = secondStep
        };
    }

    /// <summary>
    /// (EN) Whether a non-product integrand may be handled by parts with dv = dx: it must be a
    ///      single non-polynomial function (or a two-argument logarithm) that has LIATE priority.
    /// (ZH) 非乘积被积式能否以 dv = dx 用分部积分处理：它须是单个非多项式函数（或双参数对数）
    ///      且具有 LIATE 优先级。
    /// </summary>
    private static bool IsSinglePartsCandidate(Expression e) =>
        e is Expression.Function
        || (e is Expression.FunctionN fn && fn.Op == FunctionNType.Log)
        // (EN) A positive power of a log/inverse-trig function (e.g. ln²x) is also handled by parts.
        // (ZH) 对数/反三角函数的正整数次幂（如 ln²x）也交由分部积分处理。
        || (e is Expression.Power p && p.Exponent is Expression.Number
            && p.Base is Expression.Function bf && LiateFunctionPriority(bf.Op) >= 4);

    // ── Rational function integration ─────────────────────────────
    // (EN) Match reciprocal power forms and apply partial fractions or simple log/power rules.
    // (ZH) 匹配倒数幂形式，应用部分分式分解或简单对数/幂规则。

    /// <summary>
    /// (EN) Try rational function integration by matching reciprocal power forms and
    ///      applying partial fractions or simple log/power rules.
    /// (ZH) 尝试有理函数积分：匹配倒数幂形式，应用部分分式分解或简单对数/幂规则。
    /// </summary>
    private IntegrationRule? TryRationalRule(Expression integrand, Expression variable)
    {
        if (!IsReciprocalPowerForm(integrand, variable, out var baseExpr, out var expExpr))
            return null;

        // (EN) ── Case 1: Denominator is a Product of factors → partial fractions. (ZH) ── 情形 1：分母为多个因子的乘积 → 部分分式。
        if (Expression.IsMinusOne(expExpr) && TryPartialFractions(baseExpr, variable, out var partialRule))
            return partialRule;

        // (EN) ── Case 2: ∫ 1/(a*x + b) dx = ln|a*x+b|/a. (ZH) ── 情形 2：∫ 1/(a*x + b) dx = ln|a*x+b|/a。
        if (Expression.IsMinusOne(expExpr))
        {
            var a = TryExtractLinearCoeff(baseExpr, variable);
            if (a is not null)
            {
                var rule = new SimpleLogRule
                {
                    Integrand = integrand, Variable = variable,
                    LinearTerm = baseExpr
                };
                if (!Expression.IsOne(a))
                    return new ConstantTimesRule
                    {
                        Integrand = integrand, Variable = variable,
                        Constant = Divide(One, a), Other = baseExpr, Substeps = rule
                    };
                return rule;
            }
            // (EN) Not a linear denominator: cannot use the simple log rule. (ZH) 分母不是线性式，不能用简单对数规则。
            return null;
        }

        // (EN) ── Case 3: ∫ 1/(a*x + b)^k dx = (a*x+b)^(1-k)/(a*(1-k)). (ZH) ── 情形 3：∫ 1/(a*x + b)^k dx = (a*x+b)^(1-k)/(a*(1-k))。
        if (expExpr is Expression.Number expN && expN.Value.IsInteger)
        {
            var a = TryExtractLinearCoeff(baseExpr, variable);
            // (EN) The power rule requires a linear base; otherwise decline. (ZH) 幂规则要求线性底，否则放弃。
            if (a is null) return null;
            var rule = new SimplePowerRule
            {
                Integrand = integrand, Variable = variable,
                LinearTerm = baseExpr, Exponent = expExpr
            };
            if (!Expression.IsOne(a))
                return new ConstantTimesRule
                {
                    Integrand = integrand, Variable = variable,
                    Constant = Divide(One, a), Other = baseExpr, Substeps = rule
                };
            return rule;
        }

        return null;
    }

    /// <summary>
    /// (EN) Try partial fraction decomposition for Product denominators.
    /// (ZH) 尝试对乘积分母进行部分分式分解。
    /// </summary>
    private bool TryPartialFractions(Expression denom, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        // (EN) Collect linear factors from the denominator. (ZH) 从分母中收集线性因子。
        var factors = Algebraic.Factors(denom);
        if (factors.Count < 2) return false;

        // (EN) Check each factor is linear in v (v - r) or (a*v + b). (ZH) 检查每个因子是否为 v 的线性形式 (v - r) 或 (a*v + b)。
        var linearFactors = new List<(Expression Expr, Expression Root, Expression Coeff)>();
        foreach (var f in factors)
        {
            if (TryGetLinearRoot(f, v, out var root, out var coeff))
                linearFactors.Add((f, root, coeff));
            else
                return false; // Non-linear factor found
        }

        if (linearFactors.Count < 2) return false;

        // (EN) Cover-up method for distinct linear factors.
        // (ZH) 相异线性因子的遮盖法（cover-up method）。
        // (EN) For each factor (v - r_i), coefficient A_i = 1 / ∏_{j≠i} (r_i - r_j).
        // (ZH) 对每个因子 (v - r_i)，系数 A_i = 1 / ∏_{j≠i} (r_i - r_j)。
        var terms = new List<IntegrationRule>();
        var addTerms = new List<Expression>();

        for (int i = 0; i < linearFactors.Count; i++)
        {
            var ri = linearFactors[i].Root;
            Expression ai = One;

            for (int j = 0; j < linearFactors.Count; j++)
            {
                if (i == j) continue;
                var rj = linearFactors[j].Root;
                ai = ai * (ri - rj);
            }

            // (EN) A_i = 1 / ∏_{j≠i} (r_i - r_j). (ZH) A_i = 1 / ∏_{j≠i} (r_i - r_j)。
            ai = Divide(One, ai);

            // (EN) If coeff != 1, multiply: A_i = A_i / coeff. (ZH) 若 coeff != 1，则 A_i = A_i / coeff。
            var coeff_i = linearFactors[i].Coeff;
            if (!Expression.IsOne(coeff_i))
                ai = Divide(ai, coeff_i);

            var term = new SimpleLogRule
            {
                Integrand = linearFactors[i].Expr,
                Variable = v,
                LinearTerm = linearFactors[i].Expr
            };

            if (!Expression.IsOne(ai))
                terms.Add(new ConstantTimesRule
                {
                    Integrand = linearFactors[i].Expr, Variable = v,
                    Constant = ai, Other = linearFactors[i].Expr,
                    Substeps = term
                });
            else
                terms.Add(term);

            addTerms.Add(ai * Ln(linearFactors[i].Expr));
        }

        if (terms.Count >= 2)
        {
            rule = new AddRule
            {
                Integrand = denom, Variable = v,
                Substeps = terms
            };
            return true;
        }
        return false;
    }

    /// <summary>
    /// (EN) If expr is (v - r) or (a*v + b), return (v - r) form with root r and coeff a.
    /// (ZH) 若表达式为 (v - r) 或 (a*v + b)，以 (v - r) 形式返回根 r 与系数 a。
    /// </summary>
    private static bool TryGetLinearRoot(Expression expr, Expression v,
        out Expression root, out Expression coeff)
    {
        root = Zero; coeff = One;
        if (expr.Equals(v)) { root = Zero; coeff = One; return true; }

        if (expr is Expression.Sum sum && sum.Terms.Count <= 2)
        {
            Expression? constTerm = null;
            Expression? coeffTerm = null;
            foreach (var t in sum.Terms)
            {
                if (!Structure.ContainsVariable(t, v))
                    constTerm = t;
                else if (t.Equals(v))
                    coeffTerm = One;
                else if (t is Expression.Product prod && prod.Factors.Any(f => f.Equals(v)))
                {
                    var others = prod.Factors.Where(f => !f.Equals(v)).ToList();
                    var c = others.Count == 1 ? others[0] : new Expression.Product(others);
                    // (EN) The coefficient must be free of the variable, otherwise the factor is not linear.
                    // (ZH) 系数必须与变量无关，否则该因子不是线性式。
                    if (Structure.ContainsVariable(c, v)) return false;
                    coeffTerm = c;
                }
                else return false;
            }
            if (coeffTerm is not null)
            {
                coeff = coeffTerm;
                root = constTerm is not null ? Negate(constTerm) / coeff : Zero;
                return true;
            }
            return false;
        }

        if (expr is Expression.Product prod2 && prod2.Factors.Any(f => f.Equals(v)))
        {
            var others = prod2.Factors.Where(f => !f.Equals(v)).ToList();
            var c2 = others.Count == 1 ? others[0] : new Expression.Product(others);
            // (EN) Coefficient must be variable-free. (ZH) 系数必须与变量无关。
            if (Structure.ContainsVariable(c2, v)) return false;
            coeff = c2;
            root = Zero;
            return true;
        }

        return false;
    }

    /// <summary>
    /// (EN) Check if integrand is 1/(baseExpr)^k where k &gt; 0.
    /// (ZH) 检查被积表达式是否为 1/(baseExpr)^k（k &gt; 0）。
    /// </summary>
    private static bool IsReciprocalPowerForm(Expression expr, Expression v,
        out Expression baseExpr, out Expression expExpr)
    {
        baseExpr = null!; expExpr = null!;
        if (expr is Expression.Power p)
        {
            // (EN) Check exponent is a negative integer. (ZH) 检查指数是否为负整数。
            if (p.Exponent is Expression.Number ne && ne.Value.IsInteger && ne.Value.IsNegative)
            {
                baseExpr = p.Base;
                expExpr = p.Exponent;
                return Structure.ContainsVariable(baseExpr, v);
            }
        }
        return false;
    }

    /// <summary>
    /// (EN) If expr = a*v + b, return a. If expr = v, return 1. Otherwise null.
    /// (ZH) 若 expr = a*v + b 则返回 a；若 expr = v 则返回 1；否则返回 null。
    /// </summary>
    private static Expression? TryExtractLinearCoeff(Expression expr, Expression v)
    {
        if (expr.Equals(v)) return One;

        // (EN) Sum form a·v + b: every variable-dependent term must be exactly v or c·v.
        // (ZH) 和式形式 a·v + b：每个含变量的项必须恰为 v 或 c·v。
        if (expr is Expression.Sum sum)
        {
            Expression? coeff = null;
            bool sawLinear = false;
            foreach (var t in sum.Terms)
            {
                if (!Structure.ContainsVariable(t, v)) continue;
                if (t.Equals(v))
                {
                    sawLinear = true;
                    coeff ??= One;
                    continue;
                }
                if (t is Expression.Product p && p.Factors.Count(f => f.Equals(v)) == 1)
                {
                    var others = p.Factors.Where(f => !f.Equals(v)).ToList();
                    var c = others.Count == 1 ? others[0] : new Expression.Product(others);
                    // (EN) Coefficient must be variable-free, else the term is not linear. (ZH) 系数必须与变量无关，否则该项非线性。
                    if (Structure.ContainsVariable(c, v)) return null;
                    sawLinear = true;
                    coeff = coeff is null ? c : Add(coeff, c);
                    continue;
                }
                // (EN) A variable term that is not linear (e.g. x²) ⇒ not an affine expression.
                // (ZH) 出现非线性变量项（如 x²）⇒ 不是仿射表达式。
                return null;
            }
            return sawLinear ? (coeff ?? One) : null;
        }

        // (EN) Product form c·v (exactly one variable factor). (ZH) 乘积形式 c·v（恰含一个变量因子）。
        if (expr is Expression.Product prod)
        {
            if (prod.Factors.Count(f => f.Equals(v)) != 1) return null;
            var others = prod.Factors.Where(f => !f.Equals(v)).ToList();
            var c = others.Count == 1 ? others[0] : new Expression.Product(others);
            if (Structure.ContainsVariable(c, v)) return null;
            return c;
        }

        return null;
    }

    // ── Internal helpers ──
    // (EN) Miscellaneous utility methods used by the solver strategies.
    // (ZH) 求解器策略使用的杂项工具方法。

    /// <summary>
    /// (EN) LIATE priority heuristic for integration by parts: Logarithmic &gt; InverseTrig &gt;
    ///      Algebraic &gt; Trigonometric &gt; Exponential. Higher number = higher priority for u.
    /// (ZH) 分部积分的 LIATE 优先级启发式：对数 &gt; 反三角 &gt; 代数 &gt; 三角 &gt; 指数。数字越大 u 的优先级越高。
    /// </summary>
    private static int LiatePriority(Expression e, Expression v)
    {
        if (!Structure.ContainsVariable(e, v)) return -1;
        return e switch
        {
            Expression.Function f => LiateFunctionPriority(f.Op),
            // (EN) A power of a function inherits that function's LIATE priority. (ZH) 函数幂继承该函数的 LIATE 优先级。
            Expression.Power p when p.Base is Expression.Function bf => LiateFunctionPriority(bf.Op),
            Expression.Power or Expression.Product or Expression.SymbolExpr => 3,
            _ => 0
        };
    }

    /// <summary>
    /// (EN) LIATE priority of a single function op: Logarithmic &gt; InverseTrig &gt; Algebraic(default) &gt;
    ///      Trigonometric &gt; Exponential.
    /// (ZH) 单个函数算符的 LIATE 优先级：对数 &gt; 反三角 &gt; 代数(默认) &gt; 三角 &gt; 指数。
    /// </summary>
    private static int LiateFunctionPriority(FunctionType op) => op switch
    {
        FunctionType.Ln or FunctionType.Lg => 5,
        FunctionType.Asin or FunctionType.Acos or FunctionType.Atan
            or FunctionType.Acsc or FunctionType.Asec or FunctionType.Acot => 4,
        FunctionType.Sin or FunctionType.Cos or FunctionType.Tan or FunctionType.Cot
            or FunctionType.Sec or FunctionType.Csc => 2,
        FunctionType.Exp => 1,
        _ => 3,
    };

    /// <summary>
    /// (EN) Walk the expression tree and collect candidate sub-expressions for u-substitution:
    ///      function nodes f(g(x)) and their arguments g(x), plus power bases and exponents that
    ///      depend on the integration variable.
    /// (ZH) 遍历表达式树，收集适合换元积分的候选子表达式：函数节点 f(g(x)) 及其参数 g(x)，
    ///      以及依赖积分变量的幂底数与指数。
    /// </summary>
    private static List<Expression> CollectPotentialSubstitutions(Expression e, Expression v)
    {
        var set = new HashSet<Expression>();
        void Walk(Expression x)
        {
            if (x is Expression.Function fn && Structure.ContainsVariable(fn.Argument, v))
            {
                // (EN) u = f(g(x)) itself (e.g. sin(x) in sin(x)·cos(x)). (ZH) u = f(g(x)) 本身（如 sin(x)·cos(x) 中的 sin(x)）。
                set.Add(fn);
                // (EN) u = g(x) when it is not just the bare variable (e.g. x² in exp(x²)). (ZH) 当 g(x) 不是裸变量时取 u = g(x)（如 exp(x²) 中的 x²）。
                if (!fn.Argument.Equals(v)) set.Add(fn.Argument);
                Walk(fn.Argument);
            }
            if (x is Expression.Power pwr)
            {
                if (Structure.ContainsVariable(pwr.Base, v) && !pwr.Base.Equals(v)) set.Add(pwr.Base);
                if (Structure.ContainsVariable(pwr.Exponent, v)) set.Add(pwr.Exponent);
                Walk(pwr.Base);
                Walk(pwr.Exponent);
            }
            if (x is Expression.Sum sum) { foreach (var t in sum.Terms) Walk(t); }
            if (x is Expression.Product prod) { foreach (var fact in prod.Factors) Walk(fact); }
        }
        Walk(e);
        return set.ToList();
    }

    /// <summary>
    /// (EN) Rewrites the integrand as F(u)·c·u' by cancelling each variable-dependent factor of the
    ///      derivative u' against a matching factor of the integrand and folding the constant part of
    ///      u' into c. Returns (F(u), c) or null when u' is not a factor of the integrand.
    /// (ZH) 通过将导数 u' 的每个含变量因式与被积表达式的对应因式相消、并把 u' 的常数部分并入 c，
    ///      把被积式写成 F(u)·c·u'。当 u' 不是被积式的因式时返回 null。
    /// </summary>
    private static (Expression Remaining, Expression Coeff)? FactorOutDerivative(
        Expression integrand, Expression du, Expression variable)
    {
        if (integrand.Equals(du)) return (One, One);

        var integrandFactors = ExpandFactors(integrand);
        var duFactors = ExpandFactors(du);
        Expression coeff = One;

        foreach (var df in duFactors)
        {
            // (EN) Constant part of the derivative folds into the coefficient. (ZH) 导数的常数部分并入系数。
            if (!Structure.ContainsVariable(df, variable))
            {
                coeff = Multiply(coeff, Divide(One, df));
                continue;
            }

            var (dBase, dExp) = Algebraic.AsPower(df);
            int match = -1;
            Expression? replacement = null;
            for (int i = 0; i < integrandFactors.Count; i++)
            {
                if (!Structure.ContainsVariable(integrandFactors[i], variable)) continue;
                var (fBase, fExp) = Algebraic.AsPower(integrandFactors[i]);
                if (!dBase.Equals(fBase)) continue;

                // (EN) Cancel the derivative's exponent against the integrand factor's exponent,
                //      splitting the latter when it is a higher power (e.g. x³ against x).
                // (ZH) 用被积因式的指数抵消导数的指数；当被积因式是更高次幂时将其拆分（如 x³ 对 x）。
                if (fExp is Expression.Number fe && dExp is Expression.Number de
                    && fe.Value.IsInteger && de.Value.IsInteger)
                {
                    var diff = Subtract(fe, de);
                    if (diff is Expression.Number dn)
                    {
                        if (dn.Value.IsNegative) return null;
                        replacement = dn.Value.IsZero ? null
                            : (dn.Value.IsOne ? fBase : Pow(fBase, dn));
                        match = i;
                        break;
                    }
                }
                // (EN) Non-numeric exponents must match exactly to be cancelled. (ZH) 非数值指数须完全相等才能相消。
                if (fExp.Equals(dExp)) { replacement = null; match = i; break; }
                return null;
            }

            if (match < 0) return null;
            if (replacement is null) integrandFactors.RemoveAt(match);
            else integrandFactors[match] = replacement;
        }

        var remaining = integrandFactors.Count == 0 ? One
            : integrandFactors.Count == 1 ? integrandFactors[0]
            : new Expression.Product(integrandFactors);
        return (remaining, coeff);
    }

    /// <summary>
    /// (EN) Lists the multiplicative factors of an expression, distributing an integer power over a
    ///      product base first: 1/(x·ln x) → [x⁻¹, ln(x)⁻¹]. This lets a derivative factor cancel
    ///      against a factor hidden inside a reciprocal.
    /// (ZH) 列出表达式的乘法因式；先对乘积底数分配整数次幂：1/(x·ln x) → [x⁻¹, ln(x)⁻¹]。这样导数因式
    ///      才能与隐藏在倒数内部的因式相消。
    /// </summary>
    private static List<Expression> ExpandFactors(Expression e)
    {
        if (e is Expression.Power p && p.Base is Expression.Product bp
            && p.Exponent is Expression.Number ne && ne.Value.IsInteger
            && bp.Factors.Count >= 2)
        {
            return bp.Factors.Select(f => Pow(f, p.Exponent)).ToList();
        }
        return Algebraic.Factors(e).ToList();
    }

    // ── Linear argument helpers ──
    // (EN) Helpers for matching f(a·x + b) patterns and applying u-substitution.
    // (ZH) 匹配 f(a·x + b) 模式并应用换元积分的辅助方法。

    /// <summary>
    /// (EN) Match a function with direct variable argument.
    /// (ZH) 匹配以变量为直接参数的函数。
    /// </summary>
    private static IntegrationRule? MatchDirectTrig(Expression.Function f, Expression v)
    {
        return f.Op switch
        {
            FunctionType.Sin => (IntegrationRule)new SinRule { Integrand = f, Variable = v },
            FunctionType.Cos => new CosRule { Integrand = f, Variable = v },
            FunctionType.Tan => new TanRule { Integrand = f, Variable = v },
            FunctionType.Cot => new CotRule { Integrand = f, Variable = v },
            FunctionType.Sec => new SecRule { Integrand = f, Variable = v },
            FunctionType.Csc => new CscRule { Integrand = f, Variable = v },
            FunctionType.Sinh => new SinhRule { Integrand = f, Variable = v },
            FunctionType.Cosh => new CoshRule { Integrand = f, Variable = v },
            FunctionType.Tanh => new TanhRule { Integrand = f, Variable = v },
            FunctionType.Coth => new CothRule { Integrand = f, Variable = v },
            FunctionType.Sech => new SechRule { Integrand = f, Variable = v },
            FunctionType.Csch => new CschRule { Integrand = f, Variable = v },
            FunctionType.Exp => new ExpRule { Integrand = f, Variable = v, Base = E, Exp = v },
            // (EN) ∫ asec(x) dx, ∫ acsc(x) dx have elementary closed forms. (ZH) ∫asec(x)、∫acsc(x) 有初等闭式。
            FunctionType.Asec => new InverseSecRule { Integrand = f, Variable = v, IsCsc = false },
            FunctionType.Acsc => new InverseSecRule { Integrand = f, Variable = v, IsCsc = true },
            _ => null,
        };
    }

    /// <summary>
    /// (EN) Check if expr is a*var + b (linear in var).
    /// (ZH) 检查表达式是否为 a*var + b（关于 var 的线性形式）。
    /// </summary>
    private static bool TryGetLinearCoeffs(Expression expr, Expression v,
        out Expression a, out Expression b)
    {
        a = One; b = Zero;
        if (expr is Expression.Sum sum && sum.Terms.Count <= 3)
        {
            Expression? foundA = null, foundB = null;
            foreach (var t in sum.Terms)
            {
                if (!Structure.ContainsVariable(t, v))
                    foundB = foundB is null ? t : Add(foundB, t);
                else if (t.Equals(v))
                    foundA = foundA is null ? One : Add(foundA, One);
                else if (t is Expression.Product prod &&
                         prod.Factors.Count(f => f.Equals(v)) == 1)
                {
                    var others = prod.Factors.Where(f => !f.Equals(v)).ToList();
                    var c = others.Count == 1 ? others[0] : new Expression.Product(others);
                    if (Structure.ContainsVariable(c, v)) return false;
                    // (EN) Accumulate coefficients for a·x + b·x. (ZH) 累加系数以支持 a·x + b·x。
                    foundA = foundA is null ? c : Add(foundA, c);
                }
                else return false; // non-linear
            }
            if (foundA is not null && Structure.ContainsVariable(foundA, v)) return false;
            a = foundA ?? One;
            b = foundB ?? Zero;
            return foundA is not null;
        }
        if (expr is Expression.Product p && p.Factors.Any(f => f.Equals(v)))
        {
            var others = p.Factors.Where(f => !f.Equals(v)).ToList();
            a = others.Count == 1 ? others[0] : new Expression.Product(others);
            if (Structure.ContainsVariable(a, v)) return false;
            b = Zero;
            return true;
        }
        if (expr.Equals(v)) { a = One; b = Zero; return true; }
        return false;
    }

    /// <summary>
    /// (EN) Handle f(a*x+b) via u-substitution.
    /// (ZH) 通过换元积分处理 f(a*x+b)。
    /// </summary>
    private IntegrationRule? TryLinearFunctionRule(Expression.Function f,
        Expression coeffA, Expression coeffB, Expression v)
    {
        // (EN) Build u = a*x + b. (ZH) 构造 u = a*x + b。
        var uExpr = Expression.IsZero(coeffB)
            ? (Expression)Multiply(coeffA, v)
            : Add(Multiply(coeffA, v), coeffB);

        // (EN) du/dx = a, so dx = du/a. (ZH) du/dx = a，因此 dx = du/a。
        var uVar = Symbol("__u__");
        var fOfU = f with { Argument = uVar }; // (EN) create f(__u__). (ZH) 创建 f(__u__)。
        var substep = Solve(fOfU, uVar);
        if (!substep.ContainsDontKnow)
        {
            var rule = new URule
            {
                Integrand = f, Variable = v,
                UVar = uVar, UFunc = uExpr, Substeps = substep
            };
            // (EN) Wrap with 1/a only when a != 1. (ZH) 仅当 a != 1 时用 1/a 包装。
            if (!Expression.IsOne(coeffA))
                return new ConstantTimesRule
                {
                    Integrand = f, Variable = v,
                    Constant = Divide(One, coeffA), Other = f, Substeps = rule
                };
            return rule;
        }
        return null;
    }

    /// <summary>
    /// (EN) Null-safe structural equality test between two expressions.
    /// (ZH) 两个表达式之间空值安全的结构相等比较。
    /// </summary>
    /// <param name="a">(EN) Left expression (may be null). (ZH) 左表达式（可为 null）。</param>
    /// <param name="b">(EN) Right expression. (ZH) 右表达式。</param>
    /// <returns>(EN) True when <paramref name="a"/> is non-null and equals <paramref name="b"/>. (ZH) 当 <paramref name="a"/> 非空且等于 <paramref name="b"/> 时为 true。</returns>
    private static bool ExpressionEquals(Expression a, Expression b)
        => a is not null && a.Equals(b);
}
