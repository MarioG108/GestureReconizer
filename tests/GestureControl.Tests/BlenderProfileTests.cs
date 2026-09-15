using GestureControl.Core.Models;
using GestureControl.Infrastructure.Persistence;
using Xunit;

namespace GestureControl.Tests;

public class BlenderProfileTests : IDisposable
{
    private readonly string _testDir;
    private readonly ProfileStorageService _storageService;

    public BlenderProfileTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MVPGesture_BlenderTest_" + Guid.NewGuid().ToString("N"));
        _storageService = new ProfileStorageService(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void CreateBlenderProfile_InitializesWith3DNavigationDefaults()
    {
        var profile = AppProfile.CreateBlenderProfile();

        Assert.Equal("blender_default", profile.Id);
        Assert.Equal("blender", profile.ProcessName);
        Assert.True(profile.Enable3DNavigation);
        Assert.True(profile.EnableInfiniteCursorWrap);
        Assert.Equal(1.2f, profile.OrbitSensitivity);
        Assert.Equal(1.0f, profile.PanSensitivity);
        Assert.Equal(1.0f, profile.ZoomDepthSensitivity);
        Assert.Equal(0.025f, profile.PalmDepthDeadZone);

        // Check bindings for 10 gestures
        Assert.Equal(10, profile.GestureBindings.Count);
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.Fist));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.TwoFingersPeace));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.Pinch));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.SwipeLeft));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.SwipeRight));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.SwipeUp));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.SwipeDown));
    }

    [Fact]
    public async Task SaveAndLoadBlenderProfile_Preserves3DProperties()
    {
        var profile = AppProfile.CreateBlenderProfile();
        profile.OrbitSensitivity = 1.8f;
        profile.PanSensitivity = 1.4f;
        profile.ZoomDepthSensitivity = 2.0f;
        profile.PalmDepthDeadZone = 0.035f;

        await _storageService.SaveProfileAsync(profile);
        var loaded = await _storageService.LoadProfileAsync("blender_default");

        Assert.NotNull(loaded);
        Assert.Equal("blender_default", loaded.Id);
        Assert.True(loaded.Enable3DNavigation);
        Assert.True(loaded.EnableInfiniteCursorWrap);
        Assert.Equal(1.8f, loaded.OrbitSensitivity);
        Assert.Equal(1.4f, loaded.PanSensitivity);
        Assert.Equal(2.0f, loaded.ZoomDepthSensitivity);
        Assert.Equal(0.035f, loaded.PalmDepthDeadZone);
    }

    [Fact]
    public async Task EnsureDefaultProfilesAsync_CreatesBlenderProfileOnDisk()
    {
        await _storageService.EnsureDefaultProfilesAsync();

        string blenderFile = Path.Combine(_testDir, "blender_default.json");
        Assert.True(File.Exists(blenderFile));

        var loaded = await _storageService.LoadProfileAsync("blender_default");
        Assert.NotNull(loaded);
        Assert.True(loaded.Enable3DNavigation);
    }
}
