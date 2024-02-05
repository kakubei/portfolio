using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StateMachine : Node {
    [Export] private State currentState;

    private Dictionary<string, State> states = new Dictionary<string, State>();
    
    public override void _Ready() {
        SetupStates();
    }
    
    public override void _PhysicsProcess(double delta) {
        currentState?.PhysicsUpdate(delta);
    }
    
    /// <summary>
    /// Iterates through all its children and sets them up as states in its dictionary
    /// </summary>
    private void SetupStates() {
        // Will only iterate through children that are actually State classes
        foreach (var child in GetChildren().OfType<State>()) {
            states.Add(child.Name, child);
            // Connect each child state's transition signal to `Transition` method here
            child.transition += Transition;
        }

        // Failsafe to add the first state as current if we haven't specified it in the editor
        if (currentState == null && states.Count > 0) {
            currentState = states.First().Value;
            GD.PushError("No default state set for StateMachine.");
        }

        currentState?.Enter();
    }

    public void Transition(string newStateName) {
        if (!states.ContainsKey(newStateName)) {
            GD.PushError($"No state found with key: {newStateName}");
            return;
        };

        var newState = states[newStateName];

        if (newState == currentState) return;
        
        currentState.Exit();
        newState.Enter();
        currentState = newState;
    }
}