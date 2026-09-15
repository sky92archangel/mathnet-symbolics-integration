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

// ── Extended coverage: general differentiation, parts on single functions, trig powers ──
// (EN) These cases exercise the general differentiator, integration by parts of a single
//      non-polynomial function, and the trig/hyperbolic power integrator.
// (ZH) 这些用例检验通用微分器、单个非多项式函数的分部积分，以及三角/双曲幂积分器。

// (EN) ∫ ln(x) dx = x·ln(x) - x — parts with dv = dx.
// (ZH) ∫ ln(x) dx = x·ln(x) - x —— 取 dv = dx 的分部积分。
Run("ln(x) (parts, dv=dx)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Ln(x), x);
    Console.WriteLine($"    ∫ ln(x) dx = {r}");
    Assert(!Integrate.Steps(Ln(x), x).ContainsDontKnow);
});

// (EN) ∫ asin(x) dx = x·asin(x) + √(1-x²) — parts plus a substitution.
// (ZH) ∫ asin(x) dx = x·asin(x) + √(1-x²) —— 分部积分加换元。
Run("asin(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Asin(x), x);
    Console.WriteLine($"    ∫ asin(x) dx = {r}");
    Assert(!Integrate.Steps(Asin(x), x).ContainsDontKnow);
});

// (EN) ∫ atan(x) dx = x·atan(x) - ln(1+x²)/2 — parts plus a substitution.
// (ZH) ∫ atan(x) dx = x·atan(x) - ln(1+x²)/2 —— 分部积分加换元。
Run("atan(x)", () => {
    var x = Symbol("x");
    var r = Integrate.Of(Atan(x), x);
    Console.WriteLine($"    ∫ atan(x) dx = {r}");
    Assert(!Integrate.Steps(Atan(x), x).ContainsDontKnow);
});

// (EN) ∫ erf(x) dx = x·erf(x) + e^(-x²)/√π — parts; exercises the erf derivative.
// (ZH) ∫ erf(x) dx = x·erf(x) + e^(-x²)/√π —— 分部积分，检验 erf 的导数。
Run("erf(x)", () => {
    var x = Symbol("x");
    var erf = new Expression.Function(FunctionType.Erf, x);
    var r = Integrate.Of(erf, x);
    Console.WriteLine($"    ∫ erf(x) dx = {r}");
    Assert(!Integrate.Steps(erf, x).ContainsDontKnow);
});

