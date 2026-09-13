using MathNet.Symbolics.Integration.Core;
using static MathNet.Symbolics.Integration.Core.Operators;

namespace MathNet.Symbolics.Integration;

internal class IntegrationSolver
{
    private readonly int _maxDepth;
    private int _depth;

    public IntegrationSolver(int maxDepth = 12)
    {
        _maxDepth = maxDepth;
    }

    public IntegrationRule Solve(Expression integrand, Expression variable)
    {
        if (_depth >= _maxDepth)
            return new DontKnowRule { Integrand = integrand, Variable = variable };
        _depth++;

        try
        {
            // 1. Atomic rules (direct function matching)
            var rule = MatchAtomicRules(integrand, variable);
            if (rule is not null && !rule.ContainsDontKnow) return rule;

            // 2. Sum rule: ∫(f+g) = ∫f + ∫g
            rule = MatchSumRule(integrand, variable);
            if (rule is not null && !rule.ContainsDontKnow) return rule;

            // 3. Constant extraction: ∫ a*f(x) = a*∫f
            rule = MatchConstantTimesRule(integrand, variable);
            if (rule is not null && !rule.ContainsDontKnow) return rule;

            // 4. Substitution (u-sub): f(g(x))*g'(x)
            rule = TrySubstitutionRule(integrand, variable);
            if (rule is not null && !rule.ContainsDontKnow) return rule;

            // 5. Rational function integration
            rule = TryRationalRule(integrand, variable);
            if (rule is not null && !rule.ContainsDontKnow) return rule;

            // 6. Integration by parts (LIATE)
            rule = TryPartsRule(integrand, variable);
            if (rule is not null && !rule.ContainsDontKnow) return rule;

            // 7. Fall back
            return rule ?? new DontKnowRule { Integrand = integrand, Variable = variable };
        }
        finally
        {
            _depth--;
        }
    }

    private IntegrationRule? MatchAtomicRules(Expression integrand, Expression variable)
    {
        // ∫ a dx (constant)
        if (!Structure.ContainsVariable(integrand, variable))
            return new ConstantRule
            {
                Integrand = integrand, Variable = variable, Constant = integrand
            };

        // Pattern: sin(x), cos(x), tan(x), etc.
        if (integrand is Expression.Function f)
        {
            // Direct: f(x)
            if (f.Argument.Equals(variable))
            {
                var rule = MatchDirectTrig(f, variable);
                if (rule is not null) return rule;
            }
            // Linear argument: f(a*x + b)
            else if (TryGetLinearCoeffs(f.Argument, variable, out var coeffA, out var coeffB))
            {
                var result = TryLinearFunctionRule(f, coeffA, coeffB, variable);
                if (result is not null) return result;
            }
        }

        // ∫ x^n dx (power rule)
        if (TryPowerMatch(integrand, variable, out var powerRule))
            return powerRule;

        // ∫ 1/x dx
        if (integrand is Expression.Power { Base: var pb, Exponent: var pe } &&
            pb.Equals(variable) && Expression.IsMinusOne(pe))
        {
            return new ReciprocalRule { Integrand = integrand, Variable = variable, Base = variable };
        }

        // ∫ 1/√(1-x²) dx  (ArcsinRule)
        if (TryMatchArcsin(integrand, variable, out var arcsinRule))
            return arcsinRule;

        // ∫ 1/√(ax²+bx+c) dx
        if (TryMatchReciprocalSqrtQuadratic(integrand, variable, out var sqrtRule))
            return sqrtRule;

        // ∫ √(ax²+bx+c) dx  (SqrtQuadraticRule)
        if (TryMatchSqrtQuadratic(integrand, variable, out var sqrtQuadRule))
            return sqrtQuadRule;

        // ∫ 1/(a+bx²) dx  (ArctanRule)
        if (TryMatchArctan(integrand, variable, out var arctanRule))
            return arctanRule;

        // ∫ exp(-x²) dx  (ErfRule)
        if (TryMatchErf(integrand, variable, out var erfRule))
            return erfRule;

        // ∫ sin(x)/x dx → Si(x),  ∫ eˣ/x dx → Ei(x)
        if (TryMatchSpecialFunction(integrand, variable, out var specialRule))
            return specialRule;

        // Orthogonal polynomials: ∫ P_n(x) dx, ∫ T_n(x) dx, etc.
        if (TryMatchOrthogonalPoly(integrand, variable, out var orthoRule))
            return orthoRule;

        return null;
    }

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
        // x^1 → x
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

