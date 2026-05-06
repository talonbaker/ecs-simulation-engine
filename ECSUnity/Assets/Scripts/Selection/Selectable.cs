using System;
using UnityEngine;

/// <summary>
/// Marks a GameObject as click-selectable.
/// Attach alongside <see cref="OutlineRenderer"/> on any object that can be
/// selected by the player.  <see cref="SelectionManager"/> calls
/// <see cref="Select"/> and <see cref="Deselect"/>; everything else subscribes
/// to the two events.
/// </summary>
public sealed class Selectable : MonoBehaviour
{
    /// <summary>Fires when this object becomes the active selection.</summary>
    public event Action<Selectable> OnSelected;

    /// <summary>Fires when this object is deselected (either explicitly or by another selection).</summary>
    public event Action<Selectable> OnDeselected;

    /// <summary>True while this object is the active selection.</summary>
    public bool IsSelected { get; private set; }

    public void Select()
    {
        if (IsSelected) return;
        IsSelected = true;
        OnSelected?.Invoke(this);
    }

    public void Deselect()
    {
        if (!IsSelected) return;
        IsSelected = false;
        OnDeselected?.Invoke(this);
    }
}
