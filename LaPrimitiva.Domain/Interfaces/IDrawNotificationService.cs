using System.Threading.Tasks;

namespace LaPrimitiva.Domain.Interfaces
{
    public interface IDrawNotificationService
    {
        Task CheckForNewDrawsAsync();
    }
}
