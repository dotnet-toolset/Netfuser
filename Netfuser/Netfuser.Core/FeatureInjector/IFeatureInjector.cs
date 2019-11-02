using System.Collections.Generic;
using Netfuser.Core.Impl;
using Netfuser.Core.Impl.FeatureInjector;

namespace Netfuser.Core.FeatureInjector
{
    /// <summary>
    /// Allows to use existing methods to inject various features to be used at runtime, most commonly, to de-mangle
    /// certain parts of the target module.
    /// The algorithm is roughly as follows:
    /// <list type="number">
    /// <item><description>
    /// Once all types to be included in the target module are known, feature injector selects types suitable for injection.
    /// These are non-abstract, non-interface, non-static types without generic parameters, matching some other criteria
    /// to make sure they may be used for injection.
    /// </description></item>
    /// <item><description>
    /// Feature injector fires <see cref="FeatureInjectorEvent.HaveInjectableTypes"/>, inviting feature providers to select
    /// methods suitable for injection. Selection is score-based, i.e. features evaluate every method in the injectable types
    /// and give it a score. The higher it is - the higher the odds of the method are to be used for injection.
    /// This is done by calling the <see cref="Rate"/> method of the feature injector from within the observer of <see cref="FeatureInjectorEvent.HaveInjectableTypes"/>  
    /// </description></item>
    /// <item><description>
    /// Each feature provider sorts potentially injectable methods by their score and selects a dozen or so that will actually be used for injection.
    /// Next, feature providers create an instance of <see cref="InjectedFeature"/> for every such method. 
    /// </description></item>
    /// <item><description>
    /// Feature injector observes <see cref="NetfuserEvent.CilBodyBuilding"/> event and performs actual injection of each <see cref="InjectedFeature"/>
    /// </description></item>
    /// </list>
    /// </summary>
    public interface IFeatureInjector : IPlugin
    {
        /// <summary>
        /// List of types suitable for injection 
        /// </summary>
        IReadOnlyList<InjectableType> InjectableTypes { get; }
        
        /// <summary>
        /// Rate potentially injectable methods based on the feature's preferences
        /// </summary>
        /// <param name="feature">specific feature that requests injection</param>
        /// <returns>list of potentially injectable methods, with scores</returns>
        IEnumerable<InjectableMethod> Rate(InjectableFeature feature);
    }
}