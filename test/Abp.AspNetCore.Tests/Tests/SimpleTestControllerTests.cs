using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Abp.AspNetCore.App.Controllers;
using Abp.AspNetCore.App.Models;
using Abp.Events.Bus;
using Abp.Events.Bus.Exceptions;
using Abp.UI;
using Abp.Web.Mvc.Models;
using Shouldly;
using Xunit;

namespace Abp.AspNetCore.Tests
{
    public class SimpleTestControllerTests : AppTestBase
    {
        [Fact]
        public void Should_Resolve_Controller()
        {
            ServiceProvider.GetService<SimpleTestController>().ShouldNotBeNull();
        }

        [Fact]
        public async Task Should_Return_Content()
        {
            // Act
            var response = await GetResponseAsStringAsync(
                               GetUrl<SimpleTestController>(
                                   nameof(SimpleTestController.SimpleContent)
                               )
                           );

            // Assert
            response.ShouldBe("Hello world...");
        }

        [Fact]
        public async Task Should_Wrap_Json_By_Default()
        {
            // Act
            var response = await GetResponseAsObjectAsync<MvcAjaxResponse<SimpleViewModel>>(
                               GetUrl<SimpleTestController>(
                                   nameof(SimpleTestController.SimpleJson)
                               )
                           );

            //Assert
            response.Result.StrValue.ShouldBe("Forty Two");
            response.Result.IntValue.ShouldBe(42);
        }

        [Theory]
        [InlineData(true, "This is a user friendly exception message")]
        [InlineData(false, "This is an exception message")]
        public async Task Should_Wrap_Json_Exception_By_Default(bool userFriendly, string message)
        {
            //Arrange

            var exceptionEventRaised = false;
            Resolve<IEventBus>().Register<AbpHandledExceptionData>(data =>
            {
                exceptionEventRaised = true;
                data.Exception.ShouldNotBeNull();
                data.Exception.Message.ShouldBe(message);
            });

            // Act

            var response = await GetResponseAsObjectAsync<MvcAjaxResponse<SimpleViewModel>>(
                               GetUrl<SimpleTestController>(
                                   nameof(SimpleTestController.SimpleJsonException),
                                   new
                                   {
                                       message,
                                       userFriendly
                                   }),
                               HttpStatusCode.InternalServerError
                           );

            //Assert

            response.Error.ShouldNotBeNull();
            if (userFriendly)
            {
                response.Error.Message.ShouldBe(message);
            }
            else
            {
                response.Error.Message.ShouldNotBe(message);
            }

            exceptionEventRaised.ShouldBeTrue();
        }

        [Fact]
        public async Task Should_Not_Wrap_Json_Exception_If_Requested()
        {
            //Act & Assert
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await GetResponseAsObjectAsync<MvcAjaxResponse<SimpleViewModel>>(
                    GetUrl<SimpleTestController>(
                        nameof(SimpleTestController.SimpleJsonExceptionDownWrap)
                    ));
            });
        }

        [Fact]
        public async Task Should_Not_Wrap_Json_If_DontWrap_Declared()
        {
            // Act
            var response = await GetResponseAsObjectAsync<SimpleViewModel>(
                               GetUrl<SimpleTestController>(
                                   nameof(SimpleTestController.SimpleJsonDontWrap)
                               )
                           );

            //Assert
            response.StrValue.ShouldBe("Forty Two");
            response.IntValue.ShouldBe(42);
        }
    }
}