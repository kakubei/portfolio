namespace PlayerStateMachine;

public abstract partial class PlayerState : State {
    protected internal Player player;

    public struct StateName {
        public static string idle = "Idle";
        public static string walk = "Walk";
        public static string dead = "Dead";
        public static string knockback = "Knockback";
    }
    
    public override void _Ready() {
        // Player states will always be children of the Player
        player = GetNode<Player>($"{Owner.GetPath()}");
    }
}