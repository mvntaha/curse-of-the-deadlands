using UnityEngine;

namespace Deadlands.Core.Combat
{
    /// <summary>Something that can be knocked off balance without necessarily taking damage (e.g. by a shove).</summary>
    public interface IStaggerable
    {
        void ForceStagger(float duration, Vector3 push);
    }
}
