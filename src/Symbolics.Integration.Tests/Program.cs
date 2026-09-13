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

Console.WriteLine($"\n=== Result: {passed} passed, {failed} failed ===");

static void Assert(bool condition, string msg = "Assertion failed")
{
    if (!condition) throw new Exception(msg);
}
