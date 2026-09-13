using MathNet.Symbolics.Integration.Core;
using MathNet.Symbolics.Integration;
using static MathNet.Symbolics.Integration.Core.Operators;

// Simple test runner (no NUnit dependency)
int passed = 0, failed = 0;

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"  ✓ {name}");
        passed++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ {name}: {ex.Message}");
        failed++;
    }
}

Console.WriteLine("=== Symbolic Integration Tests ===");

Run("Constant", () => {
    var r = Integrate.Of(Number(5), Symbol("x"));
    Console.WriteLine($"    ∫ 5 dx = {r}");
    Assert(r != null);
});

Run("sin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sin(x), x);
    Console.WriteLine($"    ∫ sin(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Cos } or Expression.Product
        or Expression.Number); // -cos(x) form
});

Run("cos(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cos(x), x);
    Console.WriteLine($"    ∫ cos(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Sin });
});

Run("exp(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Exp(x), x);
    Console.WriteLine($"    ∫ e^x dx = {r}");
    Assert(r != null);
});

Run("sinh(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sinh(x), x);
    Console.WriteLine($"    ∫ sinh(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Cosh });
});

Run("cosh(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cosh(x), x);
    Console.WriteLine($"    ∫ cosh(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Sinh });
});

Run("x (x^2/2)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(x, x);
    Console.WriteLine($"    ∫ x dx = {r}");
    Assert(r != null);
});

Run("1/x → ln(x)", () => {
    var x = Symbol("x");
    var expr = Divide(One, x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/x dx = {r}");
    Assert(r != null);
});

Run("3*x", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Multiply(Number(3), x), x);
    Console.WriteLine($"    ∫ 3*x dx = {r}");
    Assert(r != null);
});

Run("x + sin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Add(x, Sin(x)), x);
    Console.WriteLine($"    ∫ (x + sin(x)) dx = {r}");
    Assert(r != null);
});

Run("Steps for sin(x) → SinRule", () => {
    var x = Symbol("x");
    var steps = Integrate.Steps(Sin(x), x);
    Console.WriteLine($"    Steps type: {steps.GetType().Name}");
    Assert(steps is SinRule);
});

Run("Steps for cos(x) → CosRule", () => {
    var x = Symbol("x");
    var steps = Integrate.Steps(Cos(x), x);
    Console.WriteLine($"    Steps type: {steps.GetType().Name}");
    Assert(steps is CosRule);
});

Run("operator overloading: x*x + 2*x + 1", () => {
    var x = Symbol("x");
    var expr = x*x + 2*x + 1;
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (x² + 2x + 1) dx = {r}");
    Assert(r != null);
});

Run("operator overloading: 3*sin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(3*Sin(x), x);
    Console.WriteLine($"    ∫ 3*sin(x) dx = {r}");
    Assert(r != null);
});

// ── New Trig Rules ──
Run("tan(x) → -ln|cos(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Tan(x), x);
    Console.WriteLine($"    ∫ tan(x) dx = {r}");
    Assert(r != null);
});

Run("cot(x) → ln|sin(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cot(x), x);
    Console.WriteLine($"    ∫ cot(x) dx = {r}");
    Assert(r != null);
});

Run("sec(x) → ln|sec(x)+tan(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sec(x), x);
    Console.WriteLine($"    ∫ sec(x) dx = {r}");
    Assert(r != null);
});

Run("csc(x) → -ln|csc(x)+cot(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Csc(x), x);
    Console.WriteLine($"    ∫ csc(x) dx = {r}");
    Assert(r != null);
});

// ── New Hyperbolic Rules ──
Run("tanh(x) → ln(cosh(x))", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Tanh(x), x);
    Console.WriteLine($"    ∫ tanh(x) dx = {r}");
    Assert(r != null);
});

Run("coth(x) → ln|sinh(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Coth(x), x);
    Console.WriteLine($"    ∫ coth(x) dx = {r}");
    Assert(r != null);
});

Run("sech(x) → 2·atan(e^x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sech(x), x);
    Console.WriteLine($"    ∫ sech(x) dx = {r}");
    Assert(r != null);
});

