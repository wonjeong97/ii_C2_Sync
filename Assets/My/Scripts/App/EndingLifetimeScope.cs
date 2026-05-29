using My.Scripts._05_Ending;
using VContainer;
using VContainer.Unity;

public class EndingLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<EndingManager>();
    }
}
