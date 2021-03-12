using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BookStore.Models;
using BookStore.Services;
using BookStore.Token;
using Microsoft.AspNetCore.Authorization;
using AutoMapper;
using System.Collections.Generic;
using BookStore.View_Models.User;

namespace BookStore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserAuthController : ControllerBase
    {
        private readonly UserServices _userServices;
        private readonly AccessToken _accessToken;
        private readonly RefreshToken _refreshToken;
        private readonly IMapper _mapper;

        public UserAuthController(UserServices userServices,
            AccessToken accessToken,
            RefreshToken refreshToken,
            IMapper mapper)
        {
            _userServices = userServices;
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _mapper = mapper;
        }
        [HttpPost]
        [Route("login")]
        public async Task<ActionResult<User>> Login(LoginViewModel user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid User");
            }

            User fromDb = await _userServices.DoLogin(user);
            if (fromDb is null)
            {
                return Ok(new { error_message = "Đăng nhập thất bại, vui lòng thử lại!" });
            }

            TokenPair tokenPair = new TokenPair()
            {
                Access = _accessToken.GenerateToken(fromDb),
                Refresh = _refreshToken.GenerateToken()
            };

            await _userServices.createUserTokenAsync(fromDb, tokenPair.Refresh);
            return Ok(new
            {
                data = tokenPair,
                success = true,
            });
        }

        [HttpPost]
        [Route("refresh")]
        public async Task<ActionResult<User>> Refresh(TokenPair token)
        {
            bool isValidToken;
            //Validate from client
            if (token.Refresh is null)
            {
                return Unauthorized(new { error_message = "Refresh token invalid" });
            }

            isValidToken = _refreshToken.Validate(token.Refresh);
            if (!isValidToken)
            {
                return Unauthorized(new { error_message = "Refresh token invalid" });
            }

            //Validate from server
            User user = await _userServices.GetByRefreshToken(token.Refresh);

            if (user is null)
            {
                return Unauthorized(new { error_message = "Refresh token invalid" });
            }

            isValidToken = _refreshToken.Validate(user.RefreshToken);
            if (!isValidToken)
            {
                return Unauthorized(new { error_message = "Refresh token invalid" });
            }

            //Create new token pair
            TokenPair tokenPair = new TokenPair
            {
                Access = _accessToken.GenerateToken(user),
                Refresh = _refreshToken.GenerateToken()
            };
            await _userServices.createUserTokenAsync(user, tokenPair.Refresh);
            return Ok(new
            {
                data = tokenPair,
                success = true,
            });
        }
        [HttpPut("profile/{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id,[FromForm]UserInfoPostModel userVM)
        {
            if (userVM.Avatar is not null)
            {
                if (!_userServices.isValidImage(userVM.Avatar)) return BadRequest(new { error_message = "Lỗi hình ảnh" });
            }

            UserInfoViewModel userInfoViewModel = await _userServices.UpdateInfoAsync(userVM, id);
            if(userInfoViewModel is not null)
            {
                return Ok(new { data = userInfoViewModel, success = true, message = "Cập nhật thông tin cá nhân thành công  "});
            }
            else
            {
                return Ok(new { error_message = "Có lỗi xảy ra" });
            }

        }

        [HttpGet("profile/{id}")]
        public async Task<ActionResult<UserInfoViewModel>> GetUserInfo(int id)
        {
            User user = await _userServices.GetUserById(id);
            if (user is null)
            {
                return Ok(new { error_message = "Không tìm thấy user"});
            }
            UserInfoViewModel userInfo = _mapper.Map<User, UserInfoViewModel>(user);
            return Ok(new { data = userInfo, success = true });
        }

        [HttpPut("change-password/{id}")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(int id, ChangePasswordPostModel changePassword)
        {
            
            User user = await _userServices.GetUserByIdAndPasswordAsync(id, changePassword.old_password);
            if(user is null)
            {
                return Ok(new { error_message = "Mật khẩu cũ không hợp lệ"});
            }
            if (await _userServices.ChangePasswordAsync(user, changePassword.new_password))
            {
                return Ok(new {success = true});
            }
            else
            {
                return Ok(new { error_message = "Có lỗi xảy ra khi đổi mật khẩu, vui lòng thử lại"});
            }

        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await _userServices.GetAllUser();
        }
    }
}