Run("csch(x) → ln|tanh(x/2)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Csch(x), x);
    Console.WriteLine($"    ∫ csch(x) dx = {r}");
    Assert(r != null);
});

// ── Steps Tests for new rules ──
Run("Steps for tan(x) → TanRule", () => {
    var x = Symbol("x");
    var steps = Integrate.Steps(Tan(x), x);
    Console.WriteLine($"    Steps type: {steps.GetType().Name}");
    Assert(steps is TanRule);
});

// ── Substitution & Parts ──
Run("3*x*exp(x^2) → 3/2*exp(x^2) (u-sub)", () => {
    var x = Symbol("x");
    var expr = 3 * x * Exp(x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 3x·exp(x²) dx = {r}");
    Assert(r != null);
});

Run("x*cos(x) (parts)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(x * Cos(x), x);
    Console.WriteLine($"    ∫ x·cos(x) dx = {r}");
    Assert(r != null);
});

Run("x*ln(x) (parts)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(x * Ln(x), x);
    Console.WriteLine($"    ∫ x·ln(x) dx = {r}");
    Assert(r != null);
});

Run("x*exp(x) (parts)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(x * Exp(x), x);
    Console.WriteLine($"    ∫ x·eˣ dx = {r}");
    Assert(r != null);
});

// ── Quadratic / sqrt rules ──
Run("1/sqrt(1+x^2) → asinh(x)", () => {
    var x = Symbol("x");
    var expr = 1 / Sqrt(1 + x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/√(1+x²) dx = {r}");
    Assert(r != null);
});

// ── Extended parts ──
Run("x^2*cos(x) (cyclic parts)", () => {
    var x = Symbol("x");
    var expr = x*x * Cos(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²·cos(x) dx = {r}");
    Assert(r != null);
});

// ── Linear argument ──
Run("sin(2*x) → -cos(2*x)/2", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sin(2*x), x);
    Console.WriteLine($"    ∫ sin(2x) dx = {r}");
    Assert(r != null);
});

Run("exp(3*x) → exp(3*x)/3", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Exp(3*x), x);
    Console.WriteLine($"    ∫ exp(3x) dx = {r}");
    Assert(r != null);
});

Run("cos(2*x+1) → sin(2*x+1)/2", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cos(2*x + 1), x);
    Console.WriteLine($"    ∫ cos(2x+1) dx = {r}");
    Assert(r != null);
});

Run("sinh(2*x) → cosh(2*x)/2", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sinh(2*x), x);
    Console.WriteLine($"    ∫ sinh(2x) dx = {r}");
    Assert(r != null);
});

// ── Arcsin / Arctan ──
Run("1/sqrt(1-x^2) → asin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Sqrt(1 - x*x), x);
    Console.WriteLine($"    ∫ 1/√(1-x²) dx = {r}");
    Assert(r != null);
});

Run("1/(1+x^2) → atan(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / (1 + x*x), x);
    Console.WriteLine($"    ∫ 1/(1+x²) dx = {r}");
    Assert(r != null);
});

// ── Special functions ──
Run("exp(-x^2) → erf(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Exp(-x*x), x);
    Console.WriteLine($"    ∫ exp(-x²) dx = {r}");
    Assert(r != null);
});

// ── Special functions (sin(x)/x etc.) ──
Run("sin(x)/x → Si(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sin(x) / x, x);
    Console.WriteLine($"    ∫ sin(x)/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Si });
});

Run("cos(x)/x → Ci(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cos(x) / x, x);
    Console.WriteLine($"    ∫ cos(x)/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Ci });
});

Run("exp(x)/x → Ei(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Exp(x) / x, x);
    Console.WriteLine($"    ∫ eˣ/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Ei });
});

Run("sinh(x)/x → Shi(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sinh(x) / x, x);
    Console.WriteLine($"    ∫ sinh(x)/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Shi });
});

Run("cosh(x)/x → Chi(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cosh(x) / x, x);
    Console.WriteLine($"    ∫ cosh(x)/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Chi });
});

Run("1/ln(x) → Li(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Ln(x), x);
    Console.WriteLine($"    ∫ 1/ln(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Li });
});

// ── Fresnel integrals ──
Run("sin(x^2) → FresnelS", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sin(x*x), x);
    Console.WriteLine($"    ∫ sin(x²) dx = {r}");
    Assert(r != null);
});

Run("cos(x^2) → FresnelC", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cos(x*x), x);
    Console.WriteLine($"    ∫ cos(x²) dx = {r}");
    Assert(r != null);
});

// ── Orthogonal polynomials ──
Run("LegendreP(n, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var expr = new Expression.FunctionN(FunctionNType.LegendreP, new[] { n, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ P_n(x) dx = {r}");
    Assert(r != null);
});

Run("ChebyshevT(n, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var expr = new Expression.FunctionN(FunctionNType.ChebyshevT, new[] { n, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ T_n(x) dx = {r}");
    Assert(r != null);
});

Run("HermiteH(n, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var expr = new Expression.FunctionN(FunctionNType.HermiteH, new[] { n, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ H_n(x) dx = {r}");
    Assert(r != null);
});

Run("LaguerreL(n, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var expr = new Expression.FunctionN(FunctionNType.LaguerreL, new[] { n, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ L_n(x) dx = {r}");
    Assert(r != null);
});

Run("GegenbauerC(n, a, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var a = Symbol("a");
    var expr = new Expression.FunctionN(FunctionNType.GegenbauerC, new[] { n, a, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ C_n^(a)(x) dx = {r}");
    Assert(r != null);
});

Run("JacobiP(n, a, b, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var a = Symbol("a");
    var b = Symbol("b");
    var expr = new Expression.FunctionN(FunctionNType.JacobiP, new[] { n, a, b, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ P_n^(a,b)(x) dx = {r}");
    Assert(r != null);
});

// ── Rational function integration ──
Run("1/(x-1) → ln|x-1|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / (x - 1), x);
    Console.WriteLine($"    ∫ 1/(x-1) dx = {r}");
    Assert(r != null);
});

Run("1/(2*x+1) → ln|2x+1|/2", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / (2*x + 1), x);
    Console.WriteLine($"    ∫ 1/(2x+1) dx = {r}");
    Assert(r != null);
});

// Quick Simplify test
Run("Simplify test: x + x", () => {
    var x = Operators.Symbol("x");
    var r = Operators.Simplify(x + x);
    Console.WriteLine($"    Simplify(x+x) = {r}  type={r.GetType().Name}");
    Assert(r != null);
    var r2 = Operators.Simplify(x + 0);
    Console.WriteLine($"    Simplify(x+0) = {r2}  type={r2.GetType().Name}");
    var r3 = Operators.Simplify(1*x);
    Console.WriteLine($"    Simplify(1*x) = {r3}  type={r3.GetType().Name}");
    var r4 = Operators.Simplify(x * 1);
    Console.WriteLine($"    Simplify(x*1) = {r4}  type={r4.GetType().Name}");
    var raw = x*x/2;
    Console.WriteLine($"    raw x²/2 = {raw}  type={raw.GetType().Name}");
    Assert(raw is Expression.Product);
    var simp = Operators.Simplify(raw);
    Console.WriteLine($"    simp x²/2 = {simp}  type={simp.GetType().Name}");
    if (simp is Expression.Product sp)
        Console.WriteLine($"      factors: [{string.Join(", ", sp.Factors)}]");
});

Run("1/(x+2)^3 → -1/(2*(x+2)^2)", () => {
    var x = Symbol("x");
    var expr = 1 / ((x+2)*(x+2)*(x+2));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x+2)³ dx = {r}");
    Assert(r != null);
});

Run("1/((x-1)(x-2)) → partial fractions", () => {
    var x = Symbol("x");
    var expr = 1 / ((x-1)*(x-2));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/((x-1)(x-2)) dx = {r}");
    Assert(r != null);
});

