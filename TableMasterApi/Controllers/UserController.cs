using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using TableMasterApi.Service;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly UserDAL _userRepository = new UserDAL();
    private readonly AuthDAL _authDAL = new AuthDAL();
    private readonly JwtService _jwtService;

    public UserController(JwtService jwtService)
    {
        _jwtService = jwtService;
    }

    // GET api/user/{id} -> Récupérer un utilisateur par ID
    [Authorize]
    [HttpGet("{id}")]
    public ActionResult<UserOut> GetUserById(long id)
    {
        try
        {
            if (id == null)
            {
                return BadRequest();
            }
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var user = _userRepository.GetUserById(id);
            if (user == null)
            {
                return NotFound();
            }
            return Ok(user);
        } catch (Exception e)
        {
            return StatusCode(500, e.Message);
        }
    }

    [Authorize]
    [HttpPut]
    public ActionResult<UserOut> PutUser([FromBody] UserIn user)
    {
        try
        {
            if (user == null)
            {
                return BadRequest();
            }
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var userPut = _userRepository.PutUser(idUserToken,user);
            if (user == null)
            {
                return NotFound();
            }
            return Ok(userPut);
        }
        catch (SqlException e)
        {
            if (e.Number == 2627)
            {
                return StatusCode(403, "L'Email existe deja dans la base");
            }
            return StatusCode(500, e.Message);
        }
    }
    [Authorize]
    [HttpPut]
    [Route("Password")]
    public ActionResult<bool> PutPassword([FromBody] PasswordEntity passwordEntity)
    {
        try
        {
            if (passwordEntity == null)
            {
                return BadRequest();
            }
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var user = _userRepository.GetUserById(idUserToken);
            if (user == null)
            {
                return NotFound();
            }

            var resultMatchOldPassword = _authDAL.VerifyPassword(user.Password, passwordEntity.OldPassword);

            if (!resultMatchOldPassword)
            {
                return StatusCode(404, "ancien Mot de passe incorrect");
            }

            var passwordChange = _userRepository.PutPassword(user, passwordEntity.NewPassword);


            return Ok(passwordChange);
        }
        catch (Exception e)
        {
            return StatusCode(500, e.Message);
        }
    }

    // POST api/user -> Ajouter un nouvel utilisateur
    [HttpPost]
    public ActionResult<UserOut> AddUser([FromBody] UserIn user)
    {
        try
        {
            if (user == null)
            {
                return BadRequest();
            }

            var addedUser = _userRepository.AddUser(user);
            return Ok(addedUser);
        }
        catch (SqlException e)
        {
            if (e.Number == 2627)
            {
                return StatusCode(403, "L'Email existe deja dans la base");
            }
            return StatusCode(500, e.Message);
        }
    }

    [Authorize]
    [HttpDelete]
    public ActionResult<UserOut> DeleteUser()
    {
        try
        {
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var userPut = _userRepository.DeletePassword(idUserToken);

            return Ok(userPut);
        }
        catch (SqlException e)
        {
            return StatusCode(500, e.Message);
        }
    }

}
