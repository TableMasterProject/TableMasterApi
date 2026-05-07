using Microsoft.Extensions.Options;
using Moq;
using TableMasterApi.Model;

namespace TableMasterApi.Tests.Fixtures
{
    /// <summary>
    /// Fixture pour configurer les paramètres de JWT utilisés dans les tests
    /// </summary>
    public class JwtConfigFixture
    {
        public ConfigPerso JwtConfig { get; }
        public IOptions<ConfigPerso> JwtConfigOptions { get; }

        public JwtConfigFixture()
        {
            JwtConfig = new ConfigPerso
            {
                SecretKey = "this_is_a_very_secret_key_for_testing_purposes_that_is_long_enough_for_jwt",
                Issuer = "TableMasterTestIssuer",
                Audience = "TableMasterTestAudience"
            };

            var optionsMock = Options.Create(JwtConfig);
            JwtConfigOptions = optionsMock;
        }
    }

    /// <summary>
    /// Fixture pour créer des utilisateurs de test avec données cohérentes
    /// </summary>
    public class UserTestDataFixture
    {
        public UserOut CreateTestUserOut(long id = 1, string email = "test@example.com", string firstName = "John", string lastName = "Doe")
        {
            return new UserOut
            {
                Id = id,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Password = "Password123!",
                CreatedAt = DateTime.UtcNow,
                RestaurantId = null,
                AccountType = 1
            };
        }

        public LoginUserIn CreateLoginUser(string email = "test@example.com", string password = "Password123!")
        {
            return new LoginUserIn
            {
                Email = email,
                Password = password
            };
        }
    }
}
