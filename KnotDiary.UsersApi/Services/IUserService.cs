using KnotDiary.Models;
using KnotDiary.Services;
using KnotDiary.UsersApi.Domain;
using System.Threading.Tasks;

namespace KnotDiary.UsersApi.Services
{
    public interface IUserService : ICrudService<User>
    {
        Task<User> GetUserByName(string username);

        Task<EntityMetadata<User, UserErrors>> Create(string username, string password, string email);

        Task<EntityMetadata<User, UserErrors>> Update(string username, string firstName, string lastName, string email, string mobile, string description);
        
        Task<EntityMetadata<User, UserErrors>> ChangePassword(string username, string oldPassword, string newPassword);

        Task<EntityMetadata<User, UserErrors>> UpdateAvatar(string username, MediaUpload media);

        Task<EntityMetadata<User, UserErrors>> UpdateAvatar(User user, MediaUpload media);

        Task<EntityMetadata<User, UserErrors>> UpdateBackground(string username, MediaUpload media);

        Task<EntityMetadata<User, UserErrors>> UpdateBackground(User user, MediaUpload media);

        Task<EntityMetadata<User, UserErrors>> GetByCredentials(string username, string password);
    }
}
