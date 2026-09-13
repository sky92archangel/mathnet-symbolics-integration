# Math.NET Symbolics Integration

**纯 C# 符号积分引擎**，移植自 [SymPy](https://github.com/sympy/sympy) 的 `manualintegrate` 模块。
零外部依赖，基于 C# 12 record 类型实现完整的表达式树。

---

## 快速开始

```xml
<ItemGroup>
  <ProjectReference Include="..\mathnet-symbolics-integration\src\Symbolics.Integration\Symbolics.Integration.csproj" />
</ItemGroup>
```

```csharp
using MathNet.Symbolics.Integration;
using static MathNet.Symbolics.Integration.Core.Operators;

var x = Symbol("x");

// ∫ (x² + 2x + 1) dx
var result = Integrate.Of(x*x + 2*x + 1, x);
// ∫ sin(x) dx = -cos(x)
var result2 = Integrate.Of(Sin(x), x);
```

---

## 用法指南

### 运算符重载（推荐）

**支持 `+` `-` `*` `/`，写起来和普通数学表达式一样自然：**

```csharp
var x = Symbol("x");

// ∫ x² + 2x + 1 dx
Integrate.Of(x*x + 2*x + 1, x);

// ∫ 3·sin(x) dx
Integrate.Of(3 * Sin(x), x);

// ∫ (x + sin(x)) dx
Integrate.Of(x + Sin(x), x);

// ∫ (1 + x)² dx
Integrate.Of((1 + x) * (1 + x), x);
```

### 基本积分

```csharp
Integrate.Of(5, x);         // ∫ 5 dx → 5x
Integrate.Of(x, x);         // ∫ x dx → x²/2
Integrate.Of(1 / x, x);     // ∫ 1/x dx → ln(x)
Integrate.Of(Exp(x), x);    // ∫ eˣ dx → eˣ
Integrate.Of(Sin(x), x);    // ∫ sin(x) dx → -cos(x)
Integrate.Of(Cos(x), x);    // ∫ cos(x) dx → sin(x)
Integrate.Of(Sinh(x), x);   // ∫ sinh(x) dx → cosh(x)
Integrate.Of(Cosh(x), x);   // ∫ cosh(x) dx → sinh(x)
```

### 查看积分步骤

```csharp
var steps = Integrate.Steps(Sin(x), x);

if (steps is SinRule)
    Console.WriteLine("使用了正弦规则");

// 延迟求值
var result = steps.Eval();

// 检查是否积不出来
if (steps.ContainsDontKnow)
    Console.WriteLine("暂不支持此积分");
```

---

## 表达式构造 API

### 数值

```csharp
42;             // int → 自动转为 Expression.Number
3.14;           // double → 自动转为 Expression.Approximation
```

### 符号与常数

```csharp
Symbol("x");          // 变量
E;                    // 自然常数 e
Pi;                   // 圆周率 π
I;                    // 虚数单位 i
```

### 算术运算

支持运算符重载，也可以用函数式 API：

| 运算符 | 函数式写法 | 说明 |
|---|---|---|
| `a + b` | `Add(a, b)` | 加 |
| `a - b` | `Subtract(a, b)` | 减 |
| `a * b` | `Multiply(a, b)` | 乘 |
| `a / b` | `Divide(a, b)` | 除 |
| `-a` | `Negate(a)` | 取反 |
| `Pow(a, b)` | `Pow(a, b)` | 次方 |

### 函数

```csharp
Sin(x);  Cos(x);  Tan(x);      // 三角函数
Sinh(x); Cosh(x); Tanh(x);     // 双曲函数
Exp(x);  Ln(x);   Lg(x);       // 指数/对数
Asin(x); Acos(x); Atan(x);     // 反三角函数
Abs(x);  Sqrt(x);              // 绝对值/平方根
Log(b, x);                     // 任意底数对数
```

### 表达式类型检查

```csharp
Expression.IsNumber(expr);      // 是否数字
Expression.IsSymbol(expr);      // 是否变量
Expression.IsPower(expr);       // 是否次方
Expression.IsFunction(expr);    // 是否函数
Expression.IsSum(expr);         // 是否求和
Expression.IsProduct(expr);     // 是否乘积
```

---

## 已实现的积分规则

| 规则 | 示例 |
|---|---|
| **ConstantRule** | `∫ 5 dx = 5x` |
| **PowerRule** | `∫ x² dx = x³/3` |
| **ReciprocalRule** | `∫ 1/x dx = ln(x)` |
| **ExpRule** | `∫ eˣ dx = eˣ` |
| **SinRule** | `∫ sin(x) dx = -cos(x)` |
| **CosRule** | `∫ cos(x) dx = sin(x)` |
| **SinhRule** | `∫ sinh(x) dx = cosh(x)` |
| **CoshRule** | `∫ cosh(x) dx = sinh(x)` |
| **AddRule** | `∫ (f+g) = ∫f + ∫g` |
| **ConstantTimesRule** | `∫ a·f = a·∫f` |
| **URule** | 换元积分 |
| **PartsRule** | 分部积分 |
| **DontKnowRule** | 无法积分时返回占位符 |

---

## 项目结构

```
src/
├── Symbolics.Integration/           # 主库
│   ├── Core/
│   │   ├── Symbol.cs                # 变量/符号
│   │   ├── Rational.cs              # 有理数 (替代 F# BigRational)
│   │   ├── Expression.cs            # 表达式基类 + 12 种子类 + 运算符重载
│   │   ├── FunctionType.cs          # 函数/常数/无穷 枚举
│   │   ├── Operators.cs             # 算术/函数构造器 + 归一化
│   │   ├── Structure.cs             # 树遍历/代换
│   │   └── Algebraic.cs             # 代数分解
│   ├── IntegralInfo.cs
│   ├── IntegrationRule.cs           # 13 种积分规则
│   ├── IntegrationSolver.cs         # 规则分发引擎
│   └── Integrate.cs                 # 入口: Integrate.Of()
└── Symbolics.Integration.Tests/
    └── Program.cs                   # 测试用例
```

## 运行测试

```bash
dotnet run --project src/Symbolics.Integration.Tests
```

---

## 许可证

MIT
