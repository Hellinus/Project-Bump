using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.CorgiEngine;
using MoreMountains.Feedbacks;
using UnityEngine.Serialization;


namespace _Bump.Scripts.Player
{
    [AddComponentMenu("Corgi Engine/Character/Abilities/Bump Detection Component")]
    public class BumpDetectionComponent : MonoBehaviour
    {
        public CircleCollider2D CircleCollider { get; private set; }

        [MMReadOnly] [Tooltip("bump ability script will change this bool to control this script")]
        public bool Calculating = false;

        [MMReadOnly] [Tooltip("this script will change its value to signal bump ability script")]
        public bool CalculatingFinish = false;

        [MMReadOnly] public Vector2 FinalVector;

        public LayerMask DetectLayerMask;
        
        protected Character _character;
        protected MMStateMachine<CharacterStates.MovementStates> _movement;
        protected Vector2 _vectorTemp;

        private void Start()
        {
            _character = this.gameObject.GetComponentInParent<Character>();
            if (_character != null)
            {
                _movement = _character.MovementState;
            }
            else
            {
                Debug.LogWarning("no character found.");
            }

            if (_movement == null)
            {
                Debug.LogWarning("no movement found.");
            }

            CircleCollider = GetComponent<CircleCollider2D>();

            if (CircleCollider == null)
            {
                Debug.LogWarning("no circle collider 2D found.");
            }

            Reset();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") ||
                other.CompareTag("EditorOnly")) return;
            
            // Debug.Log(other.name);

            if ((DetectLayerMask & 1 << other.gameObject.layer) > 0)
            {
                var position = this.transform.position;
                Debug.Log(position);
                Vector2 hitPos = other.ClosestPoint(position);
                    // bounds.ClosestPoint(position);
                Debug.Log(hitPos);
                _vectorTemp.x = position.x - hitPos.x;
                _vectorTemp.y = position.y - hitPos.y;
                FinalVector += _vectorTemp;
            }
        }

        public void Reset()
        {
            Calculating = false;
            CalculatingFinish = false;
            FinalVector = new Vector2(0f, 0f);
            CircleCollider.radius = 0.1f;
        }
    }
}