using MngAlarm.Application.Contracts;

namespace MngAlarm.Application.Services;

public interface IAlarmActorAccessor
{
    AlarmActorContext GetCurrentActor();
}
