using My.Scripts._02_PlayTutorial.Managers;
using My.Scripts.Core;
using My.Scripts.UI;
using VContainer;
using VContainer.Unity;

namespace My.Scripts.App
{
    public class PlayTutorialLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<PlayTutorialManager>();
            builder.RegisterComponentInHierarchy<PlayTutorialEnvironment>();
            builder.RegisterComponentInHierarchy<PlayTutorialUIManager>();
            builder.RegisterComponentInHierarchy<PadDotController>();
        }
    }    
}
