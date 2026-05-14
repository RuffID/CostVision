using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.Abstractions.DataBase
{
    public abstract class AppDbContextBase(DbContextOptions options) : DbContext(options)
    {
    }
}
