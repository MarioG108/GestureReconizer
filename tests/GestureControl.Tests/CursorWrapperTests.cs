using GestureControl.Interop.Native;
using Xunit;

namespace GestureControl.Tests;

public class CursorWrapperTests
{
    [Fact]
    public void CursorWrapper_InitialState_IsNotWrapping()
    {
        var wrapper = new CursorWrapper();
        Assert.False(wrapper.IsWrapping);
    }

    [Fact]
    public void BeginWrap_WithCustomAnchor_SetsWrappingAndAnchor()
    {
        var wrapper = new CursorWrapper();
        wrapper.BeginWrap(500, 300);

        Assert.True(wrapper.IsWrapping);
        Assert.Equal((500, 300), wrapper.Anchor);

        wrapper.EndWrap();
        Assert.False(wrapper.IsWrapping);
    }

    [Fact]
    public void ApplyRelativeDelta_WhenNotWrapping_DoesNotThrow()
    {
        var wrapper = new CursorWrapper();
        // Should safely execute without throwing even if not wrapping
        var ex = Record.Exception(() => wrapper.ApplyRelativeDelta(10f, 15f));
        Assert.Null(ex);
    }

    [Fact]
    public void EndWrap_RestoresStateToNotWrapping()
    {
        var wrapper = new CursorWrapper();
        wrapper.BeginWrap(100, 100);
        Assert.True(wrapper.IsWrapping);

        wrapper.EndWrap();
        Assert.False(wrapper.IsWrapping);
    }

    [Fact]
    public void Reset_ClearsWrappingState()
    {
        var wrapper = new CursorWrapper();
        wrapper.BeginWrap(200, 200);
        Assert.True(wrapper.IsWrapping);

        wrapper.Reset();
        Assert.False(wrapper.IsWrapping);
    }
}
