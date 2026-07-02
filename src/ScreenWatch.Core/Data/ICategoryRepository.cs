using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public interface ICategoryRepository
{
    List<Category> GetAll();
    Category? GetById(int id);
    int Add(string name, string color, string? icon = null);
    void Update(int id, string name, string color, string? icon = null);
    void Delete(int id);
}
