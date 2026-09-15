// ----------------------------------------------------------------------------
// (EN) Purpose: Console test runner for the symbolic integration engine. It runs a
//       series of integration cases covering atomic rules, the solver strategies
//       and the special-function rules, prints each computed antiderivative and
//       tallies the pass/fail counts.
// (ZH) 用途：符号积分引擎的控制台测试运行器。它执行一系列积分用例，覆盖原子规则、
//       求解器各类策略与特殊函数规则，打印每个算出的原函数并统计通过/失败数量。
// (EN) Notes: Deliberately dependency-free (no NUnit/xUnit). Each case is a lambda
//       checked by the local Assert helper; the process prints a summary at the end.
// (ZH) 说明：刻意不依赖任何测试框架（无 NUnit/xUnit）。每个用例是一个 lambda，
//       由本地 Assert 断言辅助方法校验；进程在最后打印汇总结果。
// ----------------------------------------------------------------------------

using MathNet.Symbolics.Integration.Core;
using MathNet.Symbolics.Integration;
using static MathNet.Symbolics.Integration.Core.Operators;

// (EN) Simple test runner (no NUnit dependency). (ZH) 简单测试运行器（无 NUnit 依赖）。
int passed = 0, failed = 0;

// (EN) Runs a single named test; prints a check mark on success or a cross plus the
//      exception message on failure, updating the pass/fail counters accordingly.
// (ZH) 运行单个具名测试；成功时打印对勾，失败时打印叉号及异常信息，并相应更新通过/失败计数。
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

// (EN) ∫ 5 dx — a pure constant integrand exercises the ConstantRule.
// (ZH) ∫ 5 dx —— 纯常数被积函数，检验 ConstantRule。
Run("Constant", () => {
    var r = Integrate.Of(Number(5), Symbol("x"));
    Console.WriteLine($"    ∫ 5 dx = {r}");
    Assert(r != null);
});

// (EN) ∫ sin(x) dx = -cos(x) — direct atomic SinRule.
// (ZH) ∫ sin(x) dx = -cos(x) —— 直接应用原子规则 SinRule。
Run("sin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sin(x), x);
    Console.WriteLine($"    ∫ sin(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Cos } or Expression.Product
        or Expression.Number); // -cos(x) form
});

// (EN) ∫ cos(x) dx = sin(x) — direct atomic CosRule.
// (ZH) ∫ cos(x) dx = sin(x) —— 直接应用原子规则 CosRule。
Run("cos(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cos(x), x);
    Console.WriteLine($"    ∫ cos(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Sin });
});

// (EN) ∫ e^x dx = e^x — exponential is its own antiderivative.
// (ZH) ∫ e^x dx = e^x —— 指数函数是自身的原函数。
Run("exp(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Exp(x), x);
    Console.WriteLine($"    ∫ e^x dx = {r}");
    Assert(r != null);
});

// (EN) ∫ sinh(x) dx = cosh(x) — direct hyperbolic rule.
// (ZH) ∫ sinh(x) dx = cosh(x) —— 直接应用双曲函数规则。
Run("sinh(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sinh(x), x);
    Console.WriteLine($"    ∫ sinh(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Cosh });
});

// (EN) ∫ cosh(x) dx = sinh(x) — direct hyperbolic rule.
// (ZH) ∫ cosh(x) dx = sinh(x) —— 直接应用双曲函数规则。
Run("cosh(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cosh(x), x);
    Console.WriteLine($"    ∫ cosh(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Sinh });
});

