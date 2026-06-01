using My.Scripts._04_PlayLong;
using My.Scripts.Core;
using My.Scripts.UI;
using VContainer;
using VContainer.Unity;

namespace My.Scripts.App;

public class PlayLongLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {   
        builder.RegisterComponentInHierarchy<PlayLongManager>();
        builder.RegisterComponentInHierarchy<PlayLongEnvironment>();
        builder.RegisterComponentInHierarchy<PlayLongObstacleManager>();
        builder.RegisterComponentInHierarchy<PlayLongFrameManager>();
        builder.RegisterComponentInHierarchy<PadDotController>();
    }
}