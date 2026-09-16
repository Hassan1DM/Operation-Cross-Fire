namespace CrossFire
{
    /// <summary>The two responsibilities a physical player can currently hold.
    /// Roles are reassigned between Player 1 / Player 2 on every Quantum Flux transition.</summary>
    public enum Role
    {
        Pilot,
        Gunner
    }

    /// <summary>Identifies a physical player/seat. Player 1 always owns the left half of the
    /// touch screen, Player 2 always owns the right half — only their Role (and therefore
    /// which control scheme is drawn in that half) changes over time.</summary>
    public enum PlayerSlot
    {
        Player1,
        Player2
    }

    /// <summary>The three round phases from the design document. Each phase carries its own
    /// difficulty multipliers, applied by <see cref="GameManager"/> at the moment of transition.</summary>
    public enum GamePhase
    {
        Patrol,
        Alert,
        Critical
    }

    /// <summary>Overall round outcome state.</summary>
    public enum GameRoundState
    {
        Playing,
        Won,
        Lost
    }
}
