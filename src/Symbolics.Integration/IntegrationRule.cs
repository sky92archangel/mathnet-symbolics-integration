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

/// <summary>∫ √(a+bx+cx²) dx = ((2cx+b)/4c)·√(a+bx+cx²) - (b²-4ac)/(8c)·∫ 1/√(a+bx+cx²) dx</summary>
public sealed record SqrtQuadraticRule : AtomicRule
{
    public required Expression A { get; init; }
    public required Expression B { get; init; }
    public required Expression C { get; init; }
    public required IntegrationRule ReciprocalStep { get; init; }
    public override Expression Eval()
    {
        var (a, b, c, x) = (A, B, C, Variable);
        var sqrtQ = Sqrt(a + b * x + c * x * x);
        // Term1 = ((2*c*x + b) / (4*c)) * sqrt(a+bx+cx²)
        var term1 = ((Two * c * x + b) / (Four * c)) * sqrtQ;
        // Term2 = -(b² - 4*a*c) / (8*c) * ∫ 1/√(a+bx+cx²) dx
        var discriminant = b * b - Four * a * c;
        var coeff2 = -discriminant / (Eight * c);
        var term2 = coeff2 * ReciprocalStep.Eval();
        return term1 + term2;
    }

    private static readonly Expression Eight = Expression.Int32(8);
    private static readonly Expression Four = Expression.Int32(4);
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

/// <summary>∫ tan(x) dx = -ln|cos(x)|</summary>
public sealed record TanRule : AtomicRule
{
    public override Expression Eval() => Negate(Ln(Cos(Variable)));
}

/// <summary>∫ cot(x) dx = ln|sin(x)|</summary>
public sealed record CotRule : AtomicRule
{
    public override Expression Eval() => Ln(Sin(Variable));
}

/// <summary>∫ sec(x) dx = ln|sec(x) + tan(x)|</summary>
public sealed record SecRule : AtomicRule
{
    public override Expression Eval() => Ln(Sec(Variable) + Tan(Variable));
}

/// <summary>∫ csc(x) dx = -ln|csc(x) + cot(x)|</summary>
public sealed record CscRule : AtomicRule
{
    public override Expression Eval() => Negate(Ln(Csc(Variable) + Cot(Variable)));
}

/// <summary>∫ tanh(x) dx = ln(cosh(x))</summary>
public sealed record TanhRule : AtomicRule
{
    public override Expression Eval() => Ln(Cosh(Variable));
}

/// <summary>∫ coth(x) dx = ln|sinh(x)|</summary>
public sealed record CothRule : AtomicRule
{
    public override Expression Eval() => Ln(Sinh(Variable));
}

/// <summary>∫ sech(x) dx = 2·atan(e^x)</summary>
public sealed record SechRule : AtomicRule
{
    public override Expression Eval() =>
        Multiply(Two, Atan(Exp(Variable)));
}

/// <summary>∫ csch(x) dx = ln|tanh(x/2)|</summary>
public sealed record CschRule : AtomicRule
{
    public override Expression Eval() => Ln(Tanh(Divide(Variable, Two)));
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

// ──────────────────────────────────────────────
//  More advanced atomic rules
// ──────────────────────────────────────────────

/// <summary>∫ 1/(a + b*x^2) dx → atan / arctanh depending on sign.</summary>
public sealed record ArctanRule : AtomicRule
{
    /// <summary>Constant a</summary>
    public required Expression A { get; init; }
    /// <summary>Coefficient b</summary>
    public required Expression B { get; init; }
    /// <summary>Sign of the denominator: +1 for a+bx^2, -1 for a-bx^2</summary>
    public int Sign { get; init; } = 1;

    public override Expression Eval()
    {
        var x = Variable;
        // ∫ sign/(a + b*x^2) dx, where sign = ±1
        // = sign/(a*sqrt(b/a)) * atan(x*sqrt(b/a))  when a,b > 0
        if (Sign > 0)
        {
            var sqrtBA = Sqrt(Divide(B, A));
            return Divide(One, Multiply(A, sqrtBA)) * Atan(Multiply(sqrtBA, x));
        }
        else
        {
            // ∫ 1/(a - b*x^2) dx = 1/(2*a*sqrt(b/a)) * ln|(a + x*sqrt(a*b))/(a - x*sqrt(a*b))|
            var sqrtAB = Sqrt(Multiply(A, B));
            var inner = Divide(A + sqrtAB * x, A - sqrtAB * x);
            return Divide(One, Multiply(Two, sqrtAB)) * Ln(inner);
        }
    }
}

/// <summary>∫ (c*(a+b*x)^d)^e dx</summary>
public sealed record NestedPowRule : AtomicRule
{
    /// <summary>Base expression (a+b*x)</summary>
    public required Expression BaseVal { get; init; }
    /// <summary>Inner exponent</summary>
    public required Expression InnerExp { get; init; }
    /// <summary>Outer exponent</summary>
    public required Expression OuterExp { get; init; }

    public override Expression Eval()
    {
        // ∫ (a+bx)^n dx = (a+bx)^(n+1) / (b*(n+1))
        // For now assume a=0, b=1, so it's just PowerRule
        var n = OuterExp;
        if (Expression.IsMinusOne(n))
            return Ln(BaseVal);
        return Pow(BaseVal, n + One) / (n + One);
    }
}

/// <summary>∫ 1/√(a+b*x+c*x^2) dx</summary>
public sealed record ReciprocalSqrtQuadraticRule : AtomicRule
{
    public required Expression A { get; init; }
    public required Expression B { get; init; }
    public required Expression C { get; init; }

    public override Expression Eval()
    {
        var x = Variable;
        // ∫ 1/√(a+bx+cx²) dx = 1/√c * ln|2cx+b + 2√c*√(a+bx+cx²)|
        var sqrtC = Sqrt(C);
        var inner = Two * C * x + B + Two * sqrtC * Sqrt(A + B * x + C * x * x);
        return Divide(One, sqrtC) * Ln(inner);
    }
}

/// <summary>Rational function integration via partial fractions.</summary>
public sealed record RatintRule : AtomicRule
{
    public required Expression Numerator { get; init; }
    public required Expression Denominator { get; init; }
    public override Expression Eval() => Integrand; // Placeholder - handled inline in solver
}

/// <summary>∫ 1/(x-a) dx = ln|x-a|</summary>
public sealed record SimpleLogRule : AtomicRule
{
    /// <summary>The linear term x-a</summary>
    public required Expression LinearTerm { get; init; }
    public override Expression Eval() => Ln(LinearTerm);
}

/// <summary>∫ 1/(x-a)^k dx = (x-a)^(1-k)/(1-k)</summary>
public sealed record SimplePowerRule : AtomicRule
{
    public required Expression LinearTerm { get; init; }
    public required Expression Exponent { get; init; }
    public override Expression Eval()
    {
        var k = Exponent;
        return Pow(LinearTerm, One - k) / (One - k);
    }
}

// ──────────────────────────────────────────────
//  Special function rules
// ──────────────────────────────────────────────

/// <summary>∫ e^(-x²) dx = √π/2 · erf(x)</summary>
public sealed record ErfRule : AtomicRule
{
    public override Expression Eval()
    {
        // Symbolic result: sqrt(pi)/2 * erf(x)
        // Since we don't have erf built in, return the symbolic representation
        return (Sqrt(Pi) / Two) * new Expression.Function(FunctionType.Erf, Variable);
    }
}

/// <summary>Piecewise integration: different rules for different domains.</summary>
public sealed record PiecewiseRule : IntegrationRule
{
    public required IReadOnlyList<(IntegrationRule Rule, Expression Condition)> Pieces { get; init; }

