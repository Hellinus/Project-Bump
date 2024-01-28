using MoreMountains.CorgiEngine;
using MoreMountains.Tools;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Bump.Scripts.Player
{
	[MMHiddenProperties("AbilityStopFeedbacks")]
	[AddComponentMenu("Corgi Engine/Character/Abilities/Character Bump")] 
    public class CharacterBump : CharacterAbility
    {
        /// the possible bump restrictions
        public enum BumpBehavior
        {
            CanBumpOnGround,
            CanBumpOnGroundAndFromLadders,
            CanBumpAnywhere,
            CantBump
        }

        /// the possible jump restrictions
        public enum JumpBehavior
        {
	        CanJumpOnGround,
	        CanJumpOnGroundAndFromLadders,
	        CanJumpAnywhere,
	        CantJump,
	        CanJumpAnywhereAnyNumberOfTimes
        }
        
        [Header("Bump Behaviour")]
        
        public BumpBehavior BumpRestrictions = BumpBehavior.CanBumpAnywhere;
        public float ShrinkSpeed = 1f;
        [Tooltip("if this is true, camera offset will be reset on bump")]
        public bool ResetCameraOffsetOnBump = false;
        
        [Header("Bump Force")]
        
        public float BumpForceMin = 8f;
        public float BumpForceMax = 20f;
        
        [Header("Bump Press Down Time")]
        
        public float BumpPressDownTimeMin = 0.2f;
        public float BumpPressDownTimeMax = 0.7f;
        
        [Header("Bump Detect Radius")]
        
        public float BumpDetectRadiusMin = 0.7f;
        public float BumpDetectRadiusMax = 1.5f;
        public float LerpValue = 0.5f;

        [Header("Jump Behaviour")]
        
        public float JumpForce = 9f;
        [Tooltip("basic rules for jumps : where can the player jump ?")]
        public JumpBehavior JumpRestrictions = JumpBehavior.CanJumpAnywhere;
        public int NumberOfJumps = 1;
        [Tooltip("if this is true, this character can jump down one way platforms by doing down + bump")]
        public bool CanJumpDownOneWayPlatforms = true;
        [MMReadOnly]
        [Tooltip("the number of jumps left to the character")]
        public int NumberOfJumpsLeft;
        [Tooltip("a timeframe during which, after leaving the ground, the character can still trigger a jump")]
        public float CoyoteTime = 0f;
        [Tooltip("the minimum time in the air allowed when jumping - this is used for pressure controlled jumps")]
        public float JumpMinimumAirTime = 0.1f;
        
        [Header("Collisions")]
        
        [Tooltip("duration (in seconds) we need to disable collisions when bumping down a 1 way platform")]
        public float OneWayPlatformsBumpCollisionOffDuration = 0.3f;
        [Tooltip("duration (in seconds) we need to disable collisions when bumping off a moving platform")]
        public float MovingPlatformsBumpCollisionOffDuration = 0.05f;

        [Header("Debug")]
        [MMReadOnly] public float _bumpFactor = 0f;
        [MMReadOnly] public float _bumpRadius = 0f;
        [MMReadOnly] public float _bumpForce = 0f;
        
        protected float _bumpPressDownTime = 0f;
        protected CharacterCrouch _characterCrouch = null;
        protected CharacterButtonActivation _characterButtonActivation = null;
        protected CharacterLadder _characterLadder = null;
        protected bool _jumpButtonReleased = false;
        protected int _initialNumberOfJumps;
        protected bool _jumpingDownFromOneWayPlatform = false;
        protected float _lastJumpAt = 0;
        protected bool _coyoteTime = false;
        protected float _lastTimeGrounded = 0f;
        protected bool _bumpDetecting = false;
        protected BumpDetectionComponent _bumpDetection;
        
        // animation parameters
        protected const string _shrinkingAnimationParameterName = "Shrinking";
        protected const string _shrinkingTimeAnimationParameterName = "ShrinkingTime";
        protected const string _bumpingAnimationParameterName = "Bumping";
        protected const string _hitTheGroundAnimationParameterName = "HitTheGround";
        // protected const string _numberOfJumpsLeftParameterName = "NumberOfJumpsLeft";
        protected int _shrinkingAnimationParameter;
		protected int _shrinkingTimeAnimationParameter;
        protected int _bumpingAnimationParameter;
        protected int _hitTheGroundAnimationParameter;
        // protected int _numberOfJumpsLeftAnimationParameter;
        
        /// <summary>
        /// On Start() we reset our number of bumps
        /// </summary>
        protected override void Initialization()
        {
            base.Initialization();
            ResetNumberOfJumps();
            _bumpDetection = GetComponentInChildren<BumpDetectionComponent>();
            _characterHorizontalMovement = _character?.FindAbility<CharacterHorizontalMovement>();
            _characterCrouch = _character?.FindAbility<CharacterCrouch>();
            _characterButtonActivation = _character?.FindAbility<CharacterButtonActivation>();
            _characterLadder = _character?.FindAbility<CharacterLadder>();
            ResetInitialNumberOfJumps();

            if (_bumpDetection == null)
            {
	            Debug.LogWarning("Bump Detection not found.");
            }
        }
        
        /// <summary>
        /// At the beginning of each cycle we check if we've just pressed or released the jump button
        /// </summary>
        protected override void HandleInput()
        {
	        _jumpButtonReleased = (_inputManager.JumpButton.State.CurrentState != MMInput.ButtonStates.ButtonPressed);
	        
	        if((_movement.CurrentState == CharacterStates.MovementStates.WallClinging)
	           || (_movement.CurrentState == CharacterStates.MovementStates.WallBumping)
	           || (_movement.CurrentState == CharacterStates.MovementStates.WallShrinking)
	           || (_movement.CurrentState == CharacterStates.MovementStates.WallJumping)) return;
	        
	        // we handle regular button presses
	        if (_inputManager.BumpButton.State.CurrentState == MMInput.ButtonStates.ButtonPressed)
	        {
		        _bumpPressDownTime += Time.deltaTime;
		        _movement.ChangeState(CharacterStates.MovementStates.Shrinking);
		        if (_characterHorizontalMovement != null)
		        {
			        if (BumpAuthorized && _bumpPressDownTime > BumpPressDownTimeMin)
			        {
				        _characterHorizontalMovement.MovementSpeed = ShrinkSpeed;
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
		        
		        if (_bumpPressDownTime >= BumpPressDownTimeMax)
		        {
			        _bumpPressDownTime = BumpPressDownTimeMax;
		        }
		        else if(_bumpPressDownTime <= BumpPressDownTimeMin)
		        {
			        JumpStart();
			        _bumpPressDownTime = 0f;
			        return;
		        }
			
		        _bumpFactor = (_bumpPressDownTime - BumpPressDownTimeMin) / (BumpPressDownTimeMax - BumpPressDownTimeMin);
		        _bumpRadius = _bumpFactor * (BumpDetectRadiusMax - BumpDetectRadiusMin) + BumpDetectRadiusMin;
		        _bumpForce = _bumpFactor * (BumpForceMax - BumpForceMin) + BumpForceMin;
		        
		        _bumpDetecting = true;
		        // Debug.Log(_bumpPressDownTime);
		        _bumpPressDownTime = 0f;
	        }
        }
        
        /// <summary>
        /// Every frame we perform a number of checks related to bump
        /// </summary>
        public override void ProcessAbility()
        {
            base.ProcessAbility();
			HandleDetection();
			UpdateController();
			// if we're grounded, and have jumped a while back but still haven't gotten our jumps back, we reset them
			if ((_controller.State.IsGrounded) && (Time.time - _lastJumpAt > JumpMinimumAirTime) && (NumberOfJumpsLeft < NumberOfJumps))
			{
				ResetNumberOfJumps();
			}
			// we store the last timestamp at which the character was grounded
			if (_controller.State.IsGrounded)
			{
				_lastTimeGrounded = Time.time;
			}
        }

        protected void HandleDetection()
        {
	        if (_bumpDetecting == false) return;
	        
	        if (_bumpDetection.CircleCollider.radius < _bumpRadius - 0.05f)
	        {
		        _bumpDetection.CircleCollider.radius = Mathf.Lerp(_bumpDetection.CircleCollider.radius, _bumpRadius, LerpValue);
	        }
	        else
	        {
		        BumpStart();
		        _bumpDetecting = false;
	        }
	        
        }
        
        /// <summary>
		/// Causes the character to start bumping.
		/// </summary>
        protected virtual void BumpStart()
		{
			if (!EvaluateBumpConditions())
			{
				return;
			}
			
			_movement.ChangeState(CharacterStates.MovementStates.Bumping);
			
			// we reset our walking speed
			if ((_movement.CurrentState == CharacterStates.MovementStates.Crawling)
			    || (_movement.CurrentState == CharacterStates.MovementStates.Crouching)
			    || (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing))
			{
				_characterHorizontalMovement.ResetHorizontalSpeed();
			}
			
			// TODO: Handle The Ladder
			if (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing)
			{
				_characterLadder.GetOffTheLadder();
				_characterLadder.BumpFromLadder();
			}
						
			// we trigger a character event
			MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.Bump);
			
			_condition.ChangeState(CharacterStates.CharacterConditions.Normal);
			_controller.GravityActive(true);
			_controller.CollisionsOn ();
			
			Vector2 v = _bumpDetection.FinalVector.normalized * _bumpForce;
			_bumpDetection.Reset();
			_controller.AddHorizontalForce(v.x);
			// Debug.Log(v.y);
			if (v.y >= 0.1f)
			{
				_controller.SetVerticalForce(v.y);
			}
			else
			{
				_controller.AddVerticalForce(v.y);
			}
		}
        
        
	    /// <summary>
		/// Evaluates the bump conditions to determine whether or not a bump can occur
		/// </summary>
		/// <returns><c>true</c>, if bump conditions was evaluated, <c>false</c> otherwise.</returns>
		public virtual bool EvaluateBumpConditions()
		{
			bool onAOneWayPlatform = false;
			if (_controller.StandingOn != null)
			{
				onAOneWayPlatform = (_controller.OneWayPlatformMask.MMContains(_controller.StandingOn.layer)
				                     || _controller.MovingOneWayPlatformMask.MMContains(_controller.StandingOn.layer));
			}

			if ( !AbilityAuthorized  // if the ability is not permitted
			     || !BumpAuthorized // if jumps are not authorized right now
			     || (!_controller.CanGoBackToOriginalSize() && !onAOneWayPlatform)
			     || ((_condition.CurrentState != CharacterStates.CharacterConditions.Normal) // or if we're not in the normal stance
			         && (_condition.CurrentState != CharacterStates.CharacterConditions.ControlledMovement))
			     || (_movement.CurrentState == CharacterStates.MovementStates.Jetpacking) // or if we're jetpacking
			     || (_movement.CurrentState == CharacterStates.MovementStates.Dashing) // or if we're dashing
			     || (_movement.CurrentState == CharacterStates.MovementStates.Pushing) // or if we're pushing
			     || (_controller.State.IsCollidingAbove && !onAOneWayPlatform)) // or if we're colliding with the ceiling
			{
				return false;
			}

			// if we're in a button activated zone and can interact with it
			if (_characterButtonActivation != null)
			{
				if (_characterButtonActivation.AbilityAuthorized
				    && _characterButtonActivation.PreventBumpWhenInZone
				    && _characterButtonActivation.InButtonActivatedZone
				    && !_characterButtonActivation.InButtonAutoActivatedZone)
				{
					return false;
				}
				if (_characterButtonActivation.InBumpPreventingZone)
				{
					return false;
				}
			}

			// if we're crouching and don't have enough space to stand we do nothing and exit
			if ((_movement.CurrentState == CharacterStates.MovementStates.Crouching) || (_movement.CurrentState == CharacterStates.MovementStates.Crawling))
			{				
				if (_characterCrouch != null)
				{
					if (_characterCrouch.InATunnel && (_verticalInput >= -_inputManager.Threshold.y))
					{
						return false;
					}
				}
			}
			

			if (_inputManager != null)
			{
				// if the character is standing on a moving platform and not pressing the down button,
				if (_controller.State.IsGrounded)
				{
					BumpFromMovingPlatform();
				}
			}	

			return true;
		}
        
        /// <summary>
        /// Causes the character to start jumping.
        /// </summary>
        public virtual void JumpStart()
        {
	        if (!EvaluateJumpConditions())
	        {
		        return;
	        }
			
	        // we reset our walking speed
	        if ((_movement.CurrentState == CharacterStates.MovementStates.Crawling)
	            || (_movement.CurrentState == CharacterStates.MovementStates.Crouching)
	            || (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing))
	        {
		        _characterHorizontalMovement.ResetHorizontalSpeed();
	        }
			
	        if (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing)
	        {
	        	_characterLadder.GetOffTheLadder();
	        	_characterLadder.BumpFromLadder();
	        }

	        _lastJumpAt = Time.time;
	        
	        // if we're still here, the jump will happen
	        // we set our current state to Jumping
	        _movement.ChangeState(CharacterStates.MovementStates.Jumping);

	        // we trigger a character event
	        MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.Jump);
	        
	        // we start our feedbacks
	        if ((_controller.State.IsGrounded) || _coyoteTime) 
	        {
		        PlayAbilityStartFeedbacks();
	        }
	        
	        _condition.ChangeState(CharacterStates.CharacterConditions.Normal);
	        _controller.GravityActive(true);
	        _controller.CollisionsOn ();
	        
	        // we decrease the number of jumps left
	        NumberOfJumpsLeft--;
	        _controller.SetVerticalForce(JumpForce);
        }
        
        /// <summary>
		/// Evaluates the jump conditions to determine whether or not a jump can occur
		/// </summary>
		/// <returns><c>true</c>, if jump conditions was evaluated, <c>false</c> otherwise.</returns>
		protected virtual bool EvaluateJumpConditions()
		{
			bool onAOneWayPlatform = false;
			if (_controller.StandingOn != null)
			{
				onAOneWayPlatform = (_controller.OneWayPlatformMask.MMContains(_controller.StandingOn.layer)
				                     || _controller.MovingOneWayPlatformMask.MMContains(_controller.StandingOn.layer));
			}

			if ( !AbilityAuthorized  // if the ability is not permitted
			     || !JumpAuthorized // if jumps are not authorized right now
			     || (!_controller.CanGoBackToOriginalSize() && !onAOneWayPlatform)
			     || ((_condition.CurrentState != CharacterStates.CharacterConditions.Normal) // or if we're not in the normal stance
			         && (_condition.CurrentState != CharacterStates.CharacterConditions.ControlledMovement))
			     || (_movement.CurrentState == CharacterStates.MovementStates.Jetpacking) // or if we're jetpacking
			     || (_movement.CurrentState == CharacterStates.MovementStates.Dashing) // or if we're dashing
			     || (_movement.CurrentState == CharacterStates.MovementStates.Pushing) // or if we're pushing                
			     || ((_movement.CurrentState == CharacterStates.MovementStates.WallClinging))
			     || (_controller.State.IsCollidingAbove && !onAOneWayPlatform)) // or if we're colliding with the ceiling
			{
				return false;
			}

			// if we're in a button activated zone and can interact with it
			if (_characterButtonActivation != null)
			{
				if (_characterButtonActivation.AbilityAuthorized
				    && _characterButtonActivation.PreventJumpWhenInZone
				    && _characterButtonActivation.InButtonActivatedZone
				    && !_characterButtonActivation.InButtonAutoActivatedZone)
				{
					return false;
				}
				if (_characterButtonActivation.InJumpPreventingZone)
				{
					return false;
				}
			}

			// if we're crouching and don't have enough space to stand we do nothing and exit
			if ((_movement.CurrentState == CharacterStates.MovementStates.Crouching) || (_movement.CurrentState == CharacterStates.MovementStates.Crawling))
			{				
				if (_characterCrouch != null)
				{
					if (_characterCrouch.InATunnel && (_verticalInput >= -_inputManager.Threshold.y))
					{
						return false;
					}
				}
			}

			// if we're not grounded, not on a ladder, and don't have any jumps left, we do nothing and exit
			if ((!_controller.State.IsGrounded)
			    && !EvaluateJumpTimeWindow()
			    && (_movement.CurrentState != CharacterStates.MovementStates.LadderClimbing)
			    && (JumpRestrictions != JumpBehavior.CanJumpAnywhereAnyNumberOfTimes)
			    && (NumberOfJumpsLeft <= 0))			
			{
				return false;
			}

			if (_controller.State.IsGrounded 
			    && (NumberOfJumpsLeft <= 0))
			{
				return false;
			}           

			if (_inputManager != null)
			{
				if (_jumpingDownFromOneWayPlatform)
				{
					if ((_verticalInput > -_inputManager.Threshold.y) || (_jumpButtonReleased))
					{
						_jumpingDownFromOneWayPlatform = false;
					}
				}
				
				// if the character is standing on a one way platform and is also pressing the down button,
				if (_verticalInput < -_inputManager.Threshold.y && _controller.State.IsGrounded)
				{
					if (JumpDownFromOneWayPlatform())
					{
						return false;
					}
				}

				// if the character is standing on a moving platform and not pressing the down button,
				if (_controller.State.IsGrounded)
				{
					BumpFromMovingPlatform();
				}
			}	

			return true;
		}
        
        /// Evaluates the jump restrictions
        public virtual bool JumpAuthorized 
        { 
	        get 
	        { 
		        if (EvaluateJumpTimeWindow())
		        {
			        return true;
		        }

		        if (_movement.CurrentState == CharacterStates.MovementStates.SwimmingIdle) 
		        {
			        return false;
		        }

		        if ( (JumpRestrictions == JumpBehavior.CanJumpAnywhere) ||  (JumpRestrictions == JumpBehavior.CanJumpAnywhereAnyNumberOfTimes) )
		        {
			        return true;
		        }					

		        if (JumpRestrictions == JumpBehavior.CanJumpOnGround)
		        {
			        if (_controller.State.IsGrounded
			            || (_movement.CurrentState == CharacterStates.MovementStates.Gripping)
			            || (_movement.CurrentState == CharacterStates.MovementStates.LedgeHanging))
			        {
				        return true;
			        }
			        else
			        {
				        // if we've already made a jump and that's the reason we're in the air, then yes we can jump
				        if (NumberOfJumpsLeft < NumberOfJumps)
				        {
					        return true;
				        }
			        }
		        }				

		        if (JumpRestrictions == JumpBehavior.CanJumpOnGroundAndFromLadders)
		        {
			        if ((_controller.State.IsGrounded)
			            || (_movement.CurrentState == CharacterStates.MovementStates.Gripping)
			            || (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing)
			            || (_movement.CurrentState == CharacterStates.MovementStates.LedgeHanging))
			        {
				        return true;
			        }
			        else
			        {
				        // if we've already made a jump and that's the reason we're in the air, then yes we can jump
				        if (NumberOfJumpsLeft < NumberOfJumps)
				        {
					        return true;
				        }
			        }
		        }					
				
		        return false; 
	        }
        }
        
        /// <summary>
        /// Determines if whether or not a Character is still in its Jump Window (the delay during which, after falling off a cliff, a jump is still possible without requiring multiple jumps)
        /// </summary>
        /// <returns><c>true</c>, if jump time window was evaluated, <c>false</c> otherwise.</returns>
        protected virtual bool EvaluateJumpTimeWindow()
        {
	        _coyoteTime = false;

	        if (_movement.CurrentState == CharacterStates.MovementStates.Jumping 
	            || _movement.CurrentState == CharacterStates.MovementStates.DoubleJumping
	            || _movement.CurrentState == CharacterStates.MovementStates.WallJumping)
	        {
		        return false;
	        }

	        if (Time.time - _lastTimeGrounded <= CoyoteTime)
	        {
		        _coyoteTime = true;
		        return true;
	        }
	        else 
	        {
		        return false;
	        }
        }
        
        /// <summary>
        /// Handles jumping down from a one way platform.
        /// </summary>
        public virtual bool JumpDownFromOneWayPlatform()
        {
	        if (!CanJumpDownOneWayPlatforms || _jumpingDownFromOneWayPlatform)
	        {
		        return false;
	        }
        
	        // we go through all the colliders we're standing on, and if all of them are 1way, we're ok to jump down
	        bool canJumpDown = true;
	        foreach (GameObject obj in _controller.StandingOnArray)
	        {
		        if (obj == null)
		        {
			        continue;
		        }
		        if (!_controller.OneWayPlatformMask.MMContains(obj.layer) &&
		            !_controller.MovingOneWayPlatformMask.MMContains(obj.layer) &&
		            !_controller.StairsMask.MMContains(obj.layer))
		        {
			        canJumpDown = false;	
		        }
	        }
			     
	        if (canJumpDown)
	        {
		        _movement.ChangeState(CharacterStates.MovementStates.Jumping);
		        _characterHorizontalMovement.ResetHorizontalSpeed();
		        // we turn the boxcollider off for a few milliseconds, so the character doesn't get stuck mid platform
		        StartCoroutine(_controller.DisableCollisionsWithOneWayPlatforms(OneWayPlatformsBumpCollisionOffDuration));
		        _controller.DetachFromMovingPlatform();
		        _jumpingDownFromOneWayPlatform = true;
		        return true;
	        }
	        else
	        {
		        return false;
	        }
        }
        
        /// <summary>
        /// Handles bumping from a moving platform.
        /// </summary>
        protected virtual void BumpFromMovingPlatform()
        {
	        if (_controller.StandingOn != null)
	        {
		        if ( _controller.MovingPlatformMask.MMContains(_controller.StandingOn.layer)
		             || _controller.MovingOneWayPlatformMask.MMContains(_controller.StandingOn.layer) )
		        {
			        // we turn the boxcollider off for a few milliseconds, so the character doesn't get stuck mid air
			        StartCoroutine(_controller.DisableCollisionsWithMovingPlatforms(MovingPlatformsBumpCollisionOffDuration));
			        _controller.DetachFromMovingPlatform();
		        }	
	        }
        }
        
        /// Evaluates the bump restrictions
        public virtual bool BumpAuthorized
        { 
            get 
            {
                if (_movement.CurrentState == CharacterStates.MovementStates.SwimmingIdle) 
                {
                    return false;
                }
        
                if ( (BumpRestrictions == BumpBehavior.CanBumpAnywhere))
                {
                    return true;
                }					
        
                if (BumpRestrictions == BumpBehavior.CanBumpOnGround)
                {
                    if (_controller.State.IsGrounded
                        || (_movement.CurrentState == CharacterStates.MovementStates.Gripping)
                        || (_movement.CurrentState == CharacterStates.MovementStates.LedgeHanging))
                    {
                        return true;
                    }
                }				
        
                if (BumpRestrictions == BumpBehavior.CanBumpOnGroundAndFromLadders)
                {
                    if ((_controller.State.IsGrounded)
                        || (_movement.CurrentState == CharacterStates.MovementStates.Gripping)
                        || (_movement.CurrentState == CharacterStates.MovementStates.LadderClimbing)
                        || (_movement.CurrentState == CharacterStates.MovementStates.LedgeHanging))
                    {
                        return true;
                    }
                }

                return false; 
            }
        }
        
        
        /// <summary>
        /// Stores the current NumberOfBumps
        /// </summary>
        protected virtual void ResetInitialNumberOfJumps()
        {        
	        _initialNumberOfJumps = NumberOfJumps;
        }
        
        
        /// <summary>
        /// Resets the number of bumps.
        /// </summary>
        public virtual void ResetNumberOfJumps()
        {
	        NumberOfJumpsLeft = NumberOfJumps;
        }
        
        
        /// <summary>
        /// Updates the controller state based on our current bumping state
        /// </summary>
        protected virtual void UpdateController()
        {
	        _controller.State.IsShrinking = (_movement.CurrentState == CharacterStates.MovementStates.Shrinking);
        }
        
        /// <summary>
        /// Sets the number of bumps left.
        /// </summary>
        public virtual void SetNumberOfJumpsLeft(int newNumberOfJumps)
        {
	        NumberOfJumpsLeft = newNumberOfJumps;
        }
        
        /// <summary>
        /// Adds required animator parameters to the animator parameters list if they exist
        /// </summary>
        protected override void InitializeAnimatorParameters()
        {
	        RegisterAnimatorParameter(_shrinkingAnimationParameterName, AnimatorControllerParameterType.Bool, out _shrinkingAnimationParameter);
	        RegisterAnimatorParameter(_shrinkingTimeAnimationParameterName, AnimatorControllerParameterType.Float, out _shrinkingTimeAnimationParameter);
	        RegisterAnimatorParameter(_bumpingAnimationParameterName, AnimatorControllerParameterType.Bool, out _bumpingAnimationParameter);
	        RegisterAnimatorParameter(_hitTheGroundAnimationParameterName, AnimatorControllerParameterType.Bool, out _hitTheGroundAnimationParameter);
        }
        
        /// <summary>
        /// At the end of each cycle, sends Jumping states to the Character's animator
        /// </summary>
        public override void UpdateAnimator()
        {
	        MMAnimatorExtensions.UpdateAnimatorBool(_animator, _shrinkingAnimationParameter, (_movement.CurrentState == CharacterStates.MovementStates.Shrinking), _character._animatorParameters, _character.PerformAnimatorSanityChecks);
	        MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _shrinkingTimeAnimationParameter, _bumpPressDownTime, _character._animatorParameters, _character.PerformAnimatorSanityChecks);
	        MMAnimatorExtensions.UpdateAnimatorBool(_animator, _bumpingAnimationParameter, (_movement.CurrentState == CharacterStates.MovementStates.Bumping), _character._animatorParameters, _character.PerformAnimatorSanityChecks);
	        MMAnimatorExtensions.UpdateAnimatorBool(_animator, _hitTheGroundAnimationParameter, _controller.State.JustGotGrounded, _character._animatorParameters, _character.PerformAnimatorSanityChecks);
        }
        
        /// <summary>
        /// Resets parameters in anticipation for the Character's respawn.
        /// </summary>
        public override void ResetAbility()
        {
	        base.ResetAbility ();
	        NumberOfJumps = _initialNumberOfJumps;
	        NumberOfJumpsLeft = _initialNumberOfJumps;
        }
        

    }
}