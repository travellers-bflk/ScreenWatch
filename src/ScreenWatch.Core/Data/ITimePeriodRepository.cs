using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public interface ITimePeriodRepository
{
    List<TimePeriod> GetAll();
    int Add(TimePeriod period);
    void Update(TimePeriod period);
    void Delete(int id);
}
