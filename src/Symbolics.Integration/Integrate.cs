using MathNet.Symbolics.Integration.Core;

namespace MathNet.Symbolics.Integration;

/// <summary>
/// Main entry point for symbolic integration.
/// </summary>
public static class Integrate
{
    /// <summary>Compute the indefinite integral of an expression.</summary>
    public static Expression Of(Expression integrand, Expression variable)
    {
        var solver = new IntegrationSolver();
        var rule = solver.Solve(integrand, variable);
        return Operators.Simplify(rule.Eval());
    }

    /// <summary>Returns the integration steps (rule tree) for inspection.</summary>
    public static IntegrationRule Steps(Expression integrand, Expression variable)
    {
        var solver = new IntegrationSolver();
        return solver.Solve(integrand, variable);
    }
}
