using MathNet.Symbolics.Integration.Core;

namespace MathNet.Symbolics.Integration;

/// <summary>
/// (EN) Main entry point for symbolic integration.
/// (ZH) 符号积分的主入口。
/// </summary>
public static class Integrate
{
    /// <summary>
    /// (EN) Compute the indefinite integral of an expression.
    /// (ZH) 计算表达式的原函数（不定积分）。
    /// </summary>
    public static Expression Of(Expression integrand, Expression variable)
    {
        var solver = new IntegrationSolver();
        var rule = solver.Solve(integrand, variable);
        return Operators.Simplify(rule.Eval());
    }

    /// <summary>
    /// (EN) Returns the integration steps (rule tree) for inspection.
    /// (ZH) 返回积分步骤（规则树），供检查使用。
    /// </summary>
    public static IntegrationRule Steps(Expression integrand, Expression variable)
    {
        var solver = new IntegrationSolver();
        return solver.Solve(integrand, variable);
    }
}
