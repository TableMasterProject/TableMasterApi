using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class ReviewControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public ReviewControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task GetByIdRestaurant_ShouldReturnOk_WhenReviewsExist()
        {
            var dal = new Mock<IReviewDAL>();
            var controller = CreateController(dal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var search = new SearchReviews();
            var reviews = new[] { TestData.ReviewOut() };

            dal.Setup(x => x.GetByIdRestaurant(10, search)).ReturnsAsync(reviews);

            var result = await controller.GetByIdRestaurant(10, search);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(reviews);
        }

        [Fact]
        public async Task GetByIdRestaurant_ShouldReturnNotFound_WhenDalReturnsNull()
        {
            var dal = new Mock<IReviewDAL>();
            var controller = CreateController(dal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.GetByIdRestaurant(10, It.IsAny<SearchReviews>())).ReturnsAsync((IEnumerable<ReviewOut>)null!);

            var result = await controller.GetByIdRestaurant(10, new SearchReviews());

            result.Result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task GetMy_ShouldPassCurrentUserIdToDal()
        {
            var dal = new Mock<IReviewDAL>();
            var controller = CreateController(dal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var search = new SearchReviews();
            var reviews = new[] { TestData.ReviewOut(userId: 42) };

            dal.Setup(x => x.GetMy(42, search)).ReturnsAsync(reviews);

            var result = await controller.GetMy(search);

            result.Result.Should().BeOfType<OkObjectResult>();
            dal.Verify(x => x.GetMy(42, search), Times.Once);
        }

        [Fact]
        public async Task Post_ShouldCreateReviewForCurrentUser()
        {
            var dal = new Mock<IReviewDAL>();
            var controller = CreateController(dal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = TestData.ReviewIn();
            var created = TestData.ReviewOut(userId: 42);

            dal.Setup(x => x.Add(42, input)).ReturnsAsync(created);

            var result = await controller.Post(input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(created);
        }

        [Fact]
        public async Task Put_ShouldReturnNotFound_WhenDalReturnsNull()
        {
            var dal = new Mock<IReviewDAL>();
            var controller = CreateController(dal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.Update(42, 6, It.IsAny<ReviewIn>())).ReturnsAsync((ReviewOut?)null);

            var result = await controller.Put(6, TestData.ReviewIn());

            result.Result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Delete_ShouldReturnOk_WhenDalDeletesReview()
        {
            var dal = new Mock<IReviewDAL>();
            var controller = CreateController(dal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.Delete(42, 6)).ReturnsAsync(true);

            var result = await controller.Delete(6);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);
        }

        [Fact]
        public async Task Delete_ShouldReturnNotFound_WhenDalReturnsFalse()
        {
            var dal = new Mock<IReviewDAL>();
            var controller = CreateController(dal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.Delete(42, 6)).ReturnsAsync(false);

            var result = await controller.Delete(6);

            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        private ReviewController CreateController(Mock<IReviewDAL>? dal = null)
        {
            return new ReviewController((dal ?? new Mock<IReviewDAL>()).Object, _jwtService);
        }
    }
}
