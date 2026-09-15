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

        // (EN) 3c. Expand polynomials/products of sums, then integrate term by term. (ZH) 3c. 展开多项式/和式的乘积，再逐项积分。
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

        // (EN) ∫ 1/(a+bx²) dx  (ArctanRule). (ZH) ∫ 1/(a+bx²) dx（ArctanRule）。
        if (TryMatchArctan(integrand, variable, out var arctanRule))
            return arctanRule;

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
        // (EN) Pattern: exp(-x²). (ZH) 模式：exp(-x²)。
        if (integrand is Expression.Function { Op: FunctionType.Exp } f &&
            f.Argument is Expression.Power p && p.Base.Equals(variable) &&
            p.Exponent is Expression.Number n && n.Value.Numerator == 2 && n.Value.Denominator == 1 &&
            f.Argument is Expression.Power p2 && p2.Exponent is Expression.Number { Value: var expVal }
            && expVal.Numerator == 2 && expVal.Denominator == 1)
        {
            // (EN) exp(-x²) → need to check for negative. (ZH) exp(-x²) → 需要检查负号。
            if (f.Argument is Expression.Power { Exponent: Expression.Number { Value: var ev2 } })
            {
                // (EN) The Power is x^2, but we need -x^2 as the argument to exp.
                // (ZH) Power 是 x^2，但需要 -x^2 作为 exp 的参数。
                rule = new ErfRule { Integrand = integrand, Variable = variable };
                return true;
            }
        }
        // (EN) exp(-x²) where the inner is Pow(x, 2) multiplied by -1.
        // (ZH) exp(-x²)，其中内部是 Pow(x, 2) 乘以 -1。
        if (integrand is Expression.Function { Op: FunctionType.Exp, Argument: var arg } &&
            arg is Expression.Product prod && prod.Factors.Count == 2 &&
            prod.Factors[0] is Expression.Number neg && neg.Value.IsMinusOne &&
            prod.Factors[1] is Expression.Power pw && pw.Base.Equals(variable) &&
            pw.Exponent is Expression.Number n2 && n2.Value.ToInt32() == 2)
        {
            rule = new ErfRule { Integrand = integrand, Variable = variable };
            return true;
        }
        // (EN) Also handle Pow(x, 2) directly being the argument with Negate.
        // (ZH) 也处理 Pow(x, 2) 直接作为参数且带有 Negate 的情形。
        if (integrand is Expression.Function { Op: FunctionType.Exp, Argument: var arg2 } &&
            arg2 is Expression.Power pw2 && pw2.Base.Equals(variable) &&
            pw2.Exponent is Expression.Number n3 && n3.Value.ToInt32() == 2)
        {
            // (EN) exp(x²) is not an Erf integral. (ZH) exp(x²) 不是 Erf 积分。
        }
        return false;
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
        int foundCount = 0;

        foreach (var t in terms)
        {
            if (!Structure.ContainsVariable(t, v))
            {
                a = t; foundCount++;
            }
            else if (t is Expression.Power tp && tp.Base.Equals(v) &&
                     tp.Exponent is Expression.Number ne && ne.Value.IsInteger && ne.Value.ToInt32() == 2)
            {
                c = One; foundCount++;
            }
            else if (t is Expression.Product prod &&
                     prod.Factors.Any(f => f is Expression.Power tp2 && tp2.Base.Equals(v) &&
                         tp2.Exponent is Expression.Number ne2 && ne2.Value.IsInteger && ne2.Value.ToInt32() == 2))
            {
                var nonPower = prod.Factors.Where(f => !(f is Expression.Power)).ToList();
                c = nonPower.Count == 1 ? nonPower[0] : new Expression.Product(nonPower);
                foundCount++;
            }
            else if (t.Equals(v))
            {
                b = One; foundCount++;
            }
            else if (t is Expression.Product prod2 && prod2.Factors.Any(f => f.Equals(v)))
            {
                var others = prod2.Factors.Where(f => !f.Equals(v)).ToList();
                b = others.Count == 1 ? others[0] : new Expression.Product(others);
                foundCount++;
            }
        }
        return foundCount >= 2;
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

    // ── Polynomial expansion rewrite ─────────────────────────────
    // (EN) Expand a non-negative integer power of a sum, or a product containing sums, into a
    //     polynomial that the additive rule can integrate term by term.
    // (ZH) 将和式的非负整数次幂、或含和式的乘积展开为多项式，再由加法规则逐项积分。

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

    /// <summary>
    /// (EN) Enumerates the additive terms of an expression, recursively flattening nested sums.
    /// (ZH) 枚举表达式的加法项，并递归展平嵌套和式。
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
            var replaced = Structure.Substitute(cand, uVar, integrand);
            var substep = Solve(replaced, uVar);
            if (!substep.ContainsDontKnow)
            {
                return new URule
                {
                    Integrand = integrand, Variable = variable,
                    UVar = uVar, UFunc = cand, Substeps = substep
                };
            }
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
        || (e is Expression.FunctionN fn && fn.Op == FunctionNType.Log);

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
        if (expr is Expression.Sum sum && sum.Terms.Any(t => t.Equals(v) ||
            (t is Expression.Product prod && prod.Factors.Any(f => f.Equals(v)))))
        {
            foreach (var t in sum.Terms)
            {
                if (t is Expression.Product prod && prod.Factors.Any(f => f.Equals(v)))
                {
                    var others = prod.Factors.Where(f => !f.Equals(v)).ToList();
                    var c = others.Count == 1 ? others[0] : new Expression.Product(others);
                    // (EN) The coefficient must be variable-free. (ZH) 系数必须与变量无关。
                    if (Structure.ContainsVariable(c, v)) return null;
                    return c;
                }
            }
            return One;
        }
        if (expr is Expression.Product prod2 && prod2.Factors.Any(f => f.Equals(v)))
        {
            var others = prod2.Factors.Where(f => !f.Equals(v)).ToList();
            var c2 = others.Count == 1 ? others[0] : new Expression.Product(others);
            // (EN) The coefficient must be variable-free. (ZH) 系数必须与变量无关。
            if (Structure.ContainsVariable(c2, v)) return null;
            return c2;
        }
        if (expr.Equals(v)) return One;
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
            Expression.Function f => f.Op switch
            {
                FunctionType.Ln or FunctionType.Lg => 5,
                FunctionType.Asin or FunctionType.Acos or FunctionType.Atan
                    or FunctionType.Acsc or FunctionType.Asec or FunctionType.Acot => 4,
                FunctionType.Sin or FunctionType.Cos or FunctionType.Tan or FunctionType.Cot
                    or FunctionType.Sec or FunctionType.Csc => 2,
                FunctionType.Exp => 1,
                _ => 3,
            },
            Expression.Power or Expression.Product or Expression.SymbolExpr => 3,
            _ => 0
        };
    }

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

            var (dBase, dExp) = AsPower(df);
            int match = -1;
            Expression? replacement = null;
            for (int i = 0; i < integrandFactors.Count; i++)
            {
                if (!Structure.ContainsVariable(integrandFactors[i], variable)) continue;
                var (fBase, fExp) = AsPower(integrandFactors[i]);
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
    /// (EN) Splits an expression into (base, exponent): a Power yields its parts, anything else is
    ///      treated as a first power.
    /// (ZH) 将表达式拆成 (底, 指数)：幂返回其底与指数，其它表达式视为一次幂。
    /// </summary>
    private static (Expression Base, Expression Exp) AsPower(Expression e) =>
        e is Expression.Power p ? (p.Base, p.Exponent) : (e, One);

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
                if (!Structure.ContainsVariable(t, v)) foundB = t;
                else if (t.Equals(v)) foundA = One;
                else if (t is Expression.Product prod &&
                         prod.Factors.Any(f => f.Equals(v)))
                {
                    var others = prod.Factors.Where(f => !f.Equals(v)).ToList();
                    foundA = others.Count == 1 ? others[0] : new Expression.Product(others);
                }
                else return false; // non-linear
            }
            a = foundA ?? One;
            b = foundB ?? Zero;
            return foundA is not null;
        }
        if (expr is Expression.Product p && p.Factors.Any(f => f.Equals(v)))
        {
            var others = p.Factors.Where(f => !f.Equals(v)).ToList();
            a = others.Count == 1 ? others[0] : new Expression.Product(others);
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
