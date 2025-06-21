using UnityEngine;

/// <summary>
/// Enum for different cooking states - simplified version
/// </summary>
public enum CookingState
{
    Raw,        // Initial state - ready to cook
    Cooking,    // Currently being cooked
    Cooked,     // Finished cooking - ready to eat or can burn
    Burnt       // Overcooked - usually bad result
}