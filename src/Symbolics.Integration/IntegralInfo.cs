using MathNet.Symbolics.Integration.Core;

namespace MathNet.Symbolics.Integration;

/// <summary>
/// Holds the integrand and the integration variable for a sub-integral.
/// </summary>
public readonly record struct IntegralInfo(Expression Integrand, Expression Variable);