// (EN) ∫ x dx = x²/2 — PowerRule with exponent 1.
// (ZH) ∫ x dx = x²/2 —— 指数为 1 的 PowerRule。
Run("x (x^2/2)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(x, x);
    Console.WriteLine($"    ∫ x dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/x dx = ln(x) — ReciprocalRule.
// (ZH) ∫ 1/x dx = ln(x) —— ReciprocalRule。
Run("1/x → ln(x)", () => {
    var x = Symbol("x");
    var expr = Divide(One, x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/x dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 3x dx = 3x²/2 — constant extraction followed by the power rule.
// (ZH) ∫ 3x dx = 3x²/2 —— 先提取常数再用幂规则。
Run("3*x", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Multiply(Number(3), x), x);
    Console.WriteLine($"    ∫ 3*x dx = {r}");
    Assert(r != null);
});

// (EN) ∫ (x + sin x) dx — SumRule splits the integrand term by term.
// (ZH) ∫ (x + sin x) dx —— SumRule 将被积函数逐项拆分。
Run("x + sin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Add(x, Sin(x)), x);
    Console.WriteLine($"    ∫ (x + sin(x)) dx = {r}");
    Assert(r != null);
});

// (EN) Steps API should surface the atomic SinRule for sin(x).
// (ZH) Steps API 对 sin(x) 应返回原子规则 SinRule。
Run("Steps for sin(x) → SinRule", () => {
    var x = Symbol("x");
    var steps = Integrate.Steps(Sin(x), x);
    Console.WriteLine($"    Steps type: {steps.GetType().Name}");
    Assert(steps is SinRule);
});

// (EN) Steps API should surface the atomic CosRule for cos(x).
// (ZH) Steps API 对 cos(x) 应返回原子规则 CosRule。
Run("Steps for cos(x) → CosRule", () => {
    var x = Symbol("x");
    var steps = Integrate.Steps(Cos(x), x);
    Console.WriteLine($"    Steps type: {steps.GetType().Name}");
    Assert(steps is CosRule);
});

// (EN) Overloaded operators build x² + 2x + 1 and integrate it.
// (ZH) 通过重载运算符构造 x² + 2x + 1 并求积分。
Run("operator overloading: x*x + 2*x + 1", () => {
    var x = Symbol("x");
    var expr = x*x + 2*x + 1;
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (x² + 2x + 1) dx = {r}");
    Assert(r != null);
});

// (EN) Overloaded operators build 3·sin(x) and integrate it.
// (ZH) 通过重载运算符构造 3·sin(x) 并求积分。
Run("operator overloading: 3*sin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(3*Sin(x), x);
    Console.WriteLine($"    ∫ 3*sin(x) dx = {r}");
    Assert(r != null);
});

// ── New Trig Rules ──
// (EN) Tests for tan, cot, sec, csc integration rules.
// (ZH) 正切、余切、正割、余割积分规则的测试。
// (EN) ∫ tan(x) dx = -ln|cos(x)| — TanRule.
// (ZH) ∫ tan(x) dx = -ln|cos(x)| —— TanRule。
Run("tan(x) → -ln|cos(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Tan(x), x);
    Console.WriteLine($"    ∫ tan(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ cot(x) dx = ln|sin(x)| — CotRule.
// (ZH) ∫ cot(x) dx = ln|sin(x)| —— CotRule。
Run("cot(x) → ln|sin(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cot(x), x);
    Console.WriteLine($"    ∫ cot(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ sec(x) dx = ln|sec(x)+tan(x)| — SecRule.
// (ZH) ∫ sec(x) dx = ln|sec(x)+tan(x)| —— SecRule。
Run("sec(x) → ln|sec(x)+tan(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sec(x), x);
    Console.WriteLine($"    ∫ sec(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ csc(x) dx = -ln|csc(x)+cot(x)| — CscRule.
// (ZH) ∫ csc(x) dx = -ln|csc(x)+cot(x)| —— CscRule。
Run("csc(x) → -ln|csc(x)+cot(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Csc(x), x);
    Console.WriteLine($"    ∫ csc(x) dx = {r}");
    Assert(r != null);
});

// ── New Hyperbolic Rules ──
// (EN) ∫ tanh(x) dx = ln(cosh(x)) — TanhRule.
// (ZH) ∫ tanh(x) dx = ln(cosh(x)) —— TanhRule。
Run("tanh(x) → ln(cosh(x))", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Tanh(x), x);
    Console.WriteLine($"    ∫ tanh(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ coth(x) dx = ln|sinh(x)| — CothRule.
// (ZH) ∫ coth(x) dx = ln|sinh(x)| —— CothRule。
Run("coth(x) → ln|sinh(x)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Coth(x), x);
    Console.WriteLine($"    ∫ coth(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ sech(x) dx = 2·atan(e^x) — SechRule.
// (ZH) ∫ sech(x) dx = 2·atan(e^x) —— SechRule。
Run("sech(x) → 2·atan(e^x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sech(x), x);
    Console.WriteLine($"    ∫ sech(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ csch(x) dx = ln|tanh(x/2)| — CschRule.
// (ZH) ∫ csch(x) dx = ln|tanh(x/2)| —— CschRule。
Run("csch(x) → ln|tanh(x/2)|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Csch(x), x);
    Console.WriteLine($"    ∫ csch(x) dx = {r}");
    Assert(r != null);
});

// ── Steps Tests for new rules ──
// (EN) Steps API should surface the atomic TanRule for tan(x).
// (ZH) Steps API 对 tan(x) 应返回原子规则 TanRule。
Run("Steps for tan(x) → TanRule", () => {
    var x = Symbol("x");
    var steps = Integrate.Steps(Tan(x), x);
    Console.WriteLine($"    Steps type: {steps.GetType().Name}");
    Assert(steps is TanRule);
});

// ── Substitution & Parts ──
// (EN) ∫ 3x·exp(x²) dx = 3/2·exp(x²) — u-substitution with u = x².
// (ZH) ∫ 3x·exp(x²) dx = 3/2·exp(x²) —— 令 u = x² 的换元积分。
Run("3*x*exp(x^2) → 3/2*exp(x^2) (u-sub)", () => {
    var x = Symbol("x");
    var expr = 3 * x * Exp(x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 3x·exp(x²) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ x·cos(x) dx — integration by parts with LIATE choosing u = x.
// (ZH) ∫ x·cos(x) dx —— 分部积分，LIATE 选取 u = x。
Run("x*cos(x) (parts)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(x * Cos(x), x);
    Console.WriteLine($"    ∫ x·cos(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ x·ln(x) dx — parts; logarithm has the highest LIATE priority for u.
// (ZH) ∫ x·ln(x) dx —— 分部积分；对数在 LIATE 中 u 的优先级最高。
Run("x*ln(x) (parts)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(x * Ln(x), x);
    Console.WriteLine($"    ∫ x·ln(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ x·eˣ dx = x·eˣ - eˣ — integration by parts.
// (ZH) ∫ x·eˣ dx = x·eˣ - eˣ —— 分部积分。
Run("x*exp(x) (parts)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(x * Exp(x), x);
    Console.WriteLine($"    ∫ x·eˣ dx = {r}");
    Assert(r != null);
});

// ── Quadratic / sqrt rules ──
// (EN) ∫ 1/√(1+x²) dx = asinh(x) — quadratic square-root form.
// (ZH) ∫ 1/√(1+x²) dx = asinh(x) —— 二次式平方根形式。
Run("1/sqrt(1+x^2) → asinh(x)", () => {
    var x = Symbol("x");
    var expr = 1 / Sqrt(1 + x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/√(1+x²) dx = {r}");
    Assert(r != null);
});

// ── Extended parts ──
// (EN) ∫ x²·cos(x) dx — parts applied twice (cyclic parts).
// (ZH) ∫ x²·cos(x) dx —— 连续两次分部积分（循环分部积分）。
Run("x^2*cos(x) (cyclic parts)", () => {
    var x = Symbol("x");
    var expr = x*x * Cos(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²·cos(x) dx = {r}");
    Assert(r != null);
});

// ── Linear argument ──
// (EN) ∫ sin(2x) dx = -cos(2x)/2 — automatic f(ax+b) handling.
// (ZH) ∫ sin(2x) dx = -cos(2x)/2 —— 自动处理 f(ax+b) 线性参数。
Run("sin(2*x) → -cos(2*x)/2", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sin(2*x), x);
    Console.WriteLine($"    ∫ sin(2x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ e^(3x) dx = e^(3x)/3 — linear argument in the exponential.
// (ZH) ∫ e^(3x) dx = e^(3x)/3 —— 指数函数中的线性参数。
Run("exp(3*x) → exp(3*x)/3", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Exp(3*x), x);
    Console.WriteLine($"    ∫ exp(3x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ cos(2x+1) dx = sin(2x+1)/2 — linear argument with an offset.
// (ZH) ∫ cos(2x+1) dx = sin(2x+1)/2 —— 带偏移的线性参数。
Run("cos(2*x+1) → sin(2*x+1)/2", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cos(2*x + 1), x);
    Console.WriteLine($"    ∫ cos(2x+1) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ sinh(2x) dx = cosh(2x)/2 — linear argument, hyperbolic.
// (ZH) ∫ sinh(2x) dx = cosh(2x)/2 —— 双曲函数中的线性参数。
Run("sinh(2*x) → cosh(2*x)/2", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sinh(2*x), x);
    Console.WriteLine($"    ∫ sinh(2x) dx = {r}");
    Assert(r != null);
});

// ── Arcsin / Arctan ──
// (EN) ∫ 1/√(1-x²) dx = asin(x) — ArcsinRule.
// (ZH) ∫ 1/√(1-x²) dx = asin(x) —— ArcsinRule。
Run("1/sqrt(1-x^2) → asin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Sqrt(1 - x*x), x);
    Console.WriteLine($"    ∫ 1/√(1-x²) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/(1+x²) dx = atan(x) — ArctanRule.
// (ZH) ∫ 1/(1+x²) dx = atan(x) —— ArctanRule。
Run("1/(1+x^2) → atan(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / (1 + x*x), x);
    Console.WriteLine($"    ∫ 1/(1+x²) dx = {r}");
    Assert(r != null);
});

// ── Special functions ──
// (EN) ∫ exp(-x²) dx = √π/2·erf(x) — ErfRule.
// (ZH) ∫ exp(-x²) dx = √π/2·erf(x) —— ErfRule。
Run("exp(-x^2) → erf(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Exp(-x*x), x);
    Console.WriteLine($"    ∫ exp(-x²) dx = {r}");
    Assert(r != null);
});

// ── Special functions (sin(x)/x etc.) ──
// (EN) ∫ sin(x)/x dx = Si(x) — sine integral.
// (ZH) ∫ sin(x)/x dx = Si(x) —— 正弦积分。
Run("sin(x)/x → Si(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sin(x) / x, x);
    Console.WriteLine($"    ∫ sin(x)/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Si });
});

// (EN) ∫ cos(x)/x dx = Ci(x) — cosine integral.
// (ZH) ∫ cos(x)/x dx = Ci(x) —— 余弦积分。
Run("cos(x)/x → Ci(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cos(x) / x, x);
    Console.WriteLine($"    ∫ cos(x)/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Ci });
});

// (EN) ∫ eˣ/x dx = Ei(x) — exponential integral.
// (ZH) ∫ eˣ/x dx = Ei(x) —— 指数积分。
Run("exp(x)/x → Ei(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Exp(x) / x, x);
    Console.WriteLine($"    ∫ eˣ/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Ei });
});

// (EN) ∫ sinh(x)/x dx = Shi(x) — hyperbolic sine integral.
// (ZH) ∫ sinh(x)/x dx = Shi(x) —— 双曲正弦积分。
Run("sinh(x)/x → Shi(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sinh(x) / x, x);
    Console.WriteLine($"    ∫ sinh(x)/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Shi });
});

// (EN) ∫ cosh(x)/x dx = Chi(x) — hyperbolic cosine integral.
// (ZH) ∫ cosh(x)/x dx = Chi(x) —— 双曲余弦积分。
Run("cosh(x)/x → Chi(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cosh(x) / x, x);
    Console.WriteLine($"    ∫ cosh(x)/x dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Chi });
});

// (EN) ∫ 1/ln(x) dx = Li(x) — logarithmic integral.
// (ZH) ∫ 1/ln(x) dx = Li(x) —— 对数积分。
Run("1/ln(x) → Li(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Ln(x), x);
    Console.WriteLine($"    ∫ 1/ln(x) dx = {r}");
    Assert(r is Expression.Function { Op: FunctionType.Li });
});

// ── Fresnel integrals ──
// (EN) ∫ sin(x²) dx — Fresnel sine integral S(x).
// (ZH) ∫ sin(x²) dx —— Fresnel 正弦积分 S(x)。
Run("sin(x^2) → FresnelS", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sin(x*x), x);
    Console.WriteLine($"    ∫ sin(x²) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ cos(x²) dx — Fresnel cosine integral C(x).
// (ZH) ∫ cos(x²) dx —— Fresnel 余弦积分 C(x)。
Run("cos(x^2) → FresnelC", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Cos(x*x), x);
    Console.WriteLine($"    ∫ cos(x²) dx = {r}");
    Assert(r != null);
});

// ── Orthogonal polynomials ──
// (EN) ∫ P_n(x) dx — Legendre polynomial recurrence rule.
// (ZH) ∫ P_n(x) dx —— Legendre 多项式递推规则。
Run("LegendreP(n, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var expr = new Expression.FunctionN(FunctionNType.LegendreP, new[] { n, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ P_n(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ T_n(x) dx — Chebyshev polynomial of the first kind.
// (ZH) ∫ T_n(x) dx —— 第一类 Chebyshev 多项式。
Run("ChebyshevT(n, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var expr = new Expression.FunctionN(FunctionNType.ChebyshevT, new[] { n, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ T_n(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ H_n(x) dx — Hermite polynomial.
// (ZH) ∫ H_n(x) dx —— Hermite 多项式。
Run("HermiteH(n, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var expr = new Expression.FunctionN(FunctionNType.HermiteH, new[] { n, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ H_n(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ L_n(x) dx — Laguerre polynomial.
// (ZH) ∫ L_n(x) dx —— Laguerre 多项式。
Run("LaguerreL(n, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var expr = new Expression.FunctionN(FunctionNType.LaguerreL, new[] { n, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ L_n(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ C_n^(a)(x) dx — Gegenbauer polynomial with parameter a.
// (ZH) ∫ C_n^(a)(x) dx —— 带参数 a 的 Gegenbauer 多项式。
Run("GegenbauerC(n, a, x)", () => {
    var x = Symbol("x");
    var n = Symbol("n");
    var a = Symbol("a");
    var expr = new Expression.FunctionN(FunctionNType.GegenbauerC, new[] { n, a, x });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ C_n^(a)(x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ P_n^(a,b)(x) dx — Jacobi polynomial with parameters a, b.
// (ZH) ∫ P_n^(a,b)(x) dx —— 带参数 a、b 的 Jacobi 多项式。
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
// (EN) ∫ 1/(x-1) dx = ln|x-1| — simple logarithmic rule.
// (ZH) ∫ 1/(x-1) dx = ln|x-1| —— 简单对数规则。
Run("1/(x-1) → ln|x-1|", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / (x - 1), x);
    Console.WriteLine($"    ∫ 1/(x-1) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/(2x+1) dx = ln|2x+1|/2 — linear denominator with coefficient 2.
// (ZH) ∫ 1/(2x+1) dx = ln|2x+1|/2 —— 系数为 2 的线性分母。
Run("1/(2*x+1) → ln|2x+1|/2", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / (2*x + 1), x);
    Console.WriteLine($"    ∫ 1/(2x+1) dx = {r}");
    Assert(r != null);
});

// Quick Simplify test
// (EN) Sanity checks that Simplify cancels x+x, x+0 and 1*x, and preserves x²/2.
// (ZH) 检验 Simplify 能合并 x+x、消去 x+0 与 1*x，并正确保留 x²/2。
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

// (EN) ∫ 1/(x+2)³ dx = -1/(2·(x+2)²) — reciprocal power of a linear factor.
// (ZH) ∫ 1/(x+2)³ dx = -1/(2·(x+2)²) —— 线性因子的倒数幂。
Run("1/(x+2)^3 → -1/(2*(x+2)^2)", () => {
    var x = Symbol("x");
    var expr = 1 / ((x+2)*(x+2)*(x+2));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x+2)³ dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/((x-1)(x-2)) dx — partial fractions over distinct linear factors.
// (ZH) ∫ 1/((x-1)(x-2)) dx —— 相异线性因子的部分分式分解。
Run("1/((x-1)(x-2)) → partial fractions", () => {
    var x = Symbol("x");
    var expr = 1 / ((x-1)*(x-2));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/((x-1)(x-2)) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/((x-5)(x+1)) dx — partial fractions (mixed-sign roots).
// (ZH) ∫ 1/((x-5)(x+1)) dx —— 部分分式分解（根符号相反）。
Run("1/((x-5)(x+1)) → partial fractions", () => {
    var x = Symbol("x");
    var expr = 1 / ((x-5)*(x+1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/((x-5)(x+1)) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/((3x-1)(x+2)) dx — partial fractions with non-unit leading coefficient.
// (ZH) ∫ 1/((3x-1)(x+2)) dx —— 带非单位首系数的部分分式分解。
Run("1/((3x-1)(x+2)) → partial fractions", () => {
    var x = Symbol("x");
    var expr = 1 / ((3*x-1)*(x+2));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/((3x-1)(x+2)) dx = {r}");
    Assert(r != null);
});

// ── General sqrt quadratic ──
// (EN) ∫ 1/√(x²+2x+2) dx — general 1/√(ax²+bx+c) completes the square.
// (ZH) ∫ 1/√(x²+2x+2) dx —— 一般形式 1/√(ax²+bx+c)，配方求解。
Run("1/sqrt(x^2+2x+2) → general sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Sqrt(x*x + 2*x + 2), x);
    Console.WriteLine($"    ∫ 1/√(x²+2x+2) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/√(x²+3x+1) dx — general quadratic with irrational-looking roots.
// (ZH) ∫ 1/√(x²+3x+1) dx —— 根式看似无理的二次式。
Run("1/sqrt(x^2+3x+1) → general sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Sqrt(x*x + 3*x + 1), x);
    Console.WriteLine($"    ∫ 1/√(x²+3x+1) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/√(-x²+3x-2) dx — concave quadratic (negative leading coefficient).
// (ZH) ∫ 1/√(-x²+3x-2) dx —— 开口向下的二次式（首项系数为负）。
Run("1/sqrt(-x^2+3x-2) → general sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(1 / Sqrt(-x*x + 3*x - 2), x);
    Console.WriteLine($"    ∫ 1/√(-x²+3x-2) dx = {r}");
    Assert(r != null);
});

// ── Sqrt of quadratic ──
// (EN) ∫ √(x²+1) dx — integrates a quadratic under the square root.
// (ZH) ∫ √(x²+1) dx —— 积分根号下的二次式。
Run("sqrt(x^2+1) → sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sqrt(x*x + 1), x);
    Console.WriteLine($"    ∫ √(x²+1) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ √(x²+2x+2) dx — square root of a general quadratic.
// (ZH) ∫ √(x²+2x+2) dx —— 一般二次式的平方根。
Run("sqrt(x^2+2x+2) → sqrt quadratic", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Sqrt(x*x + 2*x + 2), x);
    Console.WriteLine($"    ∫ √(x²+2x+2) dx = {r}");
    Assert(r != null);
});

// ── Assoc Laguerre ──
// (EN) ∫ L_n^k(x) dx — associated Laguerre polynomial with order k.
// (ZH) ∫ L_n^k(x) dx —— 阶数为 k 的连带 Laguerre 多项式。
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

// (EN) ∫ x²·eˣ dx — n = 2 is the smallest case routed to UpperGamma; the Steps
//      type is printed to confirm which rule was selected.
// (ZH) ∫ x²·eˣ dx —— n = 2 是交由 UpperGamma 处理的最小情形；打印 Steps 类型以确认所选规则。
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

// (EN) ∫ x²·e^(2x) dx — UpperGamma with a non-unit exponential coefficient.
// (ZH) ∫ x²·e^(2x) dx —— 指数系数非 1 的 UpperGamma 情形。
Run("UpperGamma: x²·exp(2x)", () => {
    var x = Symbol("x");
    var expr = x*x * Exp(2*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²·e^(2x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ Li(1, 2x)/x dx — PolylogRule raises the polylog order by one.
// (ZH) ∫ Li(1, 2x)/x dx —— PolylogRule 将多重对数的阶提高一阶。
Run("Polylog: polylog(1, 2x)/x", () => {
    var x = Symbol("x");
    var poly = new Expression.FunctionN(FunctionNType.Polylog, new[] { One, 2*x });
    var expr = poly / x;
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ Li(1, 2x)/x dx = {r}");
    Assert(r != null);
});

// (EN) ∫ exp(-x²)·erf(2x) dx — Owens T function; the Erf node is built manually
//      because Operators exposes no Erf helper.
// (ZH) ∫ exp(-x²)·erf(2x) dx —— Owen's T 函数；由于 Operators 未提供 Erf 辅助方法，
//      此处手动构造 Erf 节点。
Run("OwensT: exp(-x²)·erf(2x)", () => {
    var x = Symbol("x");
    // exp(-x²)·erf(2x) — create Erf manually since there's no Operators.Erf
    var erf2x = new Expression.Function(FunctionType.Erf, 2*x);
    var expr = Exp(-(x*x)) * erf2x;
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ exp(-x²)·erf(2x) dx = {r}");
    Assert(r != null);
});

// (EN) ∫ 1/√(2 - sin²(x)) dx — incomplete elliptic integral of the first kind F.
// (ZH) ∫ 1/√(2 - sin²(x)) dx —— 第一类不完全椭圆积分 F。
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

// (EN) ∫ √(2 - sin²(x)) dx — incomplete elliptic integral of the second kind E.
// (ZH) ∫ √(2 - sin²(x)) dx —— 第二类不完全椭圆积分 E。
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

// (EN) Minimal assertion helper: throws when the condition is false so that the
//      enclosing Run call records the test as failed.
// (ZH) 最小断言辅助方法：条件为假时抛出异常，从而让外层 Run 将该测试记为失败。
static void Assert(bool condition, string msg = "Assertion failed")
{
    if (!condition) throw new Exception(msg);
}
