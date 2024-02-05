using System;
using System.Linq;
using EnemyStateMachine;
using Godot;
using HexapusNew.helpers;
using PlayerStateMachine;

// NOTE: Player is part of the Player group because it's the easiest way to get it from the SceneTree
public partial class Player : CharacterBody2D {
    [ExportCategory("General")]
    [Export] private float speed = 400.0f;
    
    private int health = 5;
    
    [ExportCategory("Testing")]
    [Export] private bool canMoveUp = false;

    [Signal]
    public delegate void IsDeadEventHandler(); // Emitted by the Dead state, listened to by MainScreen.cs

    // Knockback variables
    private Vector2 pushPosition = Vector2.Zero;

    private bool canAttack = true;

    // Clinging variables
    private enum Surface {
        ground,
        right,
        left,
        ceiling,
        air
    }

    private Surface currentSurface = Surface.ground;
    private float rotationValue = 70;

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    private float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

    // Inventory
    private Inventory inventory;
    private Weapon visibleWeapon;

    // Nodes
    private Node2D visuals;
    private Sprite2D icon;
    private Sprite2D afro;
    private Hud hud;
    private RayCast2D raycastRight;
    private RayCast2D raycastLeft;
    private RayCast2D raycastDown;
    private AnimationPlayer animationPlayer;
    private Node2D weaponContainer;
    private StateMachine stateMachine;

    private void GetAllNodes() {
        visuals = GetNode<Node2D>("Visuals");
        icon = GetNode<Sprite2D>("Visuals/Icon");
        afro = GetNode<Sprite2D>("Visuals/Afro");
        hud = GetNode<Hud>("HUD");
        raycastRight = GetNode<RayCast2D>("Casters/RayCastRight");
        raycastLeft = GetNode<RayCast2D>("Casters/RayCastLeft");
        raycastDown = GetNode<RayCast2D>("Casters/RayCastDown");
        animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        weaponContainer = GetNode<Node2D>("Visuals/WeaponContainer");
        inventory = GetNode<Inventory>("/root/Inventory");
        stateMachine = GetNode<StateMachine>("PlayerStateMachine");
    }

    public override void _Ready() {
        GetAllNodes();
        SetupHealth();
        ConnectInventorySignals();
    }
    
    public override void _PhysicsProcess(double delta) {
        Attack();
        HolsterWeapon();
        DetectSurface();
        ApplyGravity(delta);
    }

    public void PlayAnimation(string animation) {
        animationPlayer.Play(animation);
    }

    public void StopAllAnimations() {
        animationPlayer.Stop();
    }
    
    public void Move(Vector2 direction, double delta) {
        Vector2 velocity = Velocity;

        if (direction != Vector2.Zero) {
            velocity.X = direction.X * speed;
        } else {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, speed);
        }

        if (canMoveUp) {
            if (direction.Y != 0) {
                velocity.Y = direction.Y * speed;
            }
            else {
                velocity.Y = Mathf.MoveToward(Velocity.Y, 0, speed);
            }
        }

