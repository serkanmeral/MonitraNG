using MngWorkflow.Application.Execution;

namespace MngWorkflow.Application.Services;

public interface IWorkflowExpressionEvaluator
{
    bool EvaluateBoolean(string expression, WorkflowExecutionContext context);
}
