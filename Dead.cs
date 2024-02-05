using System;
using Godot;
using HexapusNew.helpers;

namespace PlayerStateMachine;

public partial class Dead : PlayerState {
    public override async void Enter() {
        player.CanAttack(false);
        player.PlayAnimation(AnimationName.death);
        await ToSignal(GetTree().CreateTimer(1.5), Timer.SignalName.Timeout);
        player.EmitSignal(Player.SignalName.IsDead);
        player.QueueFree();
    }
}