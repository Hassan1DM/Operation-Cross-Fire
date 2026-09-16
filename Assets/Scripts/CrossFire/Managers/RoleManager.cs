using System;
using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Owns the Player-1/Player-2 to Pilot/Gunner assignment. Roles identify current
    /// responsibilities; PlayerSlot identifies the physical seat (left/right touch half).
    /// GameManager calls <see cref="SwapRoles"/> as step 2 of the Quantum Flux sequence.
    /// </summary>
    public class RoleManager : MonoBehaviour
    {
        public static RoleManager Instance { get; private set; }

        /// <summary>Fired after roles have swapped: (player1Role, player2Role).</summary>
        public event Action<Role, Role> OnRolesSwapped;

        public Role Player1Role { get; private set; } = Role.Pilot;
        public Role Player2Role { get; private set; } = Role.Gunner;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public Role GetRole(PlayerSlot slot) => slot == PlayerSlot.Player1 ? Player1Role : Player2Role;

        /// <summary>Toggles both players' roles. Patrol -> Alert -> Critical alternates
        /// Pilot/Gunner each transition, matching the phase table in the design document.</summary>
        public void SwapRoles()
        {
            (Player1Role, Player2Role) = (Player2Role, Player1Role);
            OnRolesSwapped?.Invoke(Player1Role, Player2Role);
        }
    }
}