    public override Expression Eval()
    {
        // For now, just return the first piece
        return Pieces[0].Rule.Eval();
    }
    public override bool ContainsDontKnow => Pieces.Any(p => p.Rule.ContainsDontKnow);
}

// ──────────────────────────────────────────────
//  Special function rules (sin(x)/x → Si(x) etc.)
// ──────────────────────────────────────────────

/// <summary>∫ sin(x)/x dx = Si(x)</summary>
public sealed record SiRule : AtomicRule
{
    public override Expression Eval() => new Expression.Function(FunctionType.Si, Variable);
}

/// <summary>∫ cos(x)/x dx = Ci(x)</summary>
public sealed record CiRule : AtomicRule
{
    public override Expression Eval() => new Expression.Function(FunctionType.Ci, Variable);
}

/// <summary>∫ sinh(x)/x dx = Shi(x)</summary>
public sealed record ShiRule : AtomicRule
{
    public override Expression Eval() => new Expression.Function(FunctionType.Shi, Variable);
}

/// <summary>∫ cosh(x)/x dx = Chi(x)</summary>
public sealed record ChiRule : AtomicRule
{
    public override Expression Eval() => new Expression.Function(FunctionType.Chi, Variable);
}

/// <summary>∫ eˣ/x dx = Ei(x)</summary>
public sealed record EiRule : AtomicRule
{
    public override Expression Eval() => new Expression.Function(FunctionType.Ei, Variable);
}

/// <summary>∫ 1/ln(x) dx = Li(x)</summary>
public sealed record LiRule : AtomicRule
{
    public override Expression Eval() => new Expression.Function(FunctionType.Li, Variable);
}

/// <summary>∫ sin(x²) dx = √(π/2)·FresnelS(√(2/π)·x)</summary>
public sealed record FresnelSRule : AtomicRule
{
    public override Expression Eval()
    {
        var sqrt2pi = Sqrt(Two / Pi);
        return Sqrt(Pi / Two) * new Expression.Function(FunctionType.FresnelS, sqrt2pi * Variable);
    }
}

/// <summary>∫ cos(x²) dx = √(π/2)·FresnelC(√(2/π)·x)</summary>
public sealed record FresnelCRule : AtomicRule
{
    public override Expression Eval()
    {
        var sqrt2pi = Sqrt(Two / Pi);
        return Sqrt(Pi / Two) * new Expression.Function(FunctionType.FresnelC, sqrt2pi * Variable);
    }
}

// ──────────────────────────────────────────────
//  Orthogonal polynomial rules
// ──────────────────────────────────────────────

public abstract record OrthogonalPolyRule : AtomicRule
{
    public required Expression N { get; init; }

    protected Expression PolyEval(FunctionNType type, Expression degree) =>
        new Expression.FunctionN(type, new[] { degree, Variable });
}

/// <summary>∫ P_n(x) dx = (P_{n+1}(x) - P_{n-1}(x)) / (2n+1)</summary>
public sealed record LegendreRule : OrthogonalPolyRule
{
    public override Expression Eval()
    {
        var n = N;
        var p1 = PolyEval(FunctionNType.LegendreP, n + One);
        var pm1 = PolyEval(FunctionNType.LegendreP, n - One);
        return (p1 - pm1) / (Two * n + One);
    }
}

/// <summary>∫ T_n(x) dx = ½(T_{n+1}/(n+1) - T_{n-1}/(n-1))</summary>
public sealed record ChebyshevTRule : OrthogonalPolyRule
{
    public override Expression Eval()
    {
        var n = N;
        var t1 = PolyEval(FunctionNType.ChebyshevT, n + One) / (n + One);
        var tm1 = PolyEval(FunctionNType.ChebyshevT, n - One) / (n - One);
        return (t1 - tm1) / Two;
    }
}

/// <summary>∫ U_n(x) dx = T_{n+1}(x)/(n+1)</summary>
public sealed record ChebyshevURule : OrthogonalPolyRule
{
    public override Expression Eval()
    {
        var n = N;
        return PolyEval(FunctionNType.ChebyshevT, n + One) / (n + One);
    }
}

/// <summary>∫ H_n(x) dx = H_{n+1}(x) / (2(n+1))</summary>
public sealed record HermiteRule : OrthogonalPolyRule
{
    public override Expression Eval()
    {
        var n = N;
        return PolyEval(FunctionNType.HermiteH, n + One) / (Two * (n + One));
    }
}

/// <summary>∫ L_n(x) dx = L_n(x) - L_{n+1}(x)</summary>
public sealed record LaguerreRule : OrthogonalPolyRule
{
    public override Expression Eval()
    {
        var n = N;
        var ln = PolyEval(FunctionNType.LaguerreL, n);
        var ln1 = PolyEval(FunctionNType.LaguerreL, n + One);
        return ln - ln1;
    }
}

/// <summary>∫ L_n^k(x) dx = -L_{n+1}^(k-1)(x)</summary>
public sealed record AssocLaguerreRule : OrthogonalPolyRule
{
    public required Expression K { get; init; }
    private Expression PolyEvalWithK(FunctionNType type, Expression degree) =>
        new Expression.FunctionN(type, new[] { degree, K - One, Variable });
    public override Expression Eval()
    {
        var n = N;
        return -PolyEvalWithK(FunctionNType.AssocLaguerreL, n + One);
    }
}

/// <summary>∫ C_n^(a)(x) dx = C_{n+1}^(a-1)(x) / (2(a-1))</summary>
public sealed record GegenbauerRule : OrthogonalPolyRule
{
    public required Expression A { get; init; }
    public override Expression Eval()
    {
        var n = N;
        return PolyEval(FunctionNType.GegenbauerC, n + One) / (Two * (A - One));
    }
}

/// <summary>∫ P_n^(a,b)(x) dx = 2·P_{n+1}^(a-1,b-1)(x) / (n+a+b)</summary>
public sealed record JacobiRule : OrthogonalPolyRule
{
    public required Expression A { get; init; }
    public required Expression B { get; init; }
    public override Expression Eval()
    {
        var n = N;
        return Two * PolyEval(FunctionNType.JacobiP, n + One) / (n + A + B);
    }
}

// ── Special function rules matching SymPy ──────────────────────

/// <summary>
/// ∫ exp(-(ax+b)²)·erf(y·(ax+b)) dx  =  -2√π/a · T(√2·(ax+b), y)
/// where T(u, y) is the Owens T function.
/// </summary>
public sealed record OwensTRule : AtomicRule
{
    public required Expression A { get; init; }
    public required Expression B { get; init; }
    public required Expression Y { get; init; }
    public override Expression Eval()
    {
        var v = Variable;
        var a = A;
        // T(√2·(a·x+b), y)
        var tArg = Sqrt(Two) * (a * v + B);
        var t = new Expression.FunctionN(FunctionNType.OwensT, new[] { tArg, Y });
        return Negate(Two * Sqrt(Pi) / a * t);
    }
}

/// <summary>
/// ∫ polylog(b, a·x) / x dx  =  polylog(b+1, a·x)
/// </summary>
public sealed record PolylogRule : AtomicRule
{
    public required Expression A { get; init; }
    public required Expression B { get; init; }
    public override Expression Eval()
    {
        var x = Variable;
        var inner = A * x;
        return new Expression.FunctionN(FunctionNType.Polylog, new[] { B + One, inner });
    }
}

/// <summary>
/// ∫ x^e · exp(a·x) dx  =  x^e · (-a·x)^(-e) · Γ(e+1, -a·x) / a
/// where e is a non-negative integer.
/// </summary>
public sealed record UpperGammaRule : AtomicRule
{
    public required Expression A { get; init; }
    public required Expression E { get; init; }
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
/// ∫ 1/√(a - d·sin²(x)) dx  =  F(x, d/a) / √a
/// where F(φ,m) is the elliptic integral of the first kind.
/// </summary>
public sealed record EllipticFRule : AtomicRule
{
    public required Expression A { get; init; }
    public required Expression D { get; init; }
    public override Expression Eval()
    {
        var x = Variable;
        var m = D / A;
        var f = new Expression.FunctionN(FunctionNType.EllipticF, new[] { x, m });
        return f / Sqrt(A);
    }
}

/// <summary>
/// ∫ √(a - d·sin²(x)) dx  =  E(x, d/a) · √a
/// where E(φ,m) is the elliptic integral of the second kind.
/// </summary>
public sealed record EllipticERule : AtomicRule
{
    public required Expression A { get; init; }
    public required Expression D { get; init; }
    public override Expression Eval()
    {
        var x = Variable;
        var m = D / A;
        var e = new Expression.FunctionN(FunctionNType.EllipticE, new[] { x, m });
        return e * Sqrt(A);
    }
}