        Velocity = velocity;
        Flip();
    }
    
    public void ApplyGravity(double delta) {
        Vector2 gravDir = ChangeGravity();
        Velocity += gravity * (float)delta * gravDir;
    }

    public void ApplyVelocity(double delta) {
        MoveAndSlide();
    }

    public void CanAttack(bool state) {
        canAttack = state;
    }

    public float GetKnockbackDirectionX() {
        return pushPosition.X;
    }
    
    // TODO: We need a single method that calls whatever current power we have and the power takes care of animation, etc
    // ... will create a ticket for this, right now it's only a test
    public void StartDiscoPower() {
        afro.Show();
        animationPlayer.Play(AnimationName.dance);
    }

    public void StopDiscoPower() {
        afro.Hide();
        animationPlayer.Stop();
    }
    
    /// <summary>
    /// Simply connects the necessary signals from Inventory to local methods
    /// </summary>
    private void ConnectInventorySignals() {
        inventory.equipWeapon += EquipWeapon;
        inventory.updateWeapon += () => { UpdateWeapon(); };
    }

    private void EquipWeapon(Weapon weapon) {
        // FindChild(name) or FindChildren(name) don't work in 4.2 (https://github.com/godotengine/godot/issues/85690)
        // ...so we have to do it the shitty way:

        var weaponAlreadyEquipped = false;
        foreach (var child in weaponContainer.GetChildren()) {
            weaponAlreadyEquipped = child == weapon;
        }

        if (!weaponAlreadyEquipped) {
            // Needs to be deferred because not all cleanup has finished
            weaponContainer.CallDeferred(Node.MethodName.AddChild, weapon);

            // Hide it if it's not the current weapon
            weapon.Visible = weapon == inventory.currentWeapon;
            visibleWeapon = inventory.currentWeapon;

            // NOTE: Use the weapon's Sprite2D Offset values to position each weapon properly on player
            weapon.Transform = weaponContainer.Transform;
        }
    }

    /// <summary>
    /// Shows new current weapon
    /// hides previous one
    /// </summary>
    private void UpdateWeapon() {
        // Note: existingWeapons is different than inventory.equippedWeapons...
        //... existing are the ones that have actually been created as children of the weaponContainer
        var existingWeapons = weaponContainer.GetChildren();
        if (!IsInstanceValid(inventory.currentWeapon) || existingWeapons.Count == 0) return;

        if (visibleWeapon != inventory.currentWeapon) {
            visibleWeapon.Holster();
            visibleWeapon = inventory.currentWeapon;
            visibleWeapon.Unholster();   
        }
    }

    private void SetupHealth() {
        hud.SetupHealth(health);
    }

    private void Flip() {
        var flipped = new Vector2(-1, 1);
        var normal = new Vector2(1, 1);
        if (Velocity.X < 0) {
            visuals.Scale = flipped;
        }
        else if (Velocity.X > 0) {
            visuals.Scale = normal;
        }
    }
    
    private void OnEnemyCollision(Node2D body) {
        if (body is not Enemy enemy) return;
        if (!enemy.alive) return;

        pushPosition = GlobalPosition.DirectionTo(enemy.Position);
 
        stateMachine.Transition(PlayerState.StateName.knockback);
        TakeDamage(enemy.myDamage);
    }

    private void TakeDamage(int damage) {
        health -= damage;
        hud.CheckHealth(damage);
        if (health <= 0) stateMachine.Transition(PlayerState.StateName.dead);
    }

    private void Attack() {
        if (inventory?.currentWeapon == null || !canAttack) return;

        if (Input.IsActionJustPressed(KeyInput.attack) && inventory.currentWeapon.Visible) {
            inventory.currentWeapon?.Attack();
        }
    }

    private void HolsterWeapon() {
        if (!Input.IsActionJustPressed(KeyInput.holsterWeapon) || inventory.currentWeapon == null) return;

        if (inventory.currentWeapon.Visible) {
            inventory.currentWeapon.Holster();
        }
        else {
            inventory.currentWeapon.Unholster();
        }
    }

    // Still experimental
    private void ChangeSurface(Surface surface) {
        if (currentSurface == surface) return;

        currentSurface = surface;

        float rotation = 0;

        switch (surface) {
            case Surface.right:
                GD.Print("On right wall");
                rotation = -rotationValue;
                canMoveUp = true;
                break;
            case Surface.left:
                GD.Print("On left wall");
                rotation = rotationValue;
                canMoveUp = true;
                break;
            default:
                GD.Print("On ground");
                rotation = 0;
                canMoveUp = false;
                break;
        }

        icon.Rotation = rotation;
        visuals.Rotation = rotation;
    }

    private Vector2 ChangeGravity() {
        var gravDir = currentSurface switch {
            Surface.right => Vector2.Right,
            Surface.left => Vector2.Left,
            Surface.air => Vector2.Down,
            _ => Vector2.Down
        };
        return gravDir;
    }

    private void DetectSurface() {
        // We need to check that collider is not null because it could have been a deleted enemy that triggered the IsColliding() call
        if (raycastRight.IsColliding() && raycastRight.GetCollider() != null) {
            if (((Node2D)raycastRight.GetCollider()).IsInGroup(Group.Walls)) {
                ChangeSurface(Surface.right);
            }
        }
        else if (raycastLeft.IsColliding()) {
            if (((Node2D)raycastLeft.GetCollider()).IsInGroup(Group.Walls)) {
                ChangeSurface(Surface.left);
            }
        }
        else if (raycastDown.IsColliding()) {
            if (((Node2D)raycastDown.GetCollider()).IsInGroup(Group.Ground)) {
                ChangeSurface(Surface.ground);
            }
        }
        else {
            ChangeSurface(Surface.air);
        }
    }
}