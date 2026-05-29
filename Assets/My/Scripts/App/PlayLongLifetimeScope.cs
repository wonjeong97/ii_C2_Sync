using My.Scripts._04_PlayLong;
using My.Scripts.Core;
using My.Scripts.UI;
using VContainer;
using VContainer.Unity;

public class PlayLongLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {   
        builder.RegisterComponentInHierarchy<PlayLongManager>();
        builder.RegisterComponentInHierarchy<PlayLongEnvironment>();
        builder.RegisterComponentInHierarchy<PlayLongObstacleManager>();
        builder.RegisterComponentInHierarchy<PlayLongFrameManager>();
        builder.RegisterComponentInHierarchy<InputManager>();
        builder.RegisterComponentInHierarchy<PadDotController>();
    }
}
