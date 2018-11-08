using Microsoft.AspNetCore.Http;

namespace KnotDiary.UsersApi.Models
{
    public class UpdateUserImageModel
    {
        public IFormFile File { get; set; }
    }
}
