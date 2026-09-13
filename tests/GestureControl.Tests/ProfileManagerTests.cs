using GestureControl.Actions.Profiles;
using GestureControl.Core.Models;
using Xunit;

namespace GestureControl.Tests;

public class ProfileManagerTests
{
    [Fact]
    public void DefaultProfile_IsGlobal()
    {
        var manager = new ProfileManager();
        Assert.Equal("global_default", manager.ActiveProfile.Id);
        Assert.Equal("*", manager.ActiveProfile.ProcessName);
    }

    [Fact]
    public void UpdateActiveWindow_BlenderProcess_SwitchesToBlenderProfile()
    {
        var manager = new ProfileManager();
        bool eventFired = false;
        manager.ProfileChanged += (_, p) =>
        {
            if (p.Id == "blender_default") eventFired = true;
        };

        manager.UpdateActiveWindow("Blender [C:\\Project\\scene.blend]", "blender.exe");

        Assert.Equal("blender_default", manager.ActiveProfile.Id);
        Assert.True(eventFired);
        Assert.Equal(ActionCommandType.MouseMiddleDown, manager.ActiveProfile.GestureBindings[HandGestureType.Fist].Type);
    }

    [Fact]
    public void UpdateActiveWindow_UnityProcess_SwitchesToUnityProfile()
    {
        var manager = new ProfileManager();
        manager.UpdateActiveWindow("Unity - SampleScene - PC, Mac & Linux Standalone", "Unity.exe");

        Assert.Equal("unity_default", manager.ActiveProfile.Id);
        Assert.Equal(ActionCommandType.MouseRightDown, manager.ActiveProfile.GestureBindings[HandGestureType.Fist].Type);
    }

    [Fact]
    public void UpdateActiveWindow_UnknownProcess_FallsBackToGlobal()
    {
        var manager = new ProfileManager();
        // First switch to blender
        manager.UpdateActiveWindow("Blender", "blender");
        Assert.Equal("blender_default", manager.ActiveProfile.Id);

        // Switch to Notepad
        manager.UpdateActiveWindow("Untitled - Notepad", "notepad.exe");
        Assert.Equal("global_default", manager.ActiveProfile.Id);
    }

    [Fact]
    public void SetActiveProfile_ExplicitId_SwitchesCorrectly()
    {
        var manager = new ProfileManager();
        manager.SetActiveProfile("unreal_default");

        Assert.Equal("unreal_default", manager.ActiveProfile.Id);
    }
}