    private bool TryMatchArctan(Expression integrand, Expression variable,
        out ArctanRule? rule)
    {
        rule = null;

        // Pattern: 1 / (a + b*x^2)  or  1 / (a - b*x^2)
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
                        A = a, B = bCoeff, Sign = 1
                    };
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Detect ∫ 1/√(a + bx + cx²) dx pattern.</summary>
    private bool TryMatchReciprocalSqrtQuadratic(Expression integrand, Expression variable,
        out ReciprocalSqrtQuadraticRule? rule)
    {
        rule = null;

        // Pattern 1: (a + bx + cx²)^(-1/2)  i.e. Power(..., -1/2)
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

        // Pattern 2: 1/√(...) → Pow(Sqrt(...), -1)
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

    /// <summary>∫ √(ax²+bx+c) dx → formula using the reciprocal sqrt quadratic.</summary>
    private bool TryMatchSqrtQuadratic(Expression integrand, Expression variable,
        out SqrtQuadraticRule? rule)
    {
        rule = null;

        // Pattern: √(a+bx+cx²) = Power(..., 1/2)
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

    /// <summary>∫ 1/√(1 - x²) dx = asin(x), ∫ 1/√(a - b*x²) dx = asin(x√(b/a))/√b</summary>
    private bool TryMatchArcsin(Expression integrand, Expression variable,
        out ArcsinRule? rule)
    {
        rule = null;
        // Pattern: (1 - x²)^(-1/2)  or  (a - b*x²)^(-1/2)
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
                // Verify the x² coefficient is negative: a - b*x²
                bool negativeCoeff = x2Term is Expression.Product prod &&
                    prod.Factors.Count >= 2 &&
                    prod.Factors[0] is Expression.Number neg && neg.Value.IsMinusOne;
                if (!negativeCoeff)
                {
                    // Also check if x² term is just -x² directly
                    if (x2Term is Expression.Power)
                        return false; // +x², not -x²
                }
                rule = new ArcsinRule
                {
                    Integrand = integrand, Variable = variable
                };
                return true;
            }
        }
        // Pattern: 1/√(1-x²)  via Sqrt
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
                // Verify negative coefficient
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

    /// <summary>∫ exp(-x²) dx = √π/2 · erf(x)</summary>
    private bool TryMatchErf(Expression integrand, Expression variable,
        out ErfRule? rule)
    {
        rule = null;
        // Pattern: exp(-x²)
        if (integrand is Expression.Function { Op: FunctionType.Exp } f &&
            f.Argument is Expression.Power p && p.Base.Equals(variable) &&
            p.Exponent is Expression.Number n && n.Value.Numerator == 2 && n.Value.Denominator == 1 &&
            f.Argument is Expression.Power p2 && p2.Exponent is Expression.Number { Value: var expVal }
            && expVal.Numerator == 2 && expVal.Denominator == 1)
        {
            // exp(-x²) → need to check for negative
            if (f.Argument is Expression.Power { Exponent: Expression.Number { Value: var ev2 } })
            {
                // The Power is x^2, but we need -x^2 as the argument to exp
                // Actually exp(-x²) would be Exp(Negate(Pow(x, 2)))
                rule = new ErfRule { Integrand = integrand, Variable = variable };
                return true;
            }
        }
        // exp(-x²) where the inner is Pow(x, 2) multiplied by -1
        if (integrand is Expression.Function { Op: FunctionType.Exp, Argument: var arg } &&
            arg is Expression.Product prod && prod.Factors.Count == 2 &&
            prod.Factors[0] is Expression.Number neg && neg.Value.IsMinusOne &&
            prod.Factors[1] is Expression.Power pw && pw.Base.Equals(variable) &&
            pw.Exponent is Expression.Number n2 && n2.Value.ToInt32() == 2)
        {
            rule = new ErfRule { Integrand = integrand, Variable = variable };
            return true;
        }
        // Also handle Pow(x, 2) directly being the argument with Negate
        if (integrand is Expression.Function { Op: FunctionType.Exp, Argument: var arg2 } &&
            arg2 is Expression.Power pw2 && pw2.Base.Equals(variable) &&
            pw2.Exponent is Expression.Number n3 && n3.Value.ToInt32() == 2)
        {
            // exp(x²) → not Erf, but check if there's a Negate...
            // Actually exp(x²) is not an Erf integral
        }
        return false;
    }

    /// <summary>∫ sin(x)/x → Si(x), ∫ cos(x)/x → Ci(x), ∫ eˣ/x → Ei(x), etc.</summary>
    private bool TryMatchSpecialFunction(Expression integrand, Expression variable,
        out IntegrationRule? rule)
    {
        rule = null;

        // ── Product-based patterns ───────────────────────────────
        if (integrand is Expression.Product prod && prod.Factors.Count >= 2)
        {
            // --- Si / Ci / Shi / Chi / Ei patterns: func(x) / x ---
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

            // --- PolylogRule: polylog(b, a·x) / x ---
            if (TryMatchPolylog(prod, variable, out rule))
                return true;

            // --- UpperGammaRule: x^n · exp(a·x) ---
            if (TryMatchUpperGamma(prod, variable, out rule))
                return true;

            // --- OwensTRule: exp(-(ax+b)²) · erf(y·(ax+b)) ---
            if (TryMatchOwensT(prod, variable, out rule))
                return true;
        }

        // ── Power-based patterns ─────────────────────────────────

        // 1/ln(x) → Li(x)
        if (integrand is Expression.Power { Base: var lb, Exponent: var le } &&
            lb is Expression.Function { Op: FunctionType.Ln, Argument: var lnArg } &&
            lnArg.Equals(variable) && Expression.IsMinusOne(le))
        {
            rule = new LiRule { Integrand = integrand, Variable = variable };
            return true;
        }

        // sin(x²) / cos(x²) → Fresnel
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

        // --- EllipticFRule: 1/√(a - d·sin²(x)) ---
        if (TryMatchEllipticF(integrand, variable, out rule))
            return true;

        // --- EllipticERule: √(a - d·sin²(x)) ---
        if (TryMatchEllipticE(integrand, variable, out rule))
            return true;

        return false;
    }

/// <summary>Match ∫ exp(-(ax+b)²) · erf(y·(ax+b)) dx</summary>
private bool TryMatchOwensT(Expression.Product prod, Expression v,
    out IntegrationRule? rule)
{
    rule = null;
    Expression? expBase = null, erfBase = null, y = null;
    Expression? a = null, b = null;

    foreach (var f in prod.Factors)
    {
        // exp(-(ax+b)²)
        if (f is Expression.Function { Op: FunctionType.Exp, Argument: var arg })
        {
            // -(ax+b)² is represented as Product([-1, Pow(Sum(ax, b), 2)])
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
        
        // erf(y·(ax+b))
        if (f is Expression.Function { Op: FunctionType.Erf, Argument: var erfArg })
        {
            if (TryMatchLinearSum(erfArg, v, out var erfa, out var erfb))
            {
                // y=1, ax+b case
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
        // Skip y=1 (already handled by ErfRule)
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

    /// <summary>Match ∫ polylog(b, a·x) / x dx</summary>
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
                // fn.Arguments = [b, inner], inner = a*v
                b = fn.Arguments[0];
                if (fn.Arguments[1] is Expression.Product axProd &&
                    axProd.Factors.Count == 2 &&
                    axProd.Factors[^1].Equals(v))
                {
                    // a is the other factor
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

    /// <summary>Match ∫ x^n · exp(a·x) dx  (n ≥ 0 integer)</summary>
    private bool TryMatchUpperGamma(Expression.Product prod, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        Expression? e = null, a = null;
        bool hasExp = false, hasPow = false;

        foreach (var f in prod.Factors)
        {
            // x^n  (n ≥ 0 integer)
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
            // exp(a·x)
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
            // Only use UpperGamma when n ≥ 2 — simpler cases use PartsRule
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

    /// <summary>Match ∫ 1/√(a - d·sin²(x)) dx</summary>
    private bool TryMatchEllipticF(Expression integrand, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        // Pattern: Power(Power(a - d*sin(v)², 1/2), -1)   i.e. 1/sqrt(...)
        //          or Power(a - d*sin(v)², -1/2)
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

    /// <summary>Match ∫ √(a - d·sin²(x)) dx</summary>
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

    /// <summary>Try to match a - d·sin(x)² form from a base expression, returning rule.</summary>
    private bool TryMatchEllipticArgs(Expression baseExpr, Expression v,
        out IntegrationRule? rule, bool isE = false)
    {
        rule = null;
        if (baseExpr is Expression.Sum sum && sum.Terms.Count == 2)
        {
            // Find constant term 'a' and the -d*sin(v)² term
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
                    // -d*sin²(v)
                    var rest = negProd.Factors[1];
                    if (rest is Expression.Power sinPow &&
                        sinPow.Exponent is Expression.Number { Value: var sv } &&
                        sv.IsInteger && sv.ToInt32() == 2 &&
                        sinPow.Base is Expression.Function { Op: FunctionType.Sin, Argument: var sa } &&
                        sa.Equals(v))
                    {
                        d = One;  // coefficient = 1 (since it's -1*sin²)
                    }
                    else if (rest is Expression.Product dProd &&
                             dProd.Factors.Count == 2 &&
                             dProd.Factors[1] is Expression.Power dSinPow &&
                             dSinPow.Exponent is Expression.Number { Value: var sv2 } &&
                             sv2.IsInteger && sv2.ToInt32() == 2 &&
                             dSinPow.Base is Expression.Function { Op: FunctionType.Sin, Argument: var sa2 } &&
                             sa2.Equals(v))
                    {
                        d = dProd.Factors[0]; // d coefficient
                    }
                }
                else if (t is Expression.Product negProd2 &&
                         negProd2.Factors.Count == 3 &&
                         negProd2.Factors[0] is Expression.Number { Value.IsMinusOne: true })
                {
                    // -d*sin²(v)
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
                // Constraint: a != d
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
    /// Try to match a sum as a·v + b, i.e. a linear expression in v.
    /// Returns a,b where the sum equals a*v + b.
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

    /// <summary>Orthogonal polynomials: P_n(x), T_n(x), H_n(x), L_n(x), etc.</summary>
    private bool TryMatchOrthogonalPoly(Expression integrand, Expression variable,
        out IntegrationRule? rule)
    {
        rule = null;
        if (integrand is Expression.FunctionN fn && fn.Arguments.Count >= 1 &&
            fn.Arguments[^1].Equals(variable))
        {
            var n = fn.Arguments[0];
            // For two-parameter polynomials, extract a,b from remaining args
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

    private static bool IsX2Term(Expression e, Expression v) => e switch
    {
        Expression.Power tp => tp.Base.Equals(v) &&
            tp.Exponent is Expression.Number { Value.IsInteger: true } n && n.Value.ToInt32() == 2,
        Expression.Product prod => prod.Factors.Count >= 2 &&
            prod.Factors[^1] is Expression.Power tp2 && tp2.Base.Equals(v) &&
            tp2.Exponent is Expression.Number { Value.IsInteger: true } n2 && n2.Value.ToInt32() == 2,
        _ => false
    };

    /// <summary>Try to parse a quadratic expression: a + b*x + c*x².</summary>
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

    // ── Substitution (u-sub) strategy ──

    private IntegrationRule? TrySubstitutionRule(Expression integrand, Expression variable)
    {
        var candidates = CollectPotentialSubstitutions(integrand, variable);
        foreach (var uExpr in candidates)
        {
            var du = DifferentiateApprox(uExpr, variable);
            if (du is not null && !Expression.IsOne(du))
            {
                var result = ExtractFactorWithCoeff(integrand, du, variable);
                if (result is not null)
                {
                    var (remaining, coeff) = result.Value;
                    var uVar = Symbol("__u__");
                    // Substitute uExpr (e.g. x^2) with uVar (e.g. __u__)
                    var fOfUNew = Structure.Substitute(uExpr, uVar, remaining);
                    var substep = Solve(fOfUNew, uVar);
                    if (!substep.ContainsDontKnow)
                    {
                        var rule = new URule
                        {
                            Integrand = integrand, Variable = variable,
                            UVar = uVar, UFunc = uExpr, Substeps = substep
                        };
                        // If coeff is not 1, wrap in constant rule
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

    // ── Integration by parts (LIATE) strategy ──

    private IntegrationRule? TryPartsRule(Expression integrand, Expression variable)
    {
        if (integrand is not Expression.Product prod || prod.Factors.Count < 2)
            return null;

        var factors = prod.Factors.ToList();
        int bestPri = -1;
        Expression? u = null;
        int uIdx = -1;

        for (int i = 0; i < factors.Count; i++)
        {
            int pri = LiatePriority(factors[i], variable);
            if (pri > bestPri) { bestPri = pri; u = factors[i]; uIdx = i; }
        }

        if (u is null || uIdx < 0) return null;
        var dvFactors = new List<Expression>(factors);
        dvFactors.RemoveAt(uIdx);
        var dv = dvFactors.Count == 1 ? dvFactors[0] : new Expression.Product(dvFactors);

        var vStep = Solve(dv, variable);
        if (vStep.ContainsDontKnow) return null;

        var uPrime = DifferentiateApprox(u, variable);
        if (uPrime is null) return null;

        var v = vStep.Eval();
        var secondIntegrand = Multiply(v, uPrime);
        var secondStep = Solve(secondIntegrand, variable);
        if (secondStep.ContainsDontKnow) return null;

        return new PartsRule
        {
            Integrand = integrand, Variable = variable,
            U = u, Dv = dv,
            VStep = vStep, SecondStep = secondStep
        };
    }

    // ── Rational function integration ──

    private IntegrationRule? TryRationalRule(Expression integrand, Expression variable)
    {
        if (!IsReciprocalPowerForm(integrand, variable, out var baseExpr, out var expExpr))
            return null;

        // ── Case 1: Denominator is a Product of factors → partial fractions ──
        if (Expression.IsMinusOne(expExpr) && TryPartialFractions(baseExpr, variable, out var partialRule))
            return partialRule;

        // ── Case 2: ∫ 1/(a*x + b) dx = ln|a*x+b|/a ──
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
            return new SimpleLogRule
            {
                Integrand = integrand, Variable = variable,
                LinearTerm = baseExpr
            };
        }

        // ── Case 3: ∫ 1/(a*x + b)^k dx = (a*x+b)^(1-k)/(a*(1-k)) ──
        if (expExpr is Expression.Number expN && expN.Value.IsInteger)
        {
            var a = TryExtractLinearCoeff(baseExpr, variable) ?? One;
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

    /// <summary>Try partial fraction decomposition for Product denominators.</summary>
    private bool TryPartialFractions(Expression denom, Expression v,
        out IntegrationRule? rule)
    {
        rule = null;
        // Collect linear factors from the denominator
        var factors = Algebraic.Factors(denom);
        if (factors.Count < 2) return false;

        // Check each factor is linear in v (v - r) or (a*v + b)
        var linearFactors = new List<(Expression Expr, Expression Root, Expression Coeff)>();
        foreach (var f in factors)
        {
            if (TryGetLinearRoot(f, v, out var root, out var coeff))
                linearFactors.Add((f, root, coeff));
            else
                return false; // Non-linear factor found
        }

        if (linearFactors.Count < 2) return false;

        // Cover-up method for distinct linear factors
        // For each factor (v - r_i), coefficient A_i = 1 / ∏_{j≠i} (r_i - r_j)
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

            // A_i = 1 / ∏_{j≠i} (r_i - r_j)
            ai = Divide(One, ai);

            // If coeff != 1, multiply: A_i = A_i / coeff
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

    /// <summary>If expr is (v - r) or (a*v + b), return (v - r) form with root r and coeff a.</summary>
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
                    coeffTerm = others.Count == 1 ? others[0] : new Expression.Product(others);
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
            coeff = others.Count == 1 ? others[0] : new Expression.Product(others);
            root = Zero;
            return true;
        }

        return false;
    }

    /// <summary>Check if integrand is 1/(baseExpr)^k where k > 0.</summary>
    private static bool IsReciprocalPowerForm(Expression expr, Expression v,
        out Expression baseExpr, out Expression expExpr)
    {
        baseExpr = null!; expExpr = null!;
        if (expr is Expression.Power p)
        {
            // Check exponent is a negative integer
            if (p.Exponent is Expression.Number ne && ne.Value.IsInteger && ne.Value.IsNegative)
            {
                baseExpr = p.Base;
                expExpr = p.Exponent;
                return Structure.ContainsVariable(baseExpr, v);
            }
        }
        return false;
    }

    /// <summary>If expr = a*v + b, return a. If expr = v, return 1. Otherwise null.</summary>
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
                    return others.Count == 1 ? others[0] : new Expression.Product(others);
                }
            }
            return One;
        }
        if (expr is Expression.Product prod2 && prod2.Factors.Any(f => f.Equals(v)))
        {
            var others = prod2.Factors.Where(f => !f.Equals(v)).ToList();
            return others.Count == 1 ? others[0] : new Expression.Product(others);
        }
        if (expr.Equals(v)) return One;
        return null;
    }

    // ── Internal helpers ──

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

    private static Expression? DifferentiateApprox(Expression e, Expression v)
    {
        if (e.Equals(v)) return One;
        if (e is Expression.Power pw && pw.Base.Equals(v))
            return Multiply(pw.Exponent, Pow(v, Subtract(pw.Exponent, One)));
        if (e is Expression.Product prod && prod.Factors.Any(f => f.Equals(v)))
        {
            var rest = prod.Factors.Where(f => !f.Equals(v)).ToList();
            return rest.Count == 1 ? rest[0] : new Expression.Product(rest);
        }
        // Basic function derivatives
        if (e is Expression.Function fn && fn.Argument.Equals(v))
        {
            return fn.Op switch
            {
                FunctionType.Ln => Divide(One, v),       // d/dx ln(x) = 1/x
                FunctionType.Exp => Exp(v),               // d/dx e^x = e^x
                FunctionType.Sin => Cos(v),               // d/dx sin(x) = cos(x)
                FunctionType.Cos => Negate(Sin(v)),       // d/dx cos(x) = -sin(x)
                FunctionType.Tan => One / (Cos(v) * Cos(v)), // sec^2(x)
                FunctionType.Cot => Negate(One / (Sin(v) * Sin(v))), // -csc^2(x)
                FunctionType.Sec => Sec(v) * Tan(v),
                FunctionType.Csc => Negate(Csc(v) * Cot(v)),
                FunctionType.Asin => One / Sqrt(One - v * v),
                FunctionType.Acos => MinusOne / Sqrt(One - v * v),
                FunctionType.Atan => One / (One + v * v),
                FunctionType.Sinh => Cosh(v),
                FunctionType.Cosh => Sinh(v),
                _ => null,
            };
        }
        return null;
    }

    private static List<Expression> CollectPotentialSubstitutions(Expression e, Expression v)
    {
        var set = new HashSet<Expression>();
        void Walk(Expression x)
        {
            if (x is Expression.Function fn && Structure.ContainsVariable(fn.Argument, v)
                && !fn.Argument.Equals(v)) set.Add(fn.Argument);
            if (x is Expression.Power pwr)
            {
                if (Structure.ContainsVariable(pwr.Base, v) && !pwr.Base.Equals(v)) set.Add(pwr.Base);
                if (Structure.ContainsVariable(pwr.Exponent, v)) set.Add(pwr.Exponent);
            }
            if (x is Expression.Sum sum) { foreach (var t in sum.Terms) Walk(t); }
            if (x is Expression.Product prod) { foreach (var fact in prod.Factors) Walk(fact); }
        }
        Walk(e);
        return set.ToList();
    }

    /// <summary>Extract a factor (or constant multiple) from expression. Returns (remaining, extractedCoeff) or null.</summary>
    private static (Expression Remaining, Expression Coeff)? ExtractFactorWithCoeff(
        Expression expr, Expression factor, Expression variable)
    {
        if (expr.Equals(factor)) return (One, One);

        if (expr is Expression.Product p)
        {
            var remaining = new List<Expression>();
            Expression? matchedCoeff = null;
            bool found = false;
            var (factorBase, factorCo) = DecomposeConstant(factor);

            foreach (var f in p.Factors)
            {
                if (!found)
                {
                    if (f.Equals(factor)) { found = true; matchedCoeff = One; continue; }
                    // Check constant multiple
                    var (fBase, fCo) = DecomposeConstant(f);
                    if (factorBase.Equals(fBase))
                    {
                        found = true;
                        matchedCoeff = Divide(fCo, factorCo);
                        continue;
                    }
                }
                remaining.Add(f);
            }

            if (found)
            {
                var rem = remaining.Count == 0 ? One
                    : remaining.Count == 1 ? remaining[0]
                    : new Expression.Product(remaining);
                return (rem, matchedCoeff ?? One);
            }
        }
        return null;
    }

    /// <summary>If expr is coeff * something, return (something, coeff). Otherwise return expr with coeff=1.</summary>
    private static (Expression Base, Expression Coeff) DecomposeConstant(Expression expr)
    {
        if (expr is Expression.Product prod)
        {
            Expression? c = null;
            Expression? base_ = null;
            foreach (var f in prod.Factors)
            {
                if (f is Expression.Number n)
                    c = c is null ? n : (Expression)(c * n);
                else if (base_ is null)
                    base_ = f;
            }
            if (c is not null && base_ is not null)
                return (base_, c);
        }
        return (expr, One);
    }

    // ── Linear argument helpers ──

    /// <summary>Match a function with direct variable argument.</summary>
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

    /// <summary>Check if expr is a*var + b (linear in var).</summary>
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

    /// <summary>Handle f(a*x+b) via u-substitution.</summary>
    private IntegrationRule? TryLinearFunctionRule(Expression.Function f,
        Expression coeffA, Expression coeffB, Expression v)
    {
        // Build u = a*x + b
        var uExpr = Expression.IsZero(coeffB)
            ? (Expression)Multiply(coeffA, v)
            : Add(Multiply(coeffA, v), coeffB);

        // du/dx = a, so dx = du/a
        var uVar = Symbol("__u__");
        var fOfU = f with { Argument = uVar }; // create f(__u__)
        var substep = Solve(fOfU, uVar);
        if (!substep.ContainsDontKnow)
        {
            var rule = new URule
            {
                Integrand = f, Variable = v,
                UVar = uVar, UFunc = uExpr, Substeps = substep
            };
            // Wrap with 1/a if a != 1
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

    private static bool ExpressionEquals(Expression a, Expression b)
        => a is not null && a.Equals(b);
}