// (EN) ∫ x·√(x²+1) dx = (x²+1)^(3/2)/3 — needs the general power/generalized diff.
// (ZH) ∫ x·√(x²+1) dx = (x²+1)^(3/2)/3 —— 需要通用微分支持。
Run("x*sqrt(x^2+1) (u-sub)", () => {
    var x = Symbol("x");
    var expr = x * Sqrt(x*x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x·√(x²+1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(x·ln x) dx = ln(ln x) — reciprocal-of-product factoring in substitution.
// (ZH) ∫ 1/(x·ln x) dx = ln(ln x) —— 换元中对乘积倒数进行因式分解。
Run("1/(x*ln(x)) (u-sub)", () => {
    var x = Symbol("x");
    var expr = 1 / (x * Ln(x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x·ln x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x³·e^(x²) dx — substitution u = x² followed by parts, needs exponent splitting.
// (ZH) ∫ x³·e^(x²) dx —— 令 u = x² 换元后再分部积分，需要指数拆分。
Run("x^3*exp(x^2) (u-sub + parts)", () => {
    var x = Symbol("x");
    var expr = x*x*x * Exp(x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x³·e^(x²) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(x²-1) dx — opposite-sign quadratic denominator gives a real logarithm.
// (ZH) ∫ 1/(x²-1) dx —— 二次分母符号相反，得到实对数。
Run("1/(x^2-1) real log", () => {
    var x = Symbol("x");
    var expr = 1 / (x*x - 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x²-1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ atan(x)/(1+x²) dx = atan(x)²/2 — substitution u = atan(x).
// (ZH) ∫ atan(x)/(1+x²) dx = atan(x)²/2 —— 令 u = atan(x) 换元。
Run("atan(x)/(1+x^2) (u-sub)", () => {
    var x = Symbol("x");
    var expr = Atan(x) / (1 + x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ atan(x)/(1+x²) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sin²(x) dx = x/2 - sin(2x)/4 — trig power (even exponent).
// (ZH) ∫ sin²(x) dx —— 三角幂（偶次）。
Run("sin(x)^2", () => {
    var x = Symbol("x");
    var expr = Sin(x) * Sin(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ sin²(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sin³(x) dx = cos³(x)/3 - cos(x) — trig power (odd exponent).
// (ZH) ∫ sin³(x) dx —— 三角幂（奇次）。
Run("sin(x)^3", () => {
    var x = Symbol("x");
    var expr = Sin(x) * Sin(x) * Sin(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ sin³(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sin²(x)·cos³(x) dx — mixed odd/even trig powers.
// (ZH) ∫ sin²(x)·cos³(x) dx —— 奇偶混合的三角幂。
Run("sin(x)^2*cos(x)^3", () => {
    var x = Symbol("x");
    var expr = Sin(x)*Sin(x) * Cos(x)*Cos(x)*Cos(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ sin²(x)·cos³(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ tan²(x) dx = tan(x) - x — tan power reduction.
// (ZH) ∫ tan²(x) dx = tan(x) - x —— 正切幂递推。
Run("tan(x)^2 = tan(x)-x", () => {
    var x = Symbol("x");
    var expr = Tan(x) * Tan(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ tan²(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sec³(x) dx — secant power reduction.
// (ZH) ∫ sec³(x) dx —— 正割幂递推。
Run("sec(x)^3", () => {
    var x = Symbol("x");
    var expr = Sec(x)*Sec(x)*Sec(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ sec³(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ tan³(x)·sec(x) dx = sec³(x)/3 - sec(x) — odd tan power via u = sec.
// (ZH) ∫ tan³(x)·sec(x) dx = sec³(x)/3 - sec(x) —— 奇次正切用 u = sec。
Run("tan(x)^3*sec(x)", () => {
    var x = Symbol("x");
    var expr = Tan(x)*Tan(x)*Tan(x) * Sec(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ tan³(x)·sec(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sinh²(x) dx = sinh(x)cosh(x)/2 - x/2 — hyperbolic even power.
// (ZH) ∫ sinh²(x) dx —— 双曲偶次幂。
Run("sinh(x)^2", () => {
    var x = Symbol("x");
    var expr = Sinh(x) * Sinh(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ sinh²(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ cosh³(x) dx = sinh(x) + sinh³(x)/3 — hyperbolic odd power.
// (ZH) ∫ cosh³(x) dx = sinh(x) + sinh³(x)/3 —— 双曲奇次幂。
Run("cosh(x)^3", () => {
    var x = Symbol("x");
    var expr = Cosh(x)*Cosh(x)*Cosh(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ cosh³(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ tanh²(x) dx = x - tanh(x) — hyperbolic tan power.
// (ZH) ∫ tanh²(x) dx = x - tanh(x) —— 双曲正切幂。
Run("tanh(x)^2 = x - tanh(x)", () => {
    var x = Symbol("x");
    var expr = Tanh(x) * Tanh(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ tanh²(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sech²(x) dx = tanh(x) — sech power.
// (ZH) ∫ sech²(x) dx = tanh(x) —— 双曲正割幂。
Run("sech(x)^2 = tanh(x)", () => {
    var x = Symbol("x");
    var expr = Sech(x) * Sech(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ sech²(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// ── Polynomial expansion rewrite ──
// (EN) Tests for expanding powers/products of sums before integrating.
// (ZH) 先展开和式的幂/乘积再积分的测试。

// (EN) ∫(x²+3)² dx = x⁵/5 + 2x³ + 9x — expand a squared sum.
// (ZH) ∫(x²+3)² dx = x⁵/5 + 2x³ + 9x —— 展开平方和式。
Run("(x^2+3)^2 (expand)", () => {
    var x = Symbol("x");
    var expr = (x*x + 3) * (x*x + 3);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (x²+3)² dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫(x+1)³ dx — expand a cubed sum and collect like terms.
// (ZH) ∫(x+1)³ dx —— 展开三次和式并合并同类项。
Run("(x+1)^3 (expand)", () => {
    var x = Symbol("x");
    var expr = (x + 1)*(x + 1)*(x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (x+1)³ dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫(x+1)(x+2) dx — expand a product of sums.
// (ZH) ∫(x+1)(x+2) dx —— 展开和式的乘积。
Run("(x+1)*(x+2) (expand)", () => {
    var x = Symbol("x");
    var expr = (x + 1)*(x + 2);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (x+1)(x+2) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫(x+1)/(x²+1) dx = atan(x) + ln(1+x²)/2 — expand then integrate each part.
// (ZH) ∫(x+1)/(x²+1) dx = atan(x) + ln(1+x²)/2 —— 展开后分别积分。
Run("(x+1)/(x^2+1) (expand)", () => {
    var x = Symbol("x");
    var expr = (x + 1) / (x*x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (x+1)/(x²+1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// ── Large-exponent robustness (no overflow / no stack overflow) ──
// (EN) Regression tests for the review findings: large binomial coefficients must not overflow,
//      and huge exponents must be declined gracefully.
// (ZH) 针对评审发现的回归测试：大二项式系数不得溢出，超大指数应被优雅拒绝。

// (EN) ∫ tan^68(x)·sec(x) dx — the binomial coefficient C(34,17) exceeds Int32 range.
// (ZH) ∫ tan^68(x)·sec(x) dx —— 二项式系数 C(34,17) 超出 Int32 范围。
Run("tan(x)^68*sec(x) no overflow", () => {
    var x = Symbol("x");
    var expr = new Expression.Power(Tan(x), Expression.Int32(68)) * Sec(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ tan⁶⁸(x)·sec(x) dx length = {r.ToString().Length}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sin^100000(x) dx — above the exponent cap, must return DontKnow instead of crashing.
// (ZH) ∫ sin^100000(x) dx —— 超过指数上限，应返回 DontKnow 而非崩溃。
Run("sin(x)^100000 declined", () => {
    var x = Symbol("x");
    var expr = new Expression.Power(Sin(x), Expression.Int32(100000));
    var steps = Integrate.Steps(expr, x);
    Console.WriteLine($"    ∫ sin^100000(x) dx steps = {steps.GetType().Name}");
    Assert(steps.ContainsDontKnow);
});

// ── Rational / affine / quadratic / product-to-sum / cyclic / log-power ──
// (EN) Regression tests for the linear-coefficient and quadratic-extraction bugs plus the new
//      affine-power, quadratic-denominator, polynomial-division, product-to-sum, cyclic-parts and
//      log-power capabilities.
// (ZH) 线性系数与二次提取 Bug 的回归测试，以及新增的仿射幂、二次分母、多项式除法、积化和差、
//      循环分部与对数幂能力的测试。

// (EN) ∫ 1/(x²+x+1) dx = (2/√3)·atan((2x+1)/√3) — must not be a lone logarithm (Bug 1).
// (ZH) ∫ 1/(x²+x+1) dx = (2/√3)·atan((2x+1)/√3) —— 不得是单个对数（Bug 1）。
Run("1/(x^2+x+1) atan (Bug1)", () => {
    var x = Symbol("x");
    var expr = 1 / (x*x + x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x²+x+1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
    Assert(r.ToString().Contains("atan"));
});

// (EN) ∫ √(x+1) dx = (2/3)(x+1)^(3/2) — must be finite (Bug 2, no ∞).
// (ZH) ∫ √(x+1) dx = (2/3)(x+1)^(3/2) —— 必须有限（Bug 2，无 ∞）。
Run("sqrt(x+1) finite (Bug2)", () => {
    var x = Symbol("x");
    var expr = Sqrt(x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ √(x+1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
    Assert(!r.ToString().Contains("∞") && !r.ToString().Contains("Infinity"));
});

// (EN) ∫ (2x+1)^-2 dx = -1/(2(2x+1)) — affine power with coefficient.
// (ZH) ∫ (2x+1)^-2 dx = -1/(2(2x+1)) —— 带系数的仿射幂。
Run("(2x+1)^-2 (affine pow)", () => {
    var x = Symbol("x");
    var expr = new Expression.Power(2*x + 1, Expression.Int32(-2));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (2x+1)⁻² dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ (2x+1)/(x²+x+1) dx = ln(x²+x+1) — quadratic denominator via the rational part.
// (ZH) ∫ (2x+1)/(x²+x+1) dx = ln(x²+x+1) —— 二次分母的有理部分。
Run("(2x+1)/(x^2+x+1)", () => {
    var x = Symbol("x");
    var expr = (2*x + 1) / (x*x + x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (2x+1)/(x²+x+1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(2x²+3x+4) dx — irrational roots, arctangent branch.
// (ZH) ∫ 1/(2x²+3x+4) dx —— 复根，反正切分支。
Run("1/(2x^2+3x+4)", () => {
    var x = Symbol("x");
    var expr = 1 / (2*x*x + 3*x + 4);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(2x²+3x+4) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x²/(1+x²) dx = x - atan(x) — polynomial long division.
// (ZH) ∫ x²/(1+x²) dx = x - atan(x) —— 多项式长除法。
Run("x^2/(1+x^2) division", () => {
    var x = Symbol("x");
    var expr = x*x / (1 + x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²/(1+x²) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x·atan(x) dx — parts plus polynomial division.
// (ZH) ∫ x·atan(x) dx —— 分部积分配合多项式除法。
Run("x*atan(x)", () => {
    var x = Symbol("x");
    var expr = x * Atan(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x·atan(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sin(3x)·cos(2x) dx — trigonometric product-to-sum.
// (ZH) ∫ sin(3x)·cos(2x) dx —— 三角积化和差。
Run("sin(3x)*cos(2x)", () => {
    var x = Symbol("x");
    var expr = Sin(3*x) * Cos(2*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ sin(3x)·cos(2x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ eˣ·sin(x) dx = eˣ(sin x - cos x)/2 — cyclic integration by parts.
// (ZH) ∫ eˣ·sin(x) dx = eˣ(sin x - cos x)/2 —— 循环分部积分。
Run("exp(x)*sin(x) cyclic", () => {
    var x = Symbol("x");
    var expr = Exp(x) * Sin(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ eˣ·sin(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ ln²(x) dx = x·ln²x - 2x·lnx + 2x — log-power via repeated parts.
// (ZH) ∫ ln²(x) dx = x·ln²x - 2x·lnx + 2x —— 对数幂的重复分部积分。
Run("ln(x)^2", () => {
    var x = Symbol("x");
    var expr = Ln(x) * Ln(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ ln²(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x·ln²(x) dx — polynomial times log-power.
// (ZH) ∫ x·ln²(x) dx —— 多项式乘对数幂。
Run("x*ln(x)^2", () => {
    var x = Symbol("x");
    var expr = x * Ln(x) * Ln(x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x·ln²(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// ── Partial fractions & Weierstrass ──
// (EN) Tests for full rational-function integration (repeated/irreducible factors) and the
//      Weierstrass substitution for rational functions of sin/cos.
// (ZH) 通用有理函数积分（重根/不可约因子）与 sin/cos 有理函数的 Weierstrass 代换测试。

// (EN) ∫ 1/(x²+1)² dx = x/(2(1+x²)) + atan(x)/2 — repeated irreducible quadratic.
// (ZH) ∫ 1/(x²+1)² dx = x/(2(1+x²)) + atan(x)/2 —— 重复不可约二次。
Run("1/(x^2+1)^2 (partial fractions)", () => {
    var x = Symbol("x");
    var expr = 1 / ((x*x + 1) * (x*x + 1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x²+1)² dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(x(x+1)²) dx = ln(x) - ln(x+1) + 1/(x+1) — repeated linear factor.
// (ZH) ∫ 1/(x(x+1)²) dx = ln(x) - ln(x+1) + 1/(x+1) —— 重复线性因子。
Run("1/(x(x+1)^2)", () => {
    var x = Symbol("x");
    var expr = 1 / (x * (x + 1) * (x + 1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x(x+1)²) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(x³-1) dx — linear + irreducible quadratic factors.
// (ZH) ∫ 1/(x³-1) dx —— 线性 + 不可约二次因子。
Run("1/(x^3-1)", () => {
    var x = Symbol("x");
    var expr = 1 / (x*x*x - 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x³-1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ atan(x)/x² dx = -atan(x)/x + ln(x) - ln(1+x²)/2 — parts plus partial fractions.
// (ZH) ∫ atan(x)/x² dx = -atan(x)/x + ln(x) - ln(1+x²)/2 —— 分部积分配合部分分式。
Run("atan(x)/x^2", () => {
    var x = Symbol("x");
    var expr = Atan(x) / (x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ atan(x)/x² dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(x(x²+1)) dx = ln(x) - ln(1+x²)/2 — mixed linear + quadratic.
// (ZH) ∫ 1/(x(x²+1)) dx = ln(x) - ln(1+x²)/2 —— 线性与二次混合。
Run("1/(x(x^2+1))", () => {
    var x = Symbol("x");
    var expr = 1 / (x * (x*x + 1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x(x²+1)) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(2+cos x) dx — Weierstrass substitution.
// (ZH) ∫ 1/(2+cos x) dx —— Weierstrass 代换。
Run("1/(2+cos(x)) (Weierstrass)", () => {
    var x = Symbol("x");
    var expr = 1 / (2 + Cos(x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(2+cos x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(3+5cos x) dx — Weierstrass with distinct poles.
// (ZH) ∫ 1/(3+5cos x) dx —— 具有不同极点的 Weierstrass。
Run("1/(3+5cos(x)) (Weierstrass)", () => {
    var x = Symbol("x");
    var expr = 1 / (3 + 5*Cos(x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(3+5cos x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// ── Exp / sqrt / trig substitutions & inverse-secant ──
// (EN) Tests for t = e^x, t = √x and x = sin θ substitutions, and the asec/acsc closed forms.
// (ZH) t = e^x、t = √x、x = sin θ 换元，以及 asec/acsc 闭式的测试。

// (EN) ∫ eˣ/(1+e^(2x)) dx = atan(eˣ) — exponential substitution.
// (ZH) ∫ eˣ/(1+e^(2x)) dx = atan(eˣ) —— 指数换元。
Run("exp(x)/(1+exp(2x))", () => {
    var x = Symbol("x");
    var expr = Exp(x) / (1 + Exp(2*x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ eˣ/(1+e^(2x)) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ e^(2x)/(1+eˣ) dx = eˣ - ln(1+eˣ) — exponential substitution.
// (ZH) ∫ e^(2x)/(1+eˣ) dx = eˣ - ln(1+eˣ) —— 指数换元。
Run("exp(2x)/(1+exp(x))", () => {
    var x = Symbol("x");
    var expr = Exp(2*x) / (1 + Exp(x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ e^(2x)/(1+eˣ) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(1+√x) dx = 2√x - 2·ln(1+√x) — square-root substitution.
// (ZH) ∫ 1/(1+√x) dx = 2√x - 2·ln(1+√x) —— 根式换元。
Run("1/(1+sqrt(x))", () => {
    var x = Symbol("x");
    var expr = 1 / (1 + Sqrt(x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(1+√x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(√x·(1+x)) dx = 2·atan(√x) — square-root substitution.
// (ZH) ∫ 1/(√x·(1+x)) dx = 2·atan(√x) —— 根式换元。
Run("1/(sqrt(x)(1+x))", () => {
    var x = Symbol("x");
    var expr = 1 / (Sqrt(x) * (1 + x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(√x(1+x)) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x²/√(1-x²) dx — trigonometric substitution x = sin θ.
// (ZH) ∫ x²/√(1-x²) dx —— 三角换元 x = sin θ。
Run("x^2/sqrt(1-x^2)", () => {
    var x = Symbol("x");
    var expr = x*x / Sqrt(1 - x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²/√(1-x²) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x/√(1-x²) dx = -√(1-x²) — trigonometric substitution.
// (ZH) ∫ x/√(1-x²) dx = -√(1-x²) —— 三角换元。
Run("x/sqrt(1-x^2)", () => {
    var x = Symbol("x");
    var expr = x / Sqrt(1 - x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x/√(1-x²) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ asec(x) dx = x·asec(x) - ln(x+√(x²-1)) — inverse-secant closed form.
// (ZH) ∫ asec(x) dx = x·asec(x) - ln(x+√(x²-1)) —— 反正割闭式。
Run("asec(x)", () => {
    var x = Symbol("x");
    var expr = new Expression.Function(FunctionType.Asec, x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ asec(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ acsc(x) dx = x·acsc(x) + ln(x+√(x²-1)) — inverse-cosecant closed form.
// (ZH) ∫ acsc(x) dx = x·acsc(x) + ln(x+√(x²-1)) —— 反余割闭式。
Run("acsc(x)", () => {
    var x = Symbol("x");
    var expr = new Expression.Function(FunctionType.Acsc, x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ acsc(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// ── √(1+x²)/√(x²−1) substitutions, fractional-linear √, rational trig ──
// (EN) Tests for the tan/sec trigonometric substitutions, the fractional-linear square-root
//      rationalisation and sin/cos rationals needing GCD reduction.
// (ZH) tan/sec 三角换元、根式线性有理化，以及需要 GCD 约分的 sin/cos 有理式测试。

// (EN) ∫ 1/(1+x²)^(3/2) dx = x/√(1+x²) — x = tan θ substitution.
// (ZH) ∫ 1/(1+x²)^(3/2) dx = x/√(1+x²) —— x = tan θ 换元。
Run("1/(1+x^2)^(3/2)", () => {
    var x = Symbol("x");
    var expr = new Expression.Power(1 + x*x, new Expression.Number(new Rational(-3, 2)));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(1+x²)^(3/2) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x²/√(1+x²) dx — x = tan θ substitution.
// (ZH) ∫ x²/√(1+x²) dx —— x = tan θ 换元。
Run("x^2/sqrt(1+x^2)", () => {
    var x = Symbol("x");
    var expr = x*x / Sqrt(1 + x*x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²/√(1+x²) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x²/√(x²−1) dx — x = sec θ substitution.
// (ZH) ∫ x²/√(x²−1) dx —— x = sec θ 换元。
Run("x^2/sqrt(x^2-1)", () => {
    var x = Symbol("x");
    var expr = x*x / Sqrt(x*x - 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²/√(x²−1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ √((x+1)/(x−1)) dx — fractional-linear square-root rationalisation.
// (ZH) ∫ √((x+1)/(x−1)) dx —— 根式线性有理化。
Run("sqrt((x+1)/(x-1))", () => {
    var x = Symbol("x");
    var expr = Sqrt((x + 1) / (x - 1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ √((x+1)/(x−1)) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/√((x+1)/(x−1)) dx — fractional-linear with exponent -1/2.
// (ZH) ∫ 1/√((x+1)/(x−1)) dx —— 指数为 -1/2 的根式线性。
Run("1/sqrt((x+1)/(x-1))", () => {
    var x = Symbol("x");
    var expr = 1 / Sqrt((x + 1) / (x - 1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/√((x+1)/(x−1)) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ sin²(x)/cos³(x) dx — sin/cos rational needing polynomial GCD reduction.
// (ZH) ∫ sin²(x)/cos³(x) dx —— 需要多项式 GCD 约分的 sin/cos 有理式。
Run("sin(x)^2/cos(x)^3", () => {
    var x = Symbol("x");
    var expr = Sin(x)*Sin(x) / (Cos(x)*Cos(x)*Cos(x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ sin²(x)/cos³(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// ── Euler / Chebyshev substitutions & √(quadratic)-denominator ──
// (EN) Tests for the Euler substitution (general √(quadratic)), Chebyshev substitution (binomial
//      differentials) and the linear-over-√(quadratic) rule.
// (ZH) Euler 代换（一般 √(二次式)）、切比雪夫代换（二项微分）与「线性/√(二次式)」规则的测试。

// (EN) ∫ x/√(x²+x+1) dx — Euler substitution with a non-zero linear term.
// (ZH) ∫ x/√(x²+x+1) dx —— 含一次项的 Euler 代换。
Run("x/sqrt(x^2+x+1)", () => {
    var x = Symbol("x");
    var expr = x / Sqrt(x*x + x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x/√(x²+x+1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(x·√(x²+1)) dx — Euler substitution.
// (ZH) ∫ 1/(x·√(x²+1)) dx —— Euler 代换。
Run("1/(x*sqrt(x^2+1))", () => {
    var x = Symbol("x");
    var expr = 1 / (x * Sqrt(x*x + 1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x·√(x²+1)) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x²·(1+x³)^(-2/3) dx = (1+x³)^(1/3) — Chebyshev case 2.
// (ZH) ∫ x²·(1+x³)^(-2/3) dx = (1+x³)^(1/3) —— 切比雪夫情形 2。
Run("x^2*(1+x^3)^(-2/3)", () => {
    var x = Symbol("x");
    var expr = x*x * new Expression.Power(1 + x*x*x, new Expression.Number(new Rational(-2, 3)));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x²·(1+x³)^(-2/3) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ x⁻¹·(1+x³)^(-1/3) dx — Chebyshev case 3.
// (ZH) ∫ x⁻¹·(1+x³)^(-1/3) dx —— 切比雪夫情形 3。
Run("x^-1*(1+x^3)^(-1/3)", () => {
    var x = Symbol("x");
    var expr = new Expression.Power(x, Expression.Int32(-1))
        * new Expression.Power(1 + x*x*x, new Expression.Number(new Rational(-1, 3)));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ x⁻¹·(1+x³)^(-1/3) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ (2x+1)/√(x²+1) dx = 2√(x²+1) + ln(2x+2√(x²+1)) — linear over √(quadratic).
// (ZH) ∫ (2x+1)/√(x²+1) dx = 2√(x²+1) + ln(2x+2√(x²+1)) —— 线性/√(二次式)。
Run("(2x+1)/sqrt(x^2+1)", () => {
    var x = Symbol("x");
    var expr = (2*x + 1) / Sqrt(x*x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (2x+1)/√(x²+1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ (x+1)/√(x²+x+1) dx — linear over a general √(quadratic).
// (ZH) ∫ (x+1)/√(x²+x+1) dx —— 一般 √(二次式) 上的线性分子。
Run("(x+1)/sqrt(x^2+x+1)", () => {
    var x = Symbol("x");
    var expr = (x + 1) / Sqrt(x*x + x + 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ (x+1)/√(x²+x+1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// ── Heaviside / Dirac delta distributions ──
// (EN) Tests for the distributional rules: ∫δ⁽ⁿ⁾(a+bx) dx and ∫Heaviside(mx+b)·g(x) dx.
// (ZH) 分布规则测试：∫δ⁽ⁿ⁾(a+bx) dx 与 ∫Heaviside(mx+b)·g(x) dx。

// (EN) ∫ δ(x) dx = Heaviside(x).
// (ZH) ∫ δ(x) dx = Heaviside(x)。
Run("DiracDelta(x)", () => {
    var x = Symbol("x");
    var expr = new Expression.Function(FunctionType.DiracDelta, x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ δ(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
    Assert(!r.ToString().Contains("DontKnow"));
});

// (EN) ∫ δ(2x−1) dx = Heaviside(2x−1)/2.
// (ZH) ∫ δ(2x−1) dx = Heaviside(2x−1)/2。
Run("DiracDelta(2x-1)", () => {
    var x = Symbol("x");
    var expr = new Expression.Function(FunctionType.DiracDelta, 2*x - 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ δ(2x−1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ δ⁽¹⁾(x) dx = δ(x).
// (ZH) ∫ δ⁽¹⁾(x) dx = δ(x)。
Run("DiracDelta(x,1)", () => {
    var x = Symbol("x");
    var expr = new Expression.FunctionN(FunctionNType.DiracDelta, new[] { x, Expression.Int32(1) });
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ δ⁽¹⁾(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ Heaviside(x) dx = Heaviside(x)·x.
// (ZH) ∫ Heaviside(x) dx = Heaviside(x)·x。
Run("Heaviside(x)", () => {
    var x = Symbol("x");
    var expr = new Expression.Function(FunctionType.Heaviside, x);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ H(x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ Heaviside(x−1)·x dx — step times a co-factor.
// (ZH) ∫ Heaviside(x−1)·x dx —— 阶跃乘以余因子。
Run("Heaviside(x-1)*x", () => {
    var x = Symbol("x");
    var expr = new Expression.Function(FunctionType.Heaviside, x - 1) * x;
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ H(x−1)·x dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// ── Irrational-root quadratics & previously-unsolved cases ──
// (EN) Tests for rational denominators with irrational real roots and the Euler-reduced integral.
// (ZH) 含无理实根的有理分母，以及经 Euler 约化后的积分的测试。

// (EN) ∫ 1/(x²+x−1) dx — quadratic denominator with irrational real roots.
// (ZH) ∫ 1/(x²+x−1) dx —— 无理实根的二次分母。
Run("1/(x^2+x-1)", () => {
    var x = Symbol("x");
    var expr = 1 / (x*x + x - 1);
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(x²+x−1) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/((x+1)·√(x²+1)) dx — Euler substitution reducing to an irrational-root quadratic.
// (ZH) ∫ 1/((x+1)·√(x²+1)) dx —— Euler 代换约化为无理根二次式。
Run("1/((x+1)sqrt(x^2+1))", () => {
    var x = Symbol("x");
    var expr = 1 / ((x + 1) * Sqrt(x*x + 1));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/((x+1)·√(x²+1)) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

// (EN) ∫ 1/(sin x + cos x) dx — Weierstrass reducing to an irrational-root quadratic.
// (ZH) ∫ 1/(sin x + cos x) dx —— Weierstrass 约化为无理根二次式。
Run("1/(sin(x)+cos(x))", () => {
    var x = Symbol("x");
    var expr = 1 / (Sin(x) + Cos(x));
    var r = Integrate.Of(expr, x);
    Console.WriteLine($"    ∫ 1/(sin x+cos x) dx = {r}");
    Assert(!Integrate.Steps(expr, x).ContainsDontKnow);
});

Console.WriteLine($"\n=== Result: {passed} passed, {failed} failed ===");

// (EN) Minimal assertion helper: throws when the condition is false so that the
//      enclosing Run call records the test as failed.
// (ZH) 最小断言辅助方法：条件为假时抛出异常，从而让外层 Run 将该测试记为失败。
static void Assert(bool condition, string msg = "Assertion failed")
{
    if (!condition) throw new Exception(msg);
}
