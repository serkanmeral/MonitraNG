namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventMaintenanceWindowEvaluator
{
    bool IsOutsideAllowedWindow(DateTime utcTimestamp);
}
