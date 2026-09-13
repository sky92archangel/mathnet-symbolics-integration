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
            // 1. Try direct atomic rules first
            var rule = MatchAtomicRules(integrand, variable);
            if (rule is not null) return rule;

            // 2. Try sum rule
            rule = MatchSumRule(integrand, variable);
            if (rule is not null) return rule;

            // 3. Try constant extraction from product
            rule = MatchConstantTimesRule(integrand, variable);
            if (rule is not null) return rule;

            return new DontKnowRule { Integrand = integrand, Variable = variable };
        }
        finally
        {
            _depth--;
        }
    }

    private IntegrationRule? MatchAtomicRules(Expression integrand, Expression variable)
    {
        // ∫ a dx (constant not containing variable)
        if (!Structure.ContainsVariable(integrand, variable))
            return new ConstantRule
            {
                Integrand = integrand,
                Variable = variable,
                Constant = integrand
            };

        // Patterns: direct variable matching
        if (integrand is Expression.Function f)
        {
            if (f.Argument.Equals(variable))
            {
                switch (f.Op)
                {
                    case FunctionType.Sin:
                        return new SinRule { Integrand = integrand, Variable = variable };
                    case FunctionType.Cos:
                        return new CosRule { Integrand = integrand, Variable = variable };
                    case FunctionType.Sinh:
                        return new SinhRule { Integrand = integrand, Variable = variable };
                    case FunctionType.Cosh:
                        return new CoshRule { Integrand = integrand, Variable = variable };
                    case FunctionType.Exp:
                        return new ExpRule { Integrand = integrand, Variable = variable, Base = Operators.E, Exp = variable };
                }
            }
        }

        // ∫ e^x dx (Exp alone)
        if (integrand is Expression.Function { Op: FunctionType.Exp } fexp &&
            fexp.Argument.Equals(variable))
        {
            return new ExpRule { Integrand = integrand, Variable = variable, Base = Operators.E, Exp = variable };
        }

        // ∫ x^n dx
        if (TryPowerMatch(integrand, variable, out var powerRule))
            return powerRule;

        // ∫ 1/x dx
        if (integrand is Expression.Power { Base: var pb, Exponent: var pe } &&
            pb.Equals(variable) && Expression.IsMinusOne(pe))
        {
            return new ReciprocalRule { Integrand = integrand, Variable = variable, Base = variable };
        }

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
                Integrand = integrand,
                Variable = variable,
                Base = p.Base,
                Exp = p.Exponent
            };
            return true;
        }

        // x^1 → x (no explicit Power node)
        if (integrand.Equals(variable))
        {
            rule = new PowerRule
            {
                Integrand = integrand,
                Variable = variable,
                Base = variable,
                Exp = One
            };
            return true;
        }

        return false;
    }

    private IntegrationRule? MatchSumRule(Expression integrand, Expression variable)
    {
        var summands = Algebraic.Summands(integrand);
        if (summands.Count <= 1) return null;

        var substeps = summands
            .Select(t => Solve(t, variable))
            .ToList();

        return new AddRule
        {
            Integrand = integrand,
            Variable = variable,
            Substeps = substeps
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
                Integrand = integrand,
                Variable = variable,
                Constant = coeff,
                Other = remaining,
                Substeps = subrule
            };
        }

        return null;
    }
}
