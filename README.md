# MathNet Symbolics Integration

A pure C# symbolic integration library — a port of SymPy's `manualintegrate` module.  
**Zero NuGet dependencies** — only `System.Numerics.BigInteger` + custom `Rational` struct.

## Quick Start

```csharp
using MathNet.Symbolics.Integration;
using static MathNet.Symbolics.Integration.Core.Operators;

var x = Symbol("x");

// Atomic rules
Integrate.Of(Sin(x), x);           // -cos(x)
Integrate.Of(Exp(x), x);           // e^x
Integrate.Of(1 / x, x);             // ln(x)
Integrate.Of(Tan(x), x);            // -ln|cos(x)|

// Strategy: u-substitution
Integrate.Of(3*x*Exp(x*x), x);      // 3/2·e^(x²)

// Strategy: integration by parts (LIATE)
Integrate.Of(x*Cos(x), x);          // x·sin(x) + cos(x)
Integrate.Of(x*Exp(x), x);          // x·e^x - e^x

// Linear arguments
Integrate.Of(Sin(2*x), x);          // -1/2·cos(2x)
Integrate.Of(Exp(3*x), x);          // 1/3·e^(3x)

// Quadratic sqrt forms
Integrate.Of(1 / Sqrt(1 + x*x), x); // asinh(x)
Integrate.Of(1 / Sqrt(1 - x*x), x); // asin(x)
Integrate.Of(1 / (1 + x*x), x);     // atan(x)

// General quadratic sqrt: ∫ 1/√(ax²+bx+c) dx
Integrate.Of(1 / Sqrt(x*x + 2*x + 2), x);
// ln(2 + 2x + 2√(2+2x+x²))

// ∫ √(ax²+bx+c) dx
Integrate.Of(Sqrt(x*x + 1), x);
// (x/2)·√(1+x²) + 1/2·ln(2x+2√(1+x²))

// Special functions
Integrate.Of(Exp(-x*x), x);         // √π/2·erf(x)
Integrate.Of(Sin(x) / x, x);        // Si(x)
Integrate.Of(Cos(x) / x, x);        // Ci(x)
Integrate.Of(Exp(x) / x, x);        // Ei(x)
Integrate.Of(Sin(x*x), x);          // Fresnel integral
Integrate.Of(1 / Ln(x), x);         // Li(x)
Integrate.Of(x*x * Exp(x), x);      // Upper incomplete gamma Γ(3,-x)
Integrate.Of(Exp(-x*x) * Erf(2*x), x); // Owens T function
// Polylog: Li(b, a·x) / x → Li(b+1, a·x)
var poly = FunctionN(Polylog, [One, 2*x]) / x;
Integrate.Of(poly, x);
// Elliptic integrals:
var sinSq = Power(Sin(x), 2);
Integrate.Of(1 / Sqrt(2 - sinSq), x);  // F(x, ½)/√2
Integrate.Of(Sqrt(2 - sinSq), x);      // E(x, ½)·√2

// Rational functions
Integrate.Of(1 / (x-1), x);         // ln|x-1|
Integrate.Of(1 / ((x-1)*(x-2)), x); // partial fractions

// Orthogonal polynomials
var Pn = new FunctionN(FunctionNType.LegendreP, new[] { Symbol("n"), x });
Integrate.Of(Pn, x);                // (P_{n+1}-P_{n-1})/(2n+1)

// Step-by-step
IntegrationRule steps = Integrate.Steps(x*Cos(x), x);
// Returns: PartsRule(U=x, Dv=cos(x), VStep=SinRule, SecondStep=PowerRule)
```

## Supported Rules

| Category | Rules |
|---|---|
| **Basic** | Constant, Power, Reciprocal, Exp |
| **Trigonometric** | sin, cos, tan, cot, sec, csc |
| **Hyperbolic** | sinh, cosh, tanh, coth, sech, csch |
| **Inverse trig** | asin, acos, atan |
| **Inverse hyp** | asinh, acosh, atanh |
| **Strategy** | Sum, Constant×, u-substitution, Parts, CyclicParts |
| **Linear args** | `f(ax+b)` automatic detection |
| **Quadratic sqrt** | `1/√(ax²+bx+c)`, `√(ax²+bx+c)`, `1/(a+bx²)` |
| **Special funcs** | Erf, FresnelS/C, Si, Ci, Shi, Chi, Ei, Li, **Polylog**, **UpperGamma** |
| **Elliptic** | **EllipticF** (1st kind), **EllipticE** (2nd kind) |
| **Owens T** | **OwensT** `exp(-(ax+b)²)·erf(y·(ax+b))` |
| **Orthogonal poly** | LegendreP, ChebyshevT/U, HermiteH, LaguerreL, GegenbauerC, JacobiP, AssocLaguerreL |
| **Rational** | `1/(x-a)^k`, `1/(ax+b)`, partial fractions |

## API

```csharp
// Compute indefinite integral
Expression Integrate.Of(Expression integrand, Expression variable);

// Get rule tree for inspection
IntegrationRule Integrate.Steps(Expression integrand, Expression variable);

// Rule eval
Expression rule.Eval();

// Simplify expression tree
Expression Operators.Simplify(Expression expr);
```

## Project Structure

```
src/
  Symbolics.Integration/
    Integrate.cs              — Entry point (Of / Steps)
    IntegrationRule.cs        — Rule hierarchy (~35 rule classes)
    IntegrationSolver.cs      — Recursive solver with strategy ordering
    Core/
      Expression.cs           — Symbolic expression types + ToString
      Operators.cs            — Arithmetic + Simplify
      FunctionType.cs         — Function enums
      Rational.cs             — Exact rational arithmetic
      Structure.cs            — Tree traversal utilities
      Algebraic.cs            — Summand/Factor decomposition
  Symbolics.Integration.Tests/
    Program.cs                — 63 integration tests
```

## Build

```bash
dotnet build src/Symbolics.Integration
dotnet run --project src/Symbolics.Integration.Tests
```
