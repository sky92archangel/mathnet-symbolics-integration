// ----------------------------------------------------------------------------
// (EN) Purpose: Declares the tag enums naming every unary function, n-ary
//       function, constant and infinity kind understood by the symbolic
//       integration engine.
// (ZH) 用途：声明符号积分引擎所能识别的函数与常数标签枚举，涵盖一元函数、
//       多元（n 元）函数、常量类型以及无穷大类型。
// (EN) Notes: The members are opaque tags carrying no behaviour; a separate
//       dispatcher maps each tag to the matching MathNet.Symbolics expression
//       node and to a numeric evaluator / integration rule.
// (ZH) 说明：枚举成员只是纯标签，本身不含运算逻辑；由独立的分派器把每个标签映射到
//       对应的 MathNet.Symbolics 表达式节点，以及相应的数值求值器或积分规则。
// ----------------------------------------------------------------------------

namespace MathNet.Symbolics.Integration.Core;

/// <summary>
/// (EN) Unary mathematical functions.
/// (ZH) 一元数学函数。
/// </summary>
public enum FunctionType
{
    /// <summary>
    /// (EN) Absolute value |x|; real-valued and defined for every real x.
    /// (ZH) 绝对值 |x|；实值函数，对所有实数 x 都有定义。
    /// </summary>
    Abs,
    /// <summary>
    /// (EN) Natural logarithm ln(x); domain x &gt; 0 over the reals.
    /// (ZH) 自然对数 ln(x)；在实数域上定义域为 x &gt; 0。
    /// </summary>
    Ln,
    /// <summary>
    /// (EN) Common (base-10) logarithm lg(x) = log10(x); domain x &gt; 0.
    /// (ZH) 常用（以 10 为底）对数 lg(x) = log10(x)；定义域 x &gt; 0。
    /// </summary>
    Lg,
    /// <summary>
    /// (EN) Exponential e^x; entire, maps the reals onto (0, +∞).
    /// (ZH) 指数函数 e^x；在复平面上解析（整函数），在实数域上值域为 (0, +∞)。
    /// </summary>
    Exp,
    /// <summary>
    /// (EN) Sine sin(x); argument in radians, period 2π.
    /// (ZH) 正弦 sin(x)；参数以弧度计，周期为 2π。
    /// </summary>
    Sin,
    /// <summary>
    /// (EN) Cosine cos(x); argument in radians, period 2π.
    /// (ZH) 余弦 cos(x)；参数以弧度计，周期为 2π。
    /// </summary>
    Cos,
    /// <summary>
    /// (EN) Tangent tan(x) = sin(x)/cos(x); poles at x = π/2 + kπ.
    /// (ZH) 正切 tan(x) = sin(x)/cos(x)；在 x = π/2 + kπ 处有极点。
    /// </summary>
    Tan,
    /// <summary>
    /// (EN) Cosecant csc(x) = 1/sin(x); undefined where sin(x) = 0.
    /// (ZH) 余割 csc(x) = 1/sin(x)；在 sin(x) = 0 处无定义。
    /// </summary>
    Csc,
    /// <summary>
    /// (EN) Secant sec(x) = 1/cos(x); undefined where cos(x) = 0.
    /// (ZH) 正割 sec(x) = 1/cos(x)；在 cos(x) = 0 处无定义。
    /// </summary>
    Sec,
    /// <summary>
    /// (EN) Cotangent cot(x) = cos(x)/sin(x); undefined where sin(x) = 0.
    /// (ZH) 余切 cot(x) = cos(x)/sin(x)；在 sin(x) = 0 处无定义。
    /// </summary>
    Cot,
    /// <summary>
    /// (EN) Hyperbolic sine sinh(x) = (e^x - e^-x)/2.
    /// (ZH) 双曲正弦 sinh(x) = (e^x - e^-x)/2。
    /// </summary>
    Sinh,
    /// <summary>
    /// (EN) Hyperbolic cosine cosh(x) = (e^x + e^-x)/2.
    /// (ZH) 双曲余弦 cosh(x) = (e^x + e^-x)/2。
    /// </summary>
    Cosh,
    /// <summary>
    /// (EN) Hyperbolic tangent tanh(x) = sinh(x)/cosh(x); range (-1, 1).
    /// (ZH) 双曲正切 tanh(x) = sinh(x)/cosh(x)；值域为 (-1, 1)。
    /// </summary>
    Tanh,
    /// <summary>
    /// (EN) Hyperbolic cosecant csch(x) = 1/sinh(x); undefined at x = 0.
    /// (ZH) 双曲余割 csch(x) = 1/sinh(x)；在 x = 0 处无定义。
    /// </summary>
    Csch,
    /// <summary>
    /// (EN) Hyperbolic secant sech(x) = 1/cosh(x); always positive.
    /// (ZH) 双曲正割 sech(x) = 1/cosh(x)；恒为正。
    /// </summary>
    Sech,
    /// <summary>
    /// (EN) Hyperbolic cotangent coth(x) = cosh(x)/sinh(x); undefined at x = 0.
    /// (ZH) 双曲余切 coth(x) = cosh(x)/sinh(x)；在 x = 0 处无定义。
    /// </summary>
    Coth,
    /// <summary>
    /// (EN) Arcsine asin(x); domain [-1, 1], principal range [-π/2, π/2].
    /// (ZH) 反正弦 asin(x)；定义域 [-1, 1]，主值范围 [-π/2, π/2]。
    /// </summary>
    Asin,
    /// <summary>
    /// (EN) Arccosine acos(x); domain [-1, 1], principal range [0, π].
    /// (ZH) 反余弦 acos(x)；定义域 [-1, 1]，主值范围 [0, π]。
    /// </summary>
    Acos,
    /// <summary>
    /// (EN) Arctangent atan(x); entire, principal range (-π/2, π/2).
    /// (ZH) 反正切 atan(x)；处处有定义，主值范围 (-π/2, π/2)。
    /// </summary>
    Atan,
    /// <summary>
    /// (EN) Arccosecant acsc(x) = asin(1/x); domain |x| ≥ 1, range [-π/2, π/2] excluding 0.
    /// (ZH) 反余割 acsc(x) = asin(1/x)；定义域 |x| ≥ 1，值域为 [-π/2, π/2] 去掉 0。
    /// </summary>
    Acsc,
    /// <summary>
    /// (EN) Arcsecant asec(x) = acos(1/x); domain |x| ≥ 1, range [0, π] excluding π/2.
    /// (ZH) 反正割 asec(x) = acos(1/x)；定义域 |x| ≥ 1，值域为 [0, π] 去掉 π/2。
    /// </summary>
    Asec,
    /// <summary>
    /// (EN) Arccotangent acot(x); continuous principal branch on (0, π).
    /// (ZH) 反余切 acot(x)；主值取连续分支，值域为 (0, π)。
    /// </summary>
    Acot,
    /// <summary>
    /// (EN) Inverse hyperbolic sine asinh(x); entire, range all reals.
    /// (ZH) 反双曲正弦 asinh(x)；处处有定义，值域为全体实数。
    /// </summary>
    Asinh,
    /// <summary>
    /// (EN) Inverse hyperbolic cosine acosh(x); domain x ≥ 1, range [0, +∞).
    /// (ZH) 反双曲余弦 acosh(x)；定义域 x ≥ 1，值域 [0, +∞)。
    /// </summary>
    Acosh,
    /// <summary>
    /// (EN) Inverse hyperbolic tangent atanh(x); domain (-1, 1), range all reals.
    /// (ZH) 反双曲正切 atanh(x)；定义域 (-1, 1)，值域为全体实数。
    /// </summary>
    Atanh,
    /// <summary>
    /// (EN) Inverse hyperbolic cosecant acsch(x) = asinh(1/x); domain x ≠ 0.
    /// (ZH) 反双曲余割 acsch(x) = asinh(1/x)；定义域 x ≠ 0。
    /// </summary>
    Acsch,
    /// <summary>
    /// (EN) Inverse hyperbolic secant asech(x) = acosh(1/x); domain (0, 1].
    /// (ZH) 反双曲正割 asech(x) = acosh(1/x)；定义域 (0, 1]。
    /// </summary>
    Asech,
    /// <summary>
    /// (EN) Inverse hyperbolic cotangent acoth(x); domain |x| &gt; 1.
    /// (ZH) 反双曲余切 acoth(x)；定义域 |x| &gt; 1。
    /// </summary>
    Acoth,
    // Special functions
    /// <summary>
    /// (EN) Error function erf(x) = (2/√π) ∫₀ˣ e^(−t²) dt; odd, range (−1, 1).
    /// (ZH) 误差函数 erf(x) = (2/√π) ∫₀ˣ e^(−t²) dt；奇函数，值域 (−1, 1)。
    /// </summary>
    Erf,
    /// <summary>
    /// (EN) Complementary error function erfc(x) = 1 − erf(x); used for large x to avoid cancellation.
    /// (ZH) 互补误差函数 erfc(x) = 1 − erf(x)；x 较大时用它可避免直接相减造成的精度损失。
    /// </summary>
    Erfc,
    /// <summary>
    /// (EN) Imaginary error function erfi(x) = −i·erf(ix) = (2/√π) ∫₀ˣ e^(t²) dt.
    /// (ZH) 虚误差函数 erfi(x) = −i·erf(ix) = (2/√π) ∫₀ˣ e^(t²) dt。
    /// </summary>
    Erfi,
    /// <summary>
    /// (EN) Fresnel cosine integral C(x) = ∫₀ˣ cos(π t²/2) dt.
    /// (ZH) Fresnel 余弦积分 C(x) = ∫₀ˣ cos(π t²/2) dt。
    /// </summary>
    FresnelC,
    /// <summary>
    /// (EN) Fresnel sine integral S(x) = ∫₀ˣ sin(π t²/2) dt.
    /// (ZH) Fresnel 正弦积分 S(x) = ∫₀ˣ sin(π t²/2) dt。
    /// </summary>
    FresnelS,
    /// <summary>
    /// (EN) Sine integral Si(x) = ∫₀ˣ sin(t)/t dt (the integrand is taken as 1 at t = 0).
    /// (ZH) 正弦积分 Si(x) = ∫₀ˣ sin(t)/t dt（t = 0 处被积函数取极限值 1）。
    /// </summary>
    Si,
    /// <summary>
    /// (EN) Cosine integral Ci(x) = −∫ₓ^∞ cos(t)/t dt; defined for x &gt; 0.
    /// (ZH) 余弦积分 Ci(x) = −∫ₓ^∞ cos(t)/t dt；定义域 x &gt; 0。
    /// </summary>
    Ci,
    /// <summary>
    /// (EN) Hyperbolic sine integral Shi(x) = ∫₀ˣ sinh(t)/t dt.
    /// (ZH) 双曲正弦积分 Shi(x) = ∫₀ˣ sinh(t)/t dt。
    /// </summary>
    Shi,
    /// <summary>
    /// (EN) Hyperbolic cosine integral Chi(x) = γ + ln x + ∫₀ˣ (cosh(t) − 1)/t dt; x &gt; 0.
    /// (ZH) 双曲余弦积分 Chi(x) = γ + ln x + ∫₀ˣ (cosh(t) − 1)/t dt；定义域 x &gt; 0。
    /// </summary>
    Chi,
    /// <summary>
    /// (EN) Exponential integral Ei(x) = −PV ∫₋ₓ^∞ e^(−t)/t dt; a pole lies at x = 0.
    /// (ZH) 指数积分 Ei(x) = −PV ∫₋ₓ^∞ e^(−t)/t dt；在 x = 0 处有极点，积分取柯西主值。
    /// </summary>
    Ei,
    /// <summary>
    /// (EN) Logarithmic integral li(x) = PV ∫₀ˣ dt/ln t; principal branch for x &gt; 1.
    /// (ZH) 对数积分 li(x) = PV ∫₀ˣ dt/ln t；主值分支用于 x &gt; 1。
    /// </summary>
    Li,
    /// <summary>
    /// (EN) Airy function of the first kind Ai(x), a solution of y'' = x·y decaying as x → +∞.
    /// (ZH) 第一类 Airy 函数 Ai(x)，方程 y'' = x·y 的一个解，当 x → +∞ 时衰减到 0。
    /// </summary>
    AiryAi,
    /// <summary>
    /// (EN) Derivative of the Airy function of the first kind, Ai′(x).
    /// (ZH) 第一类 Airy 函数的导数 Ai′(x)。
    /// </summary>
    AiryAiPrime,
    /// <summary>
    /// (EN) Airy function of the second kind Bi(x), the linearly independent solution of y'' = x·y.
    /// (ZH) 第二类 Airy 函数 Bi(x)，方程 y'' = x·y 的另一个线性无关解。
    /// </summary>
    AiryBi,
    /// <summary>
    /// (EN) Derivative of the Airy function of the second kind, Bi′(x).
    /// (ZH) 第二类 Airy 函数的导数 Bi′(x)。
    /// </summary>
    AiryBiPrime,
    /// <summary>
    /// (EN) Heaviside step function H(x): 0 for x &lt; 0 and 1 for x &gt; 0 (distribution).
    /// (ZH) Heaviside 阶跃函数 H(x)：x &lt; 0 时为 0，x &gt; 0 时为 1（分布意义）。
    /// </summary>
    Heaviside,
    /// <summary>
    /// (EN) Dirac delta δ(x), the distributional derivative of Heaviside (order 0).
    /// (ZH) Dirac δ 函数 δ(x)，Heaviside 的分布意义导数（0 阶）。
    /// </summary>
    DiracDelta,
}

/// <summary>
/// (EN) N-ary mathematical functions (2 or more arguments).
/// (ZH) 多元（n 元）数学函数（2 个或更多参数）。
/// </summary>
public enum FunctionNType
{
    /// <summary>
    /// (EN) Logarithm to an explicit base; args = [basis, x], i.e. log_basis(x).
    /// (ZH) 指定底数的对数；参数为 [basis, x]，即 log_basis(x)。
    /// </summary>
    Log,
    /// <summary>
    /// (EN) Two-argument arctangent; args = [y, x], returns the angle of the point (x, y) in (−π, π].
    /// (ZH) 双参数反正切；参数为 [y, x]，返回点 (x, y) 的辐角，取值于 (−π, π]。
    /// </summary>
    Atan2,
    // Orthogonal polynomials: args = [degree, x]
    /// <summary>
    /// (EN) Legendre polynomial P_n(x); args = [degree n, x], n a non-negative integer.
    /// (ZH) Legendre 多项式 P_n(x)；参数为 [次数 n, x]，n 为非负整数。
    /// </summary>
    LegendreP,
    /// <summary>
    /// (EN) Chebyshev polynomial of the first kind T_n(x) = cos(n·acos x); args = [degree n, x].
    /// (ZH) 第一类 Chebyshev 多项式 T_n(x) = cos(n·acos x)；参数为 [次数 n, x]。
    /// </summary>
    ChebyshevT,
    /// <summary>
    /// (EN) Chebyshev polynomial of the second kind U_n(x); args = [degree n, x].
    /// (ZH) 第二类 Chebyshev 多项式 U_n(x)；参数为 [次数 n, x]。
    /// </summary>
    ChebyshevU,
    /// <summary>
    /// (EN) Physicists' Hermite polynomial H_n(x); args = [degree n, x], weight e^(−x²).
    /// (ZH) 物理学家版 Hermite 多项式 H_n(x)；参数为 [次数 n, x]，权函数为 e^(−x²)。
    /// </summary>
    HermiteH,
    /// <summary>
    /// (EN) Laguerre polynomial L_n(x); args = [degree n, x], weight e^(−x) on [0, ∞).
    /// (ZH) Laguerre 多项式 L_n(x)；参数为 [次数 n, x]，权函数为 [0, ∞) 上的 e^(−x)。
    /// </summary>
    LaguerreL,
    /// <summary>
    /// (EN) Gegenbauer (ultraspherical) polynomial C_n^{(λ)}(x); args = [degree n, λ, x] (λ = a in the solver).
    /// (ZH) Gegenbauer（超球）多项式 C_n^{(λ)}(x)；参数为 [次数 n, λ, x]（求解器中 λ 记为 a）。
    /// </summary>
    GegenbauerC,
    /// <summary>
    /// (EN) Jacobi polynomial P_n^{(α,β)}(x); args = [degree n, α, β, x] (α = a, β = b in the solver).
    /// (ZH) Jacobi 多项式 P_n^{(α,β)}(x)；参数为 [次数 n, α, β, x]（求解器中 α 记为 a，β 记为 b）。
    /// </summary>
    JacobiP,
    /// <summary>
    /// (EN) Associated Laguerre polynomial L_n^{(k)}(x); args = [degree n, order k, x].
    /// (ZH) 连带 Laguerre 多项式 L_n^{(k)}(x)；参数为 [次数 n, 阶数 k, x]。
    /// </summary>
    AssocLaguerreL,
    // Special functions
    /// <summary>
    /// (EN) Owen's T function T(u, y) = (1/2π) ∫₀ᵘ exp(−y²(1+t²)/2)/(1+t²) dt; args = [u, y].
    /// (ZH) Owen's T 函数 T(u, y) = (1/2π) ∫₀ᵘ exp(−y²(1+t²)/2)/(1+t²) dt；参数为 [u, y]。
    /// </summary>
    OwensT,
    /// <summary>
    /// (EN) Polylogarithm Li_s(z) = Σ_{k≥1} z^k / k^s; args = [s, z], |z| &lt; 1 for the defining series.
    /// (ZH) 多重对数（polylogarithm）Li_s(z) = Σ_{k≥1} z^k / k^s；参数为 [s, z]，定义级数要求 |z| &lt; 1。
    /// </summary>
    Polylog,
    /// <summary>
    /// (EN) Upper incomplete gamma Γ(s, z) = ∫_z^∞ t^(s−1) e^(−t) dt; args = [s, z].
    /// (ZH) 上不完全 Gamma 函数 Γ(s, z) = ∫_z^∞ t^(s−1) e^(−t) dt；参数为 [s, z]。
    /// </summary>
    UpperGamma,
    /// <summary>
    /// (EN) Incomplete elliptic integral of the first kind F(φ | m) = ∫₀^φ dθ/√(1 − m·sin²θ); args = [phi, m].
    /// (ZH) 第一类不完全椭圆积分 F(φ | m) = ∫₀^φ dθ/√(1 − m·sin²θ)；参数为 [phi, m]。
    /// </summary>
    EllipticF,
    /// <summary>
    /// (EN) Incomplete elliptic integral of the second kind E(φ | m) = ∫₀^φ √(1 − m·sin²θ) dθ; args = [phi, m].
    /// (ZH) 第二类不完全椭圆积分 E(φ | m) = ∫₀^φ √(1 − m·sin²θ) dθ；参数为 [phi, m]。
    /// </summary>
    EllipticE,
    /// <summary>
    /// (EN) Bessel function of the first kind J_ν(x); args = [order ν, x].
    /// (ZH) 第一类 Bessel 函数 J_ν(x)；参数为 [阶 ν, x]。
    /// </summary>
    BesselJ,
    /// <summary>
    /// (EN) Bessel function of the second kind Y_ν(x); args = [order ν, x], x &gt; 0.
    /// (ZH) 第二类 Bessel 函数 Y_ν(x)；参数为 [阶 ν, x]，定义域 x &gt; 0。
    /// </summary>
    BesselY,
    /// <summary>
    /// (EN) Modified Bessel function of the first kind I_ν(x); args = [order ν, x].
    /// (ZH) 第一类修正 Bessel 函数 I_ν(x)；参数为 [阶 ν, x]。
    /// </summary>
    BesselI,
    /// <summary>
    /// (EN) Modified Bessel function of the second kind K_ν(x); args = [order ν, x], x &gt; 0.
    /// (ZH) 第二类修正 Bessel 函数 K_ν(x)；参数为 [阶 ν, x]，定义域 x &gt; 0。
    /// </summary>
    BesselK,
    /// <summary>
    /// (EN) Ratio of consecutive modified Bessel functions of the first kind I_{ν+1}(x)/I_ν(x); args = [order ν, x].
    /// (ZH) 相邻第一类修正 Bessel 函数之比 I_{ν+1}(x)/I_ν(x)；参数为 [阶 ν, x]。
    /// </summary>
    BesselIRatio,
    /// <summary>
    /// (EN) Ratio of consecutive modified Bessel functions of the second kind K_{ν+1}(x)/K_ν(x); args = [order ν, x].
    /// (ZH) 相邻第二类修正 Bessel 函数之比 K_{ν+1}(x)/K_ν(x)；参数为 [阶 ν, x]。
    /// </summary>
    BesselKRatio,
    /// <summary>
    /// (EN) Hankel function of the first kind H⁽¹⁾_ν(x) = J_ν(x) + i·Y_ν(x); args = [order ν, x].
    /// (ZH) 第一类 Hankel 函数 H⁽¹⁾_ν(x) = J_ν(x) + i·Y_ν(x)；参数为 [阶 ν, x]。
    /// </summary>
    HankelH1,
    /// <summary>
    /// (EN) Hankel function of the second kind H⁽²⁾_ν(x) = J_ν(x) − i·Y_ν(x); args = [order ν, x].
    /// (ZH) 第二类 Hankel 函数 H⁽²⁾_ν(x) = J_ν(x) − i·Y_ν(x)；参数为 [阶 ν, x]。
    /// </summary>
    HankelH2,
    /// <summary>
    /// (EN) Dirac delta of order n: δ⁽ⁿ⁾(a+b·x) as a distribution; args = [a+b·x, n] with n a positive integer.
    /// (ZH) n 阶 Dirac δ 函数 δ⁽ⁿ⁾(a+b·x)（分布意义）；参数为 [a+b·x, n]，n 为正整数。
    /// </summary>
    DiracDelta,
}

/// <summary>
/// (EN) Well-known mathematical constants.
/// (ZH) 常用数学常数。
/// </summary>
public enum ConstantType
{
    /// <summary>
    /// (EN) Euler's number e = 2.71828…, the base of the natural logarithm.
    /// (ZH) 自然常数 e = 2.71828…，自然对数的底。
    /// </summary>
    E,
    /// <summary>
    /// (EN) The circle constant π = 3.14159…, ratio of circumference to diameter.
    /// (ZH) 圆周率 π = 3.14159…，圆周长与直径之比。
    /// </summary>
    Pi,
    /// <summary>
    /// (EN) The imaginary unit i with i² = −1.
    /// (ZH) 虚数单位 i，满足 i² = −1。
    /// </summary>
    I,
}

/// <summary>
/// (EN) Types of infinity.
/// (ZH) 无穷大的类型。
/// </summary>
public enum InfinityType
{
    /// <summary>
    /// (EN) Complex infinity: an unbounded magnitude with undefined direction (e.g. 1/0).
    /// (ZH) 复无穷：模无界但方向不确定（例如 1/0 的结果）。
    /// </summary>
    ComplexInfinity,
    /// <summary>
    /// (EN) Positive real infinity +∞.
    /// (ZH) 正实数无穷 +∞。
    /// </summary>
    PositiveInfinity,
    /// <summary>
    /// (EN) Negative real infinity −∞.
    /// (ZH) 负实数无穷 −∞。
    /// </summary>
    NegativeInfinity,
}
