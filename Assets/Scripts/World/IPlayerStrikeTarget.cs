using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public interface IPlayerStrikeTarget
    {
        bool CanReceivePlayerStrike { get; }
        Vector3 StrikeTargetPosition { get; }
        void HitByPlayerPunch(Vector3 hitDirection, int punchVariant, Transform attacker);
    }
}
