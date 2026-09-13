using MathNet.Symbolics.Integration.Core;
using static MathNet.Symbolics.Integration.Core.Operators;

namespace MathNet.Symbolics.Integration;

public abstract record IntegrationRule
{
    public required Expression Integrand { get; init; }
    public required Expression Variable { get; init; }
    public abstract Expression Eval();
    public virtual bool ContainsDontKnow => false;
}

public abstract record AtomicRule : IntegrationRule
{
    public override bool ContainsDontKnow => false;
}

public sealed record ConstantRule : AtomicRule
{
    public required Expression Constant { get; init; }
    public override Expression Eval() => Multiply(Constant, Variable);
}

public sealed record PowerRule : AtomicRule
{
    public required Expression Base { get; init; }
    public required Expression Exp { get; init; }
    public override Expression Eval()
    {
        if (Expression.IsMinusOne(Exp))
            return Ln(Base);
        return Divide(Pow(Base, Add(Exp, One)), Add(Exp, One));
    }
}

public sealed record ReciprocalRule : AtomicRule
{
    public required Expression Base { get; init; }
    public override Expression Eval() => Ln(Base);
}

public sealed record ExpRule : AtomicRule
{
    public required Expression Base { get; init; }
    public required Expression Exp { get; init; }
    public override Expression Eval()
    {
        if (Base is Expression.Constant { Type: ConstantType.E })
            return new Expression.Function(FunctionType.Exp, Variable);
        return Divide(Integrand, Ln(Base));
    }
}

public sealed record SinRule : AtomicRule
{
    public override Expression Eval() => Negate(Cos(Variable));
}

public sealed record CosRule : AtomicRule
{
    public override Expression Eval() => Sin(Variable);
}

public sealed record SinhRule : AtomicRule
{
    public override Expression Eval() => Cosh(Variable);
}

public sealed record CoshRule : AtomicRule
{
    public override Expression Eval() => Sinh(Variable);
}

public sealed record ArcsinRule : AtomicRule
{
    public override Expression Eval() => Asin(Variable);
}

public sealed record ArcsinhRule : AtomicRule
{
    public override Expression Eval() => Asinh(Variable);
}

public sealed record AddRule : IntegrationRule
{
    public required IReadOnlyList<IntegrationRule> Substeps { get; init; }
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
    public override bool ContainsDontKnow => Substeps.Any(s => s.ContainsDontKnow);
}

public sealed record ConstantTimesRule : IntegrationRule
{
    public required Expression Constant { get; init; }
    public required Expression Other { get; init; }
    public required IntegrationRule Substeps { get; init; }
    public override Expression Eval() => Multiply(Constant, Substeps.Eval());
    public override bool ContainsDontKnow => Substeps.ContainsDontKnow;
}

public sealed record RewriteRule : IntegrationRule
{
    public required Expression Rewritten { get; init; }
    public required IntegrationRule Substeps { get; init; }
    public override Expression Eval() => Substeps.Eval();
    public override bool ContainsDontKnow => Substeps.ContainsDontKnow;
}

public sealed record URule : IntegrationRule
{
    public required Expression UVar { get; init; }
    public required Expression UFunc { get; init; }
    public required IntegrationRule Substeps { get; init; }
    public override Expression Eval()
    {
        var result = Substeps.Eval();
        return Structure.Substitute(UVar, UFunc, result);
    }
    public override bool ContainsDontKnow => Substeps.ContainsDontKnow;
}

public sealed record PartsRule : IntegrationRule
{
    public required Expression U { get; init; }
    public required Expression Dv { get; init; }
    public required IntegrationRule VStep { get; init; }
    public required IntegrationRule SecondStep { get; init; }
    public override Expression Eval()
    {
        var v = VStep.Eval();
        return Subtract(Multiply(U, v), SecondStep.Eval());
    }
    public override bool ContainsDontKnow => VStep.ContainsDontKnow || SecondStep.ContainsDontKnow;
}

public sealed record CyclicPartsRule : IntegrationRule
{
    public required IReadOnlyList<PartsRule> PartsRules { get; init; }
    public required Expression Coefficient { get; init; }
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
    public override bool ContainsDontKnow => PartsRules.Any(r => r.ContainsDontKnow);
}

public sealed record AlternativeRule : IntegrationRule
{
    public required IReadOnlyList<IntegrationRule> Alternatives { get; init; }
    public override Expression Eval() => Alternatives[0].Eval();
    public override bool ContainsDontKnow => Alternatives.Any(a => a.ContainsDontKnow);
}

public sealed record DontKnowRule : AtomicRule
{
    public override bool ContainsDontKnow => true;
    public override Expression Eval()
    {
        return new Expression.FunctionN(FunctionNType.Log, new[] { Integrand, Variable });
    }
}
