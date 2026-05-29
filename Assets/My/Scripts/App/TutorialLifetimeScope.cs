using My.Scripts._01_Tutorial;
using My.Scripts.Core;
using VContainer;
using VContainer.Unity;

namespace My.Scripts.App
{
    public class TutorialLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<TutorialManager>();
            builder.RegisterComponentInHierarchy<APIManager>();
        }
    }    
}