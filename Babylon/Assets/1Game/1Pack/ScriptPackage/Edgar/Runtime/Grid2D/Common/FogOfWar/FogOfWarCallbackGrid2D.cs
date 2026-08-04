using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    /// This component can be attached to any object inside the room template hierarchy.
    /// This component received updates about the Fog of War state of the room.
    /// </summary>
    public abstract class FogOfWarCallbackGrid2D : MonoBehaviour
    {
        /// <summary>
        /// Called when a room starts being revealed by the Fog of War.
        /// Might be called more than once for each room.
        /// </summary>
        public virtual void OnRoomRevealing()
        {
            
        }
        
        /// <summary>
        /// Called when a room has been revealed by the Fog of War.
        /// Might be called more than once for each room.
        /// </summary>
        public virtual void OnRoomRevealed()
        {
            
        }
    }
}