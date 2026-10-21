using AwesomeAssertions;
using Faemiyah.Arranger.Grains.Abstractions;
using Faemiyah.Arranger.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ApiEndpointExtensionsTests
{
    [TestCase(CommandError.NotFound, StatusCodes.Status404NotFound)]
    [TestCase(CommandError.NotMember, StatusCodes.Status403Forbidden)]
    [TestCase(CommandError.Forbidden, StatusCodes.Status403Forbidden)]
    [TestCase(CommandError.Invalid, StatusCodes.Status400BadRequest)]
    [TestCase(CommandError.Conflict, StatusCodes.Status409Conflict)]
    public void ToHttpResult_MapsErrorsToProblems(CommandError error, int statusCode)
    {
        var result = CommandResult.Failure(error, "Nope.").ToHttpResult();

        var problem = result.Should().BeOfType<ProblemHttpResult>().Which;
        problem.StatusCode.Should().Be(statusCode);
        problem.ProblemDetails.Detail.Should().Be("Nope.");
    }

    [Test]
    public void ToHttpResult_SuccessWithoutSnapshot_IsNoContent()
    {
        CommandResult.Success(null).ToHttpResult().Should().BeOfType<NoContent>();
    }
}
