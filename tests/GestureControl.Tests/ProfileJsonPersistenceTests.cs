using GestureControl.Core.Models;
using GestureControl.Infrastructure.Persistence;
using Xunit;

namespace GestureControl.Tests;

public class ProfileJsonPersistenceTests : IDisposable
{
    private readonly string _testDir;
    private readonly ProfileStorageService _storageService;

    public ProfileJsonPersistenceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MVPGesture_TestProfiles_" + Guid.NewGuid().ToString("N"));
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
        catch
        {
            // Ignore cleanup failures in temp dir
        }
    }

    [Fact]
    public async Task SaveAndLoadProfile_PreservesAll10GesturesAndConfigurations()
    {
        // 1. Arrange: create profile with 10 gestures (6 static + 4 dynamic)
        var profile = AppProfile.CreateDefaultGlobal();
        profile.DeadZoneRadius = 0.025f;
        profile.MouseSpeedMultiplier = 2.2f;
        profile.SmoothingFactor = 0.75f;
        profile.MinimumHoldDurationMs = 150;
        profile.MinSwipeDistance = 0.18f;
        profile.MinSwipeVelocity = 0.45f;
        profile.SwipeCooldownMs = 450;

        // Ensure 10 distinct gestures are mapped
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.OpenHand));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.Fist));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.Pinch));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.IndexPoint));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.TwoFingersPeace));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.LateralPalm));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.SwipeLeft));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.SwipeRight));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.SwipeUp));
        Assert.True(profile.GestureBindings.ContainsKey(HandGestureType.SwipeDown));

        // 2. Act: save to disk and reload
        await _storageService.SaveProfileAsync(profile);
        var loaded = await _storageService.LoadProfileAsync(profile.Id);

        // 3. Assert
        Assert.NotNull(loaded);
        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal(profile.Name, loaded.Name);
        Assert.Equal(profile.DeadZoneRadius, loaded.DeadZoneRadius);
        Assert.Equal(profile.MouseSpeedMultiplier, loaded.MouseSpeedMultiplier);
        Assert.Equal(profile.SmoothingFactor, loaded.SmoothingFactor);
        Assert.Equal(profile.MinimumHoldDurationMs, loaded.MinimumHoldDurationMs);
        Assert.Equal(profile.MinSwipeDistance, loaded.MinSwipeDistance);
        Assert.Equal(profile.MinSwipeVelocity, loaded.MinSwipeVelocity);
        Assert.Equal(profile.SwipeCooldownMs, loaded.SwipeCooldownMs);
        Assert.Equal(10, loaded.GestureBindings.Count);

        // Check dynamic swipe bindings
        Assert.Equal(profile.GestureBindings[HandGestureType.SwipeLeft].Type, loaded.GestureBindings[HandGestureType.SwipeLeft].Type);
        Assert.Equal(profile.GestureBindings[HandGestureType.SwipeRight].Type, loaded.GestureBindings[HandGestureType.SwipeRight].Type);
        Assert.Equal(profile.GestureBindings[HandGestureType.SwipeUp].Type, loaded.GestureBindings[HandGestureType.SwipeUp].Type);
        Assert.Equal(profile.GestureBindings[HandGestureType.SwipeDown].Type, loaded.GestureBindings[HandGestureType.SwipeDown].Type);
    }

    [Fact]
    public async Task EnsureDefaultProfilesAsync_CreatesGlobalAndBlenderFiles()
    {
        await _storageService.EnsureDefaultProfilesAsync();

        string globalFile = Path.Combine(_testDir, "global_default.json");
        string blenderFile = Path.Combine(_testDir, "blender_default.json");

        Assert.True(File.Exists(globalFile), $"File {globalFile} should exist");
        Assert.True(File.Exists(blenderFile), $"File {blenderFile} should exist");

        var allProfiles = await _storageService.LoadAllProfilesAsync();
        Assert.True(allProfiles.Count >= 2);
    }
}
