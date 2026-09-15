using MathNet.Symbolics.Integration.Core;

namespace MathNet.Symbolics.Integration;

/// <summary>
/// (EN) Holds the integrand and the integration variable for a sub-integral.
/// (ZH) 持有子积分的被积表达式与积分变量。
/// </summary>
public readonly record struct IntegralInfo(Expression Integrand, Expression Variable);
