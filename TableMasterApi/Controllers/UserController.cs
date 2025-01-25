using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using TableMasterApi.DAL;
using TableMasterApi.Model;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly UserDAL _userRepository = new UserDAL();

    public UserController()
    {
    }

    // GET api/user/{id} -> Récupérer un utilisateur par ID
    [HttpGet("{id}")]
    public ActionResult<UserOut> GetUserById(long id)
    {
        try
        {
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

    // GET api/user -> Récupérer tous les utilisateurs
    [HttpGet]
    public ActionResult<IEnumerable<UserOut>> GetAllUsers()
    {
        try
        {
            var users = _userRepository.GetAllUsers();
            return Ok(users);
        } catch (Exception e)
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
        catch (Exception e)
        {
            return StatusCode(500, e.Message);
        }
    }

}
