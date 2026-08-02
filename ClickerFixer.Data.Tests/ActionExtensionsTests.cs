using ClickerFixer.Data;

namespace ClickerFixer.Data.Tests;

public class ActionExtensionsTests
{
    [Fact]
    public async Task Debounce_CoalescesBurstIntoSingleCall()
    {
        var callCount = 0;
        var debounced = ((Action)(() => Interlocked.Increment(ref callCount))).Debounce(milliseconds: 20);

        debounced();
        debounced();
        debounced();

        await Task.Delay(200);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Debounce_ExceptionInAction_InvokesOnErrorInsteadOfDisappearing()
    {
        Exception? observed = null;
        var debounced = ((Action)(() => throw new InvalidOperationException("boom")))
            .Debounce(milliseconds: 20, onError: ex => observed = ex);

        debounced();

        await Task.Delay(200);

        Assert.NotNull(observed);
        Assert.IsType<InvalidOperationException>(observed);
        Assert.Equal("boom", observed!.Message);
    }

    [Fact]
    public async Task Debounce_ExceptionWithNoErrorHandler_DoesNotThrowUnobserved()
    {
        var debounced = ((Action)(() => throw new InvalidOperationException("boom"))).Debounce(milliseconds: 20);

        var ex = Record.Exception(() => debounced());
        Assert.Null(ex); // debounce itself never throws synchronously

        await Task.Delay(200);
        // No assertion beyond "test process didn't crash" — this exercises the
        // default onError path (Console.WriteLine) instead of an unobserved
        // task exception reaching the finalizer thread.
    }
}
