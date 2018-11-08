using KnotDiary.Common;
using KnotDiary.Data;
using KnotDiary.UsersApi.Domain;

namespace KnotDiary.UsersApi.Data
{
    public class UserRepository : MongoDbGenericRepository<User>, IUserRepository
    {
        public UserRepository(IConfigurationHelper configurationHelper) : base(configurationHelper)
        {
        }
    }
}
