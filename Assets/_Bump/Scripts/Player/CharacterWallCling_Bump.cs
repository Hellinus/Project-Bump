using MoreMountains.CorgiEngine;
using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Bump.Scripts.Player
{
    [MMHiddenProperties("AbilityStopFeedbacks")]
    [AddComponentMenu("Corgi Engine/Character/Abilities/Character WallCling_Bump")] 
    public class CharacterWallCling_Bump : CharacterAbility
    {
	    public enum ForceModes { AddForce, SetForce }
	    
        public override string HelpBoxText() { return "Add this component to your character and it'll be able to cling to walls, slowing down its fall, and bump from wall. Here you can define the slow factor (close to 0 : super slow, 1 : normal fall) and the tolerance (to account for tiny holes in the wall."; }
        
        [Header("Wall Clinging")]
        [Tooltip("the slow factor when wall clinging")]
        [Range(0.01f, 1)]
        public float WallClingingSlowFactor = 0.2f;
        [Tooltip("the vertical offset to apply to raycasts for wall clinging")]
        public float RaycastVerticalOffset = 0f;
        [Tooltip("the tolerance applied to compensate for tiny irregularities in the wall (slightly misplaced tiles for example)")]
        public float WallClingingTolerance = 0.3f;
        [Tooltip("if this is true, vertical forces will be reset on entry")]
        public bool ResetVerticalForceOnEntry = true;
        
        [Header("Automation")]
        [Tooltip("if this is set to true, you won't need to press the opposite direction to wall cling, it'll be automatic anytime the character faces a wall")]
        public bool InputIndependent = false;

        [Header("Wall Bump")]

        
        [Header("Wall Jump")]
        
        [Tooltip("the force of a walljump")]
        public ForceModes ForceMode = ForceModes.AddForce;
        public Vector2 WallJumpForce = new Vector2(10,4);
        public bool LimitNumberOfWalljumps = true;
        public int MaximumNumberOfWalljumps = 1;
        public int NumberOfWalljumpsLeft = 0;
        [Tooltip("if this is true, the character will be forced to flip towards the jump direction on the jump frame")]
        public bool ForceFlipTowardsDirection = false;
        public bool WallJumpHappenedThisFrame { get; set; }
        
        
        [Header("Debug")]
        [MMReadOnly] public float _bumpFactor = 0f;
        [MMReadOnly] public float _bumpRadius = 0f;
        [MMReadOnly] public float _bumpForce = 0f;
        
        public bool IsFacingRightWhileWallClinging { get; set; }

        protected CharacterBump _characterBump;
        
        protected CharacterStates.MovementStates _stateLastFrame;
        protected RaycastHit2D _raycast;
        protected WallClingingOverride _wallClingingOverride;
       
        protected float _bumpPressDownTime = 0f;
        protected bool _bumpDetecting = false;
        protected BumpDetectionComponent _bumpDetection;
        protected bool _hasWallJumped = false;
        protected Vector2 _wallJumpVector;

        protected override void Initialization()
        {
	        base.Initialization();
	        _bumpDetection = GetComponentInChildren<BumpDetectionComponent>();
	        _characterBump = _character?.FindAbility<CharacterBump>();
	        ResetNumberOfWalljumpsLeft();
        }
        
        /// <summary>
        /// Resets the amount of walljumps left
        /// </summary>
        public virtual void ResetNumberOfWalljumpsLeft()
        {
	        NumberOfWalljumpsLeft = MaximumNumberOfWalljumps;
        }

        /// <summary>
        /// Checks the input to see if we should enter the WallClinging state
        /// </summary>
        protected override void HandleInput()
        {
            WallClinging();
            
            WallJumpHappenedThisFrame = false;

            if ((_movement.CurrentState != CharacterStates.MovementStates.WallClinging)
                && (_movement.CurrentState != CharacterStates.MovementStates.WallShrinking)) return;
            
            if (_inputManager.BumpButton.State.CurrentState == MMInput.ButtonStates.ButtonPressed)
            {
	            _bumpPressDownTime += Time.deltaTime;
	            _movement.ChangeState(CharacterStates.MovementStates.WallShrinking);
	            if (_characterHorizontalMovement != null)
	            {
		            if (_characterBump.BumpAuthorized && _bumpPressDownTime > _characterBump.BumpPressDownTimeMin)
		            {
			            _characterHorizontalMovement.MovementSpeed = _characterBump.ShrinkSpeed;
		            }
	            }
            }
            
            // we handle button release
            if (_inputManager.BumpButton.State.CurrentState == MMInput.ButtonStates.ButtonUp)
            {
	            if (_characterHorizontalMovement != null)
	            {
		            _characterHorizontalMovement.ResetHorizontalSpeed();
	            }

	            if (_bumpPressDownTime >= _characterBump.BumpPressDownTimeMax)
	            {
		            _bumpPressDownTime = _characterBump.BumpPressDownTimeMax;
	            }
	            else if (_bumpPressDownTime <= _characterBump.BumpPressDownTimeMin)
	            {
		            WallJumpStart();
		            _bumpPressDownTime = 0f;
		            return;
	            }
	            
	            _bumpFactor = (_bumpPressDownTime - _characterBump.BumpPressDownTimeMin) /
	                          (_characterBump.BumpPressDownTimeMax - _characterBump.BumpPressDownTimeMin);
	            _bumpRadius = _bumpFactor * (_characterBump.BumpDetectRadiusMax - _characterBump.BumpDetectRadiusMin) + _characterBump.BumpDetectRadiusMin;
	            _bumpForce = _bumpFactor * (_characterBump.BumpForceMax - _characterBump.BumpForceMin) + _characterBump.BumpForceMin;

	            _bumpDetecting = true;
	            _bumpPressDownTime = 0f;
            }
        }
        
        /// <summary>
        /// Every frame, checks if the wallclinging state should be exited
        /// </summary>
        public override void ProcessAbility()
        {
            base.ProcessAbility();
            HandleDetection();
            UpdateController();
            ExitWallClinging();
            WallClingingLastFrame();
            if (_controller.State.IsGrounded)
            {
	            ResetNumberOfWalljumpsLeft();
            }
        }
        
        protected void HandleDetection()
        {
	        if (_bumpDetecting == false) return;
	        
	        if (_bumpDetection.CircleCollider.radius < _bumpRadius - 0.05f)
	        {
		        _bumpDetection.CircleCollider.radius = Mathf.Lerp(_bumpDetection.CircleCollider.radius, _bumpRadius, _characterBump.LerpValue);
	        }
	        else
	        {
		        WallBumpStart();
		        _bumpDetecting = false;
	        }
        }

        protected virtual void WallBumpStart()
        {
	        if (!_characterBump.EvaluateBumpConditions())
	        {
		        return;
	        }
	        
	        // todo: why gravity connects with this???
	        //_movement.ChangeState(CharacterStates.MovementStates.WallBumping);
	        
	        _characterHorizontalMovement.ResetHorizontalSpeed();
	        
	        // we trigger a character event
	        MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.WallBump);
	        
	        _condition.ChangeState(CharacterStates.CharacterConditions.Normal);
	        _controller.GravityActive(true);
	        _controller.CollisionsOn ();
	        
	        Vector2 v = _bumpDetection.FinalVector.normalized * _bumpForce;
	        _bumpDetection.Reset();
	        _controller.AddHorizontalForce(v.x);
	        if (v.y >= 0.1f)
	        {
		        _controller.SetVerticalForce(v.y);
	        }
	        else
	        {
		        _controller.AddVerticalForce(v.y);
	        }
        }

        protected void WallJumpStart()
        {
	        if (!AbilityAuthorized
	            || _condition.CurrentState != CharacterStates.CharacterConditions.Normal)
	        {
		        return;
	        }
	        
	        // wall jump
	        float wallJumpDirection;
	        
	        if (!EvaluateWallJumpConditions())
	        {
		        return;
	        }
	        
	        _movement.ChangeState(CharacterStates.MovementStates.WallJumping);
	        MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.WallJump);
	        
	        _condition.ChangeState(CharacterStates.CharacterConditions.Normal);
	        _controller.GravityActive(true);
	        _controller.SlowFall (0f);	
	        
	        _hasWallJumped = true;
	        WallJumpHappenedThisFrame = true;
	        
	        // If the character is colliding to the right with something (probably the wall)
	        wallJumpDirection = IsFacingRightWhileWallClinging ? -1f : 1f;
	        _characterHorizontalMovement?.SetAirControlDirection(wallJumpDirection);
	        _wallJumpVector.x = wallJumpDirection * WallJumpForce.x;
	        _wallJumpVector.y = Mathf.Sqrt( 2f * WallJumpForce.y * Mathf.Abs(_controller.Parameters.Gravity));
	        
	        if (ForceMode == ForceModes.AddForce)
	        {
		        _controller.AddForce(_wallJumpVector);
	        }
	        else
	        {
		        _controller.SetForce(_wallJumpVector);
	        }
	        
	        if (ForceFlipTowardsDirection)
	        {
		        if (_wallJumpVector.x > 0)
		        {
			        _character.Face(Character.FacingDirections.Right);    
		        }
		        else
		        {
			        _character.Face(Character.FacingDirections.Left);
		        }
	        }
	        
	        if (LimitNumberOfWalljumps)
	        {
		        NumberOfWalljumpsLeft--;
	        }
        }
        
        
        public virtual bool EvaluateWallJumpConditions()
        {
	        if (LimitNumberOfWalljumps && NumberOfWalljumpsLeft <= 0)
	        {
		        Debug.Log("1");
		        return false;
	        }

	        if (_hasWallJumped)
	        {
		        Debug.Log("2");
		        return false;
	        }

	        if (_controller.State.IsGrounded)
	        {
		        Debug.Log("3");
		        return false;
	        }
			
	        if (_movement.CurrentState != CharacterStates.MovementStates.WallClinging
	            && _movement.CurrentState != CharacterStates.MovementStates.WallShrinking)
	        {
		        Debug.Log(_movement.CurrentState);
		        return false;	
	        }

	        return true;
        }
        
        /// <summary>
        /// At the end of the frame, we store the current state for comparison use in the next frame
        /// </summary>
        public override void LateProcessAbility()
        {
            base.LateProcessAbility();
            _stateLastFrame = _movement.CurrentState;
        }
        
        /// <summary>
        /// Makes the player stick to a wall when jumping
        /// </summary>
        protected virtual void WallClinging()
        {
            if (!AbilityAuthorized
                || (_condition.CurrentState != CharacterStates.CharacterConditions.Normal)
                || (_controller.State.IsGrounded)
                || (_controller.State.ColliderResized)
                || (_controller.Speed.y >= 0) )
            {
                return;
            }
            
            if (InputIndependent)
            {
                if (TestForWall())
                {
                    EnterWallClinging();
                }
            }
            else
            {
                if (_horizontalInput <= -_inputManager.Threshold.x)
                {
                    if (TestForWall(-1))
                    {
                        EnterWallClinging();
                    }
                }
                else if (_horizontalInput >= _inputManager.Threshold.x)
                {
                    if (TestForWall(1))
                    {
                        EnterWallClinging();
                    }
                }
            }            
        }
        
        /// <summary>
        /// Casts a ray to check if we're facing a wall
        /// </summary>
        /// <returns></returns>
        protected virtual bool TestForWall()
        {
            if (_character.IsFacingRight)
            {
                return TestForWall(1);
            }
            else
            {
                return TestForWall(-1);
            }
        }
        
        protected virtual bool TestForWall(int direction)
        {
            // we then cast a ray to the direction's the character is facing, in a down diagonal.
            // we could use the controller's IsCollidingLeft/Right for that, but this technique 
            // compensates for walls that have small holes or are not perfectly flat
            Vector3 raycastOrigin = _characterTransform.position;
            Vector3 raycastDirection;
            if (direction > 0)
            {
                raycastOrigin = raycastOrigin + _characterTransform.right * _controller.Width() / 2 + _characterTransform.up * RaycastVerticalOffset;
                raycastDirection = _characterTransform.right - _characterTransform.up;
            }
            else
            {
                raycastOrigin = raycastOrigin - _characterTransform.right * _controller.Width() / 2 + _characterTransform.up * RaycastVerticalOffset;
                raycastDirection = -_characterTransform.right - _characterTransform.up;
            }

            // we cast our ray 
            _raycast = MMDebug.RayCast(raycastOrigin, raycastDirection, WallClingingTolerance, _controller.PlatformMask & ~(_controller.OneWayPlatformMask | _controller.MovingOneWayPlatformMask), Color.black, _controller.Parameters.DrawRaycastsGizmos);

            // we check if the ray hit anything. If it didn't, or if we're not moving in the direction of the wall, we exit
            return _raycast;
        }
        
        /// <summary>
        /// Enters the wall clinging state
        /// </summary>
        protected virtual void EnterWallClinging()
        {
            // we check for an override
            if (_controller.CurrentWallCollider != null)
            {
                _wallClingingOverride = _controller.CurrentWallCollider.gameObject.MMGetComponentNoAlloc<WallClingingOverride>();
            }
            else if (_raycast.collider != null)
            {
                _wallClingingOverride = _raycast.collider.gameObject.MMGetComponentNoAlloc<WallClingingOverride>();
            }
            
            if (_wallClingingOverride != null)
            {
                // if we can't wallcling to this wall, we do nothing and exit
                if (!_wallClingingOverride.CanWallClingToThis)
                {
                    return;
                }
                _controller.SlowFall(_wallClingingOverride.WallClingingSlowFactor);
            }
            else
            {
                // we slow the controller's fall speed
                _controller.SlowFall(WallClingingSlowFactor);
            }

            // if we weren't wallclinging before this frame, we start our sounds
            if (((_movement.CurrentState != CharacterStates.MovementStates.WallClinging) || (_movement.CurrentState != CharacterStates.MovementStates.WallShrinking))
                && !_startFeedbackIsPlaying
                )
            {
                if (ResetVerticalForceOnEntry)
                {
                    _controller.SetVerticalForce(0f);
                }
                // we start our feedbacks
                PlayAbilityStartFeedbacks();
                MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.WallCling, MMCharacterEvent.Moments.Start);
            }

            if (_movement.CurrentState == CharacterStates.MovementStates.WallShrinking)
            {
	            ;
            }
            else
            {
	            _movement.ChangeState(CharacterStates.MovementStates.WallClinging);
            }

            IsFacingRightWhileWallClinging = _character.IsFacingRight;
        }
        
        /// <summary>
		/// If the character is currently wallclinging, checks if we should exit the state
		/// </summary>
		protected virtual void ExitWallClinging()
		{
			if (_movement.CurrentState == CharacterStates.MovementStates.WallClinging || _movement.CurrentState == CharacterStates.MovementStates.WallShrinking)
			{
				// we prepare a boolean to store our exit condition value
				bool shouldExit = (_controller.State.IsGrounded) // if the character is grounded
				                  || (_controller.Speed.y > 0); // or it's moving up

				// we then cast a ray to the direction's the character is facing, in a down diagonal.
				// we could use the controller's IsCollidingLeft/Right for that, but this technique 
				// compensates for walls that have small holes or are not perfectly flat
				Vector3 raycastOrigin = _characterTransform.position;
				Vector3 raycastDirection;
				if (_character.IsFacingRight) 
				{ 
					raycastOrigin = raycastOrigin + _characterTransform.right * _controller.Width()/ 2 + _characterTransform.up * RaycastVerticalOffset;
					raycastDirection = _characterTransform.right - _characterTransform.up; 
				}
				else
				{
					raycastOrigin = raycastOrigin - _characterTransform.right * _controller.Width()/ 2 + _characterTransform.up * RaycastVerticalOffset;
					raycastDirection = - _characterTransform.right - _characterTransform.up;
				}
                				
				// we check if the ray hit anything. If it didn't, or if we're not moving in the direction of the wall, we exit
				if (!InputIndependent)
				{
					// we cast our ray 
					RaycastHit2D hit = MMDebug.RayCast(raycastOrigin, raycastDirection, WallClingingTolerance, _controller.PlatformMask & ~(_controller.OneWayPlatformMask | _controller.MovingOneWayPlatformMask), Color.black, _controller.Parameters.DrawRaycastsGizmos);
                    
					if (_character.IsFacingRight)
					{
						if ((!hit) || (_horizontalInput <= _inputManager.Threshold.x))
						{
							shouldExit = true;
						}
					}
					else
					{
						if ((!hit) || (_horizontalInput >= -_inputManager.Threshold.x))
						{
							shouldExit = true;
						}
					}
				}
				else
				{
					if (_raycast.collider == null)
					{
						shouldExit = true;
					}
				}
				
				if (shouldExit)
				{
					ProcessExit();
				}
			}

			if ((((_stateLastFrame == CharacterStates.MovementStates.WallClinging) && (_movement.CurrentState != CharacterStates.MovementStates.WallClinging))
			     || ((_stateLastFrame == CharacterStates.MovementStates.WallShrinking) && (_movement.CurrentState != CharacterStates.MovementStates.WallShrinking)))
			    && _startFeedbackIsPlaying)
			{
				// we play our exit feedbacks
				StopStartFeedbacks();
				PlayAbilityStopFeedbacks();
				MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.WallCling, MMCharacterEvent.Moments.End);
			}
		}
        
        protected virtual void ProcessExit()
        {
	        // if we're not wallclinging anymore, we reset the slowFall factor, and reset our state.
	        _controller.SlowFall(0f);
	        // we reset the state
	        _movement.ChangeState(CharacterStates.MovementStates.Idle);
        }
        
        /// <summary>
        /// This methods tests if we were wallcling previously, and if so, resets the slowfall factor and stops the wallclinging sound
        /// </summary>
        protected virtual void WallClingingLastFrame()
        {
	        if ((((_stateLastFrame == CharacterStates.MovementStates.WallClinging) && (_movement.CurrentState != CharacterStates.MovementStates.WallClinging))
	             || ((_stateLastFrame == CharacterStates.MovementStates.WallShrinking) && (_movement.CurrentState != CharacterStates.MovementStates.WallShrinking)))
	            && _startFeedbackIsPlaying)
	        {
		        _controller.SlowFall (0f);	
		        StopStartFeedbacks();
	        }
        }
        
        protected override void OnDeath()
        {
	        base.OnDeath();
	        ProcessExit();
        }
        
        protected void LateUpdate()
        {
	        if ((_character.MovementState.CurrentState == CharacterStates.MovementStates.WallClinging)
	            || (_character.MovementState.CurrentState == CharacterStates.MovementStates.WallShrinking))
	        {
		        _hasWallJumped = false;
	        }
        }
        
        /// <summary>
        /// Updates the controller state based on our current bumping state
        /// </summary>
        protected virtual void UpdateController()
        {
	        _controller.State.IsWallShrinking = (_movement.CurrentState == CharacterStates.MovementStates.WallShrinking);
        }
        
        /// <summary>
        /// On reset ability, we cancel all the changes made
        /// </summary>
        public override void ResetAbility()
        {
	        base.ResetAbility();
	        if (_condition.CurrentState == CharacterStates.CharacterConditions.Normal)
	        {
		        ProcessExit();	
	        }

	        // if (_animator != null)
	        // {
		       //  MMAnimatorExtensions.UpdateAnimatorBool(_animator, _wallClingingAnimationParameter, false, _character._animatorParameters, _character.PerformAnimatorSanityChecks);	
	        // }
        }
        
    }
}
