using Microsoft.AspNetCore.Mvc;
using KnotDiary.Common.Extensions;
using KnotDiary.Common.Web.Controllers;
using KnotDiary.Models;
using KnotDiary.UsersApi.Domain;
using KnotDiary.UsersApi.Models;
using KnotDiary.UsersApi.Services;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using KnotDiary.Common;

namespace KnotDiary.UsersApi.Controllers
{
    [Route("api/user")]
    public class UserApiController : BaseApiController
    {
        private readonly IUserService _userService;

        public UserApiController(IUserService userService, ILogHelper logHelper, IConfigurationHelper configurationHelper) : base(logHelper, configurationHelper)
        {
            _userService = userService;
        }

        [Route("")]
        [HttpPost]
        public async Task<IActionResult> Signup([FromBody]CreateUserModel model)
        {
            try
            {
                var result = await _userService.Create(model.Username, model.Password, model.Email);
                if (result.Errors?.Count > 0 || result.Data == null)
                {
                    var error = result.Errors.First();
                    return GetErrorResponse(error.ErrorType.ToEnumString());
                }

                return GetSuccessResponse(true, "Success", result.Data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Route("")]
        [HttpPut]
        public async Task<IActionResult> EditUser([FromBody]UpdateUserModel model, string userId)
        {
            try
            {
                var user = await _userService.Get(userId);
                if (user == null)
                {
                    return Unauthorized();
                }

                var result = await _userService.Update(user.Username, model.FirstName, model.LastName, model.Email, model.Mobile, model.Description);
                if (result.Errors?.Count > 0 || result.Data == null)
                {
                    var error = result.Errors.First();
                    return GetErrorResponse(error.ErrorType.ToEnumString());
                }

                return GetSuccessResponse(true, "Success", result.Data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Route("id/{id}")]
        [HttpGet]
        public async Task<IActionResult> GetUserById(string id)
        {
            try
            {
                var result = await _userService.Get(id);
                if (result == null)
                {
                    return NotFound();
                }

                return GetSuccessResponse(true, "Success", result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [Route("{username}")]
        [HttpGet]
        public async Task<IActionResult> GetUserByUsername(string username)
        {
            try
            {
                var result = await _userService.GetUserByName(username);
                if (result == null)
                {
                    return NotFound();
                }

                return GetSuccessResponse(true, "Success", result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Route("credentials")]
        [HttpGet]
        public async Task<IActionResult> GetByCredentials(string username, string password)
        {
            try
            {
                var result = await _userService.GetByCredentials(username, password);
                if (result == null)
                {
                    return NotFound();
                }
                
                if (result.Errors?.Count > 0 || result.Data == null)
                {
                    var error = result.Errors.First();
                    return GetErrorResponse(error.ErrorType.ToEnumString());
                }


                return GetSuccessResponse(true, "Success", result.Data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Route("{username}/password")]
        [HttpPost]
        public async Task<IActionResult> ChangePassword([FromBody]UpdatePasswordModel model, string userId)
        {
            try
            {
                var user = await _userService.Get(userId);
                if (user == null)
                {
                    return NotFound();
                }

                var result = await _userService.ChangePassword(user.Username, model.OldPassword, model.NewPassword);
                if (result == null || result.Errors?.Count > 0 || result.Data == null)
                {
                    var error = result.Errors.First();
                    return GetErrorResponse(error.ErrorType.ToEnumString());
                }

                return GetSuccessResponse(true, "Success", result.Data);
            }
            catch (Exception ex)
            {
                return GetErrorResponse(ex, "Failed to update your avatar. Please try again.");
            }
        }

        [Route("{username}/avatar")]
        [HttpPost]
        public async Task<IActionResult> UpdateAvatar(UpdateUserImageModel model, string userId)
        {
            try
            {
                var user = await _userService.Get(userId);
                if (user == null)
                {
                    return NotFound();
                }

                if (model.File == null)
                {
                    return GetErrorResponse("Failed to update your avatar. Please try again.");
                }

                using (var stream = model.File.OpenReadStream())
                {
                    var result = await _userService.UpdateAvatar(user, new MediaUpload { File = stream, FileType = GetUploadFileExtension(model.File.FileName) });
                    if (result == null || result.Errors?.Count > 0 || result.Data == null)
                    {
                        var error = result.Errors.First();
                        return GetErrorResponse(error.ErrorType.ToEnumString());
                    }

                    return GetSuccessResponse(true, "Success", result.Data);
                }

            }
            catch (Exception ex)
            {
                return GetErrorResponse(ex, "Failed to update your avatar. Please try again.");
            }
        }

        [Route("{username}/background")]
        [HttpPost]
        public async Task<IActionResult> UpdateBackground(UpdateUserImageModel model, string userId)
        {
            try
            {
                var user = await _userService.Get(userId);
                if (user == null)
                {
                    return NotFound();
                }

                if (model.File == null)
                {
                    return GetErrorResponse("Failed to update your profile background. Please try again.");
                }

                using (var stream = model.File.OpenReadStream())
                {
                    var result = await _userService.UpdateBackground(user, new MediaUpload { File = stream, FileType = GetUploadFileExtension(model.File.FileName) });
                    if (result == null || result.Errors?.Count > 0 || result.Data == null)
                    {
                        var error = result.Errors.First();
                        return GetErrorResponse(error.ErrorType.ToEnumString());
                    }

                    return GetSuccessResponse(true, "Success", result.Data);
                }

            }
            catch (Exception ex)
            {
                return GetErrorResponse(ex, "Failed to update your profile background. Please try again.");
            }
        }
    }
}