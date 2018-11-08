namespace KnotDiary.UsersApi.Domain
{
    public enum UserErrors
    {
        UnhandledError = 0,
        UserNotFound = 1,
        UsernameInUse = 2,
        PasswordTooWeak = 3,
        EmailInUse = 4,
        FailedToUploadAvatar = 5,
        FailedToUploadBackground = 6,
        FailedToChangePassword = 7,
        WrongPasswordUsed = 8
    }
}
