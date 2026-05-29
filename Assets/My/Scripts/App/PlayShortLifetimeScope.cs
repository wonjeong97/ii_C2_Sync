using My.Scripts._03_PlayShort;
using My.Scripts.Core;
using My.Scripts.UI;
using VContainer;
using VContainer.Unity;

public class PlayShortLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<PlayShortManager>();
        builder.RegisterComponentInHierarchy<PlayShortEnvironment>();
        builder.RegisterComponentInHierarchy<PlayShortUIManager>();
        builder.RegisterComponentInHierarchy<InputManager>();
        builder.RegisterComponentInHierarchy<PadDotController>();
    }
}