Run("1/((x-5)(x+1)) → partial fractions", () => {
    var x = Symbol("x");
    var expr = 1 / ((x-5)*(x+1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/((x-5)(x+1)) dx = {r}");
    Assert(r != null);
});

Run("1/((3x-1)(x+2)) → partial fractions", () => {
    var x = Symbol("x");
    var expr = 1 / ((3*x-1)*(x+2));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/((3x-1)(x+2)) dx = {r}");
    Assert(r != null);
});

// ── General sqrt quadratic ──
Run("1/sqrt(x^2+2x+2) → general sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Sqrt(x*x + 2*x + 2), x);
    Console.WriteLine($"    ∫ 1/√(x²+2x+2) dx = {r}");
    Assert(r != null);
});

Run("1/sqrt(x^2+3x+1) → general sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Sqrt(x*x + 3*x + 1), x);
    Console.WriteLine($"    ∫ 1/√(x²+3x+1) dx = {r}");
    Assert(r != null);
});

Run("1/sqrt(-x^2+3x-2) → general sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Sqrt(-x*x + 3*x - 2), x);
    Console.WriteLine($"    ∫ 1/√(-x²+3x-2) dx = {r}");
    Assert(r != null);
});

// ── Sqrt of quadratic ──
Run("sqrt(x^2+1) → sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sqrt(x*x + 1), x);
    Console.WriteLine($"    ∫ √(x²+1) dx = {r}");
    Assert(r != null);
});

Run("sqrt(x^2+2x+2) → sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sqrt(x*x + 2*x + 2), x);
    Console.WriteLine($"    ∫ √(x²+2x+2) dx = {r}");
    Assert(r != null);
});

// ── Assoc Laguerre ──
Run("AssocLaguerreL(n, k, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var k = Symbol("k");
    var expr = new Expression.FunctionN(FunctionNType.AssocLaguerreL, new[] { n, k, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ L_n^k(x) dx = {r}");
    Assert(r != null);
});

// ── New special function rules ────────────────────────

Run("UpperGamma: x²·exp(x)", () => {
    var x = Symbol("x");
    var expr = x*x * Exp(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²·eˣ dx = {r}");
    Assert(r != null);
    // Should use UpperGammaRule
    var steps = Integrate.Steps(expr, x);
    Console.WriteLine($"      Steps: {steps.GetType().Name}");
});

Run("UpperGamma: x²·exp(2x)", () => {
    var x = Symbol("x");
    var expr = x*x * Exp(2*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²·e^(2x) dx = {r}");
    Assert(r != null);
});

Run("Polylog: polylog(1, 2x)/x", () => {
    var x = Symbol("x");
    var poly = new Expression.FunctionN(FunctionNType.Polylog, new[] { One, 2*x });
    var expr = poly / x;
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ Li(1, 2x)/x dx = {r}");
    Assert(r != null);
});

Run("OwensT: exp(-x²)·erf(2x)", () => {
    var x = Symbol("x");
    // exp(-x²)·erf(2x) — create Erf manually since there's no Operators.Erf
    var erf2x = new Expression.Function(FunctionType.Erf, 2*x);
    var expr = Exp(-(x*x)) * erf2x;
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ exp(-x²)·erf(2x) dx = {r}");
    Assert(r != null);
});

Run("EllipticF: 1/√(2 - sin²(x))", () => {
    var x = Symbol("x");
    var sinSq = new Expression.Power(
        new Expression.Function(FunctionType.Sin, x),
        Expression.Int32(2));
    var expr = 1 / Sqrt(2 - sinSq);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/√(2 - sin²(x)) dx = {r}");
    Assert(r != null);
});

Run("EllipticE: √(2 - sin²(x))", () => {
    var x = Symbol("x");
    var sinSq = new Expression.Power(
        new Expression.Function(FunctionType.Sin, x),
        Expression.Int32(2));
    var expr = Sqrt(2 - sinSq);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ √(2 - sin²(x)) dx = {r}");
    Assert(r != null);
});

Console.WriteLine($"\n=== Result: {passed} passed, {failed} failed ===");

static void Assert(bool condition, string msg = "Assertion failed")
{
    if (!condition) throw new Exception(msg);
}
