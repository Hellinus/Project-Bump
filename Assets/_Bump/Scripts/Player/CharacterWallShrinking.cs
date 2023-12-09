using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.CorgiEngine;

namespace _Bump.Scripts.Player
{
    [AddComponentMenu("Corgi Engine/Character/Abilities/Character Wallshrinking")]
    public class CharacterWallShrinking : CharacterAbility
    {
        [Header("Wall Shrinking")]
        [Range(0.01f, 1)]
        public float WallShrinkingingSlowFactor = 0.2f;
        
    }
}