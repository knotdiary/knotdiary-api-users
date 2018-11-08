using Newtonsoft.Json;
using KnotDiary.Models;

namespace KnotDiary.UsersApi.Domain
{
    public class User : BaseEntity
    {
        public string Username { get; set; }

        public string Description { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public string Mobile { get; set; }

        [JsonIgnore]
        public string Password { get; set; }

        public string AvatarUrl { get; set; }

        public string ProfileBackgroundUrl { get; set; }
    }
}
