namespace KnotDiary.UsersApi.Models
{
    public class UpdatePasswordModel
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
}
