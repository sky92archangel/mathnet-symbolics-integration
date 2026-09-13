namespace MathNet.Symbolics.Integration.Core;

/// <summary>Unary mathematical functions.</summary>
public enum FunctionType
{
    Abs,
    Ln, Lg, Exp,
    Sin, Cos, Tan, Csc, Sec, Cot,
    Sinh, Cosh, Tanh, Csch, Sech, Coth,
    Asin, Acos, Atan, Acsc, Asec, Acot,
    Asinh, Acosh, Atanh, Acsch, Asech, Acoth,
    // Special functions
    Erf, Erfc, Erfi,
    FresnelC, FresnelS,
    Si, Ci, Shi, Chi,
    Ei, Li,
    AiryAi, AiryAiPrime,
    AiryBi, AiryBiPrime,
}

/// <summary>N-ary mathematical functions (2 or more arguments).</summary>
public enum FunctionNType
{
    Log,
    Atan2,
    // Orthogonal polynomials: args = [degree, x]
    LegendreP, ChebyshevT, ChebyshevU,
    HermiteH, LaguerreL, GegenbauerC, JacobiP,
    AssocLaguerreL,  // args = [degree, k, x]
    // Special functions
    OwensT,          // args = [u, y]
    Polylog,         // args = [s, z]
    UpperGamma,      // args = [s, z]
    EllipticF,       // args = [phi, m]
    EllipticE,       // args = [phi, m]
    BesselJ, BesselY,
    BesselI, BesselK,
    BesselIRatio, BesselKRatio,
    HankelH1, HankelH2,
}

/// <summary>Well-known mathematical constants.</summary>
public enum ConstantType
{
    E,
    Pi,
    I,
}

/// <summary>Types of infinity.</summary>
public enum InfinityType
{
    ComplexInfinity,
    PositiveInfinity,
    NegativeInfinity,
}
