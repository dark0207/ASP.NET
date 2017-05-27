﻿using System.Net;
using System.Threading.Tasks;
using Abp.AspNetCore.App.Controllers;
using Abp.Web.Models;
using Shouldly;
using Xunit;

namespace Abp.AspNetCore.Tests
{
    public class Validation_Tests : AppTestBase
    {
        [Fact]
        public async Task Should_Work_With_Valid_Parameters_ActionResult()
        {
            // Act
            var response = await GetResponseAsStringAsync(
                GetUrl<ValidationTestController>(
                    nameof(ValidationTestController.GetContentValue),
                    new { value = 42 }
                )
            );

            response.ShouldBe("OK: 42");
        }

        [Fact]
        public async Task Should_Work_With_Valid_Parameters_JsonResult()
        {
            // Act
            var response = await GetResponseAsObjectAsync<AjaxResponse<ValidationTestController.ValidationTestArgument1>>(
                GetUrl<ValidationTestController>(
                    nameof(ValidationTestController.GetJsonValue),
                    new { value = 42 }
                )
            );

            response.Success.ShouldBeTrue();
            response.Result.Value.ShouldBe(42);
        }

        [Theory]
        [InlineData(-2)]
        [InlineData("undefined")]
        [InlineData(null)]
        public async Task Should_Not_Work_With_Invalid_Parameters(object value)
        {
            // Act
            var response = await GetResponseAsObjectAsync<AjaxResponse<ValidationTestController.ValidationTestArgument1>>(
                GetUrl<ValidationTestController>(
                    nameof(ValidationTestController.GetJsonValue),
                    new { value = value }
                ),
                HttpStatusCode.BadRequest
            );

            response.Success.ShouldBeFalse();
            response.Result.ShouldBeNull();
            response.Error.ShouldNotBeNull();
            response.Error.ValidationErrors.ShouldNotBeNull();
            response.Error.ValidationErrors[0].Members.Length.ShouldBe(1);
            response.Error.ValidationErrors[0].Members[0].ShouldBe("value");
        }

        [Fact]
        public async Task Should_Not_Work_With_Invalid_Parameters_2()
        {
            // Act
            var response = await GetResponseAsObjectAsync<AjaxResponse<ValidationTestController.ValidationTestArgument1>>(
                GetUrl<ValidationTestController>(
                    nameof(ValidationTestController.GetJsonValue)
                ),
                HttpStatusCode.BadRequest
            );

            response.Success.ShouldBeFalse();
            response.Result.ShouldBeNull();
            response.Error.ShouldNotBeNull();
            response.Error.ValidationErrors.ShouldNotBeNull();
            response.Error.ValidationErrors[0].Members.Length.ShouldBe(1);
            response.Error.ValidationErrors[0].Members[0].ShouldBe("value");
        }
    }
}
