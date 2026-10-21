namespace Hypocycloid.Utils
{
    /// <summary>
    /// Reuse callbacks for a component on a pooled object. A pooled instance keeps whatever state
    /// its last life left behind, so anything that must start clean has to clear itself here
    /// rather than in <c>Awake</c>, which only ever runs once.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Runs after the instance is activated and parented, before the caller sees it.</summary>
        void OnSpawned();

        /// <summary>Runs before the instance is deactivated and returned to its pool.</summary>
        void OnDespawned();
    }
}
