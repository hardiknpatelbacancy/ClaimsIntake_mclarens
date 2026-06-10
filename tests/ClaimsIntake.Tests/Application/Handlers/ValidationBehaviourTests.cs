using ClaimsIntake.Application.Common.Behaviours;
using FluentValidation;
using MediatR;

namespace ClaimsIntake.Tests.Application.Handlers;

public class ValidationBehaviourTests
{
    private sealed record TestRequest(string Value) : IRequest<string>;

    private sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator() => RuleFor(r => r.Value).NotEmpty();
    }

    [Fact]
    public async Task InvalidRequest_ThrowsValidationException_AndHandlerIsNeverInvoked()
    {
        var behaviour = new ValidationBehaviour<TestRequest, string>([new TestRequestValidator()]);
        var handlerInvoked = false;

        await Assert.ThrowsAsync<ValidationException>(() =>
            behaviour.Handle(new TestRequest(""), _ => { handlerInvoked = true; return Task.FromResult("ok"); }, CancellationToken.None));

        Assert.False(handlerInvoked);
    }

    [Fact]
    public async Task ValidRequest_PassesThroughToHandler()
    {
        var behaviour = new ValidationBehaviour<TestRequest, string>([new TestRequestValidator()]);

        var result = await behaviour.Handle(new TestRequest("fine"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task NoValidatorsRegistered_PassesThrough()
    {
        var behaviour = new ValidationBehaviour<TestRequest, string>([]);

        var result = await behaviour.Handle(new TestRequest(""), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
    }
}
