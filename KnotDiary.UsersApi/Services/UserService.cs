using MongoDB.Bson;
using KnotDiary.Common;
using KnotDiary.Common.Messaging;
using KnotDiary.Models;
using KnotDiary.Services;
using KnotDiary.UsersApi.Data;
using KnotDiary.UsersApi.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KnotDiary.UsersApi.Services
{
    public class UserService : IUserService
    {
        private readonly IStorageService _storageService;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IConfigurationHelper _configurationHelper;
        private readonly ITopicProducer _topicProducer;
        private readonly ILogger _logger;
        private readonly string EventsUserUpdatedExchange;
        private readonly string EventsUserCreatedExchange;

        public UserService(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IConfigurationHelper configurationHelper,
            IStorageService storageService,
            ITopicProducer topicProducer,
            ILogger logger)
        {
            _userRepository = userRepository;
            _storageService = storageService;
            _passwordHasher = passwordHasher;
            _configurationHelper = configurationHelper;
            _topicProducer = topicProducer;
            _logger = logger;

            EventsUserCreatedExchange = _configurationHelper.GetAppSettings("Messaging:Exchanges:Producer:UsersCreated");
            EventsUserUpdatedExchange = _configurationHelper.GetAppSettings("Messaging:Exchanges:Producer:UsersUpdated");
        }

        public async Task<User> Create(User model)
        {
            return await _userRepository.Insert(model);
        }

        public async Task<EntityMetadata<User, UserErrors>> Create(string username, string password, string email)
        {
            var user = await _userRepository.GetAsync(a => a.Username == username || a.Email == email);
            if (user != null)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(user.Email == email ? UserErrors.EmailInUse : UserErrors.UsernameInUse)
                });
            }

            if (!IsPasswordValid(password))
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.PasswordTooWeak)
                });
            }

            try
            {
                var hashedPassword = _passwordHasher.HashPassword(password);
                var newUser = new User { Username = username, Password = hashedPassword, Email = email };

                var result = await Create(newUser);
                if (result != null)
                {
                    _topicProducer.SendToExchange(EventsUserCreatedExchange, null, result);
                    return new EntityMetadata<User, UserErrors>(result);
                }

                _logger.Error($"KnotDiary.UsersApi.Services.UserService | Create | Null response during db insert - {username} - {email}");
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UnhandledError)
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"KnotDiary.UsersApi.Services.UserService | Create | DbError - {username} - {email}");
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UnhandledError)
                });
            }
        }

        public async Task<bool> Delete(string id)
        {
            return await _userRepository.Delete(id);
        }

        public async Task<User> Get(string id)
        {
            return await _userRepository.GetAsync(a => a.Id == ObjectId.Parse(id));
        }

        public async Task<IEnumerable<User>> GetAll(int skip = 0, int take = 15)
        {
            return await _userRepository.GetListAsync(null, null, skip, take);
        }

        public async Task<IEnumerable<User>> GetAll(Expression<Func<User, bool>> filter = null, int skip = 0, int take = 15)
        {
            return await _userRepository.GetListAsync(filter, null, skip, take);
        }

        public async Task<IEnumerable<User>> GetAll(Expression<Func<User, bool>> filter = null, Expression<Func<User, object>> orderBy = null, int skip = 0, int take = 15)
        {
            return await _userRepository.GetListAsync(filter, orderBy, skip, take);
        }

        public async Task<EntityMetadata<User, UserErrors>> GetByCredentials(string username, string password)
        {
            var user = await _userRepository.GetAsync(a => a.Username == username);
            if (user == null)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>> { new ErrorInfo<UserErrors>(UserErrors.UserNotFound) });
            }

            var isPasswordCorrect = _passwordHasher.VerifyHashedPassword(user.Password, password);
            if (isPasswordCorrect)
            {
                return new EntityMetadata<User, UserErrors>(user);
            }

            return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>> { new ErrorInfo<UserErrors>(UserErrors.UserNotFound) });
        }

        public async Task<User> GetUserByName(string username)
        {
            return await _userRepository.GetAsync(a => a.Username == username);
        }

        public async Task<User> Update(User model)
        {
            return await _userRepository.Update(model);
        }

        public async Task<EntityMetadata<User, UserErrors>> ChangePassword(string username, string oldPassword, string newPassword)
        {
            var user = await _userRepository.GetAsync(a => a.Username == username);
            if (user == null)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UserNotFound)
                });
            }

            var isPasswordCorrect = _passwordHasher.VerifyHashedPassword(user.Password, oldPassword);
            if (!isPasswordCorrect)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.WrongPasswordUsed)
                });
            }

            var hashedPassword = _passwordHasher.HashPassword(newPassword);
            user.Password = hashedPassword;

            var result = await _userRepository.Update(user);
            if (result == null)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.FailedToChangePassword)
                });
            }

            return new EntityMetadata<User, UserErrors>(result);
        }

        public async Task<EntityMetadata<User, UserErrors>> Update(string username, string firstName, string lastName, string email, string mobile, string description)
        {
            var user = await _userRepository.GetAsync(a => a.Username == username);
            if (user == null)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UserNotFound)
                });
            }

            var userByEmail = await _userRepository.GetAsync(a => a.Email == email);
            if (userByEmail != null && user.Username != username)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.EmailInUse)
                });
            }

            if (!string.IsNullOrEmpty(firstName))
            {
                user.FirstName = firstName;
            }

            if (!string.IsNullOrEmpty(lastName))
            {
                user.LastName = lastName;
            }

            if (!string.IsNullOrEmpty(email))
            {
                user.Email = email;
            }

            if (!string.IsNullOrEmpty(mobile))
            {
                user.Mobile = mobile;
            }

            if (!string.IsNullOrEmpty(description))
            {
                user.Description = description;
            }

            try
            {
                var result = await Update(user);
                if (result != null)
                {
                    _topicProducer.SendToExchange(EventsUserUpdatedExchange, null, result);
                    return new EntityMetadata<User, UserErrors>(result);
                }

                _logger.Error($"KnotDiary.UsersApi.Services.UserService | Update | Null response during db update - {username} - {email}");
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UnhandledError)
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"KnotDiary.UsersApi.Services.UserService | Update | DbError - {username} - {email}");
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UnhandledError)
                });
            }
        }

        public async Task<EntityMetadata<User, UserErrors>> UpdateAvatar(string username, MediaUpload media)
        {
            var user = await _userRepository.GetAsync(a => a.Username == username);
            if (user != null)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UserNotFound)
                });
            }

            return await UpdateAvatar(user, media);
        }

        public async Task<EntityMetadata<User, UserErrors>> UpdateAvatar(User user, MediaUpload media)
        {
            var avatarUrl = await UploadUserMedia(_configurationHelper.GetAppSettings("App:Storage:UserAvatarPath"), user, media);
            if (string.IsNullOrEmpty(avatarUrl))
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.FailedToUploadAvatar)
                });
            }

            user.AvatarUrl = avatarUrl;

            try
            {
                var result = await Update(user);
                if (result != null)
                {
                    _topicProducer.SendToExchange(EventsUserUpdatedExchange, null, result);
                    return new EntityMetadata<User, UserErrors>(result);
                }

                _logger.Error($"KnotDiary.UsersApi.Services.UserService | UpdateAvatar | Null response during db update - {user.Username}");
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UnhandledError)
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"KnotDiary.UsersApi.Services.UserService | UpdateAvatar | DbError - {user.Username}");
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UnhandledError)
                });
            }
        }

        public async Task<EntityMetadata<User, UserErrors>> UpdateBackground(string username, MediaUpload media)
        {
            var user = await _userRepository.GetAsync(a => a.Username == username);
            if (user != null)
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UserNotFound)
                });
            }

            return await UpdateAvatar(user, media);
        }

        public async Task<EntityMetadata<User, UserErrors>> UpdateBackground(User user, MediaUpload media)
        {
            var profileBackgroundUrl = await UploadUserMedia(_configurationHelper.GetAppSettings("App:Storage:UserBackgroundPath"), user, media);
            if (string.IsNullOrEmpty(profileBackgroundUrl))
            {
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.FailedToUploadBackground)
                });
            }

            user.ProfileBackgroundUrl = profileBackgroundUrl;

            try
            {
                var result = await Update(user);
                if (result != null)
                {
                    return new EntityMetadata<User, UserErrors>(result);
                }

                _logger.Error($"KnotDiary.UsersApi.Services.UserService | UpdateBackground | Null response during db update - {user.Username}");
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UnhandledError)
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"KnotDiary.UsersApi.Services.UserService | UpdateBackground | DbError - {user.Username}");
                return new EntityMetadata<User, UserErrors>(new List<ErrorInfo<UserErrors>>
                {
                    new ErrorInfo<UserErrors>(UserErrors.UnhandledError)
                });
            }
        }

        private async Task<string> UploadUserMedia(string container, User user, MediaUpload media)
        {
            var generatedFileName = $"{container}/{user.Username}_{DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss")}.{media.FileType}";
            media.FileName = generatedFileName;
            var imageUrl = await _storageService.UploadStreamAsync(_configurationHelper.GetAppSettings("App:Storage:AppContainer"), media);

            return imageUrl;
        }

        private bool IsPasswordValid(string password)
        {
            var expression = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,15}$");
            var match = expression.Match(password);

            return match.Success;
        }
    }
}
