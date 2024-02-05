using Godot;
using HexapusNew.helpers;

namespace PlayerStateMachine;

public partial class Walk: PlayerState {
    public override void Enter() {
        player.PlayAnimation(AnimationName.walk);
    }
    
    public override void PhysicsUpdate(double delta) {
        var inputVector = GameManager.GetInputVector();
        if (inputVector != Vector2.Zero) {
            player.Move(inputVector, delta);
            player.ApplyVelocity(delta);
        }

        // Not too happy with this state knowing which next state to transition to
        // TODO: Maybe have an intermediary that knows which states come next?
        if (inputVector == Vector2.Zero) {
            EmitSignal(PlayerState.SignalName.transition, StateName.idle);
        }
    }
    
    public override void Exit() {
        player.StopAllAnimations();
    }
}