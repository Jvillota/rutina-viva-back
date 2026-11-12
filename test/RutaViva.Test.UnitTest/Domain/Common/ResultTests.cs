using FluentAssertions;
using RutaViva.Domain.Common;

namespace RutaViva.Test.UnitTest.Domain.Common;

public class ResultTests
{
    private static readonly Error SampleError = Error.Conflict("sample.conflict", "Sample");

    [Fact]
    public void Success_WhenCreated_HasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        Result<int> result = SampleError;

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Value_WhenResultFailed_Throws()
    {
        Result<int> result = SampleError;

        var read = () => result.Value;

        read.Should().Throw<InvalidOperationException>().WithMessage("*sample.conflict*");
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }
}
