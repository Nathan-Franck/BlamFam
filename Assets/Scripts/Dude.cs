using ToonBoom.TBGRenderer;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput), typeof(TBGRenderer), typeof(Animator))]
public class Dude : MonoBehaviour
{
    public Transform PeletteSource;
    public Transform FloorPoint;

    public PlayerInput PlayerInput;
    public TBGRenderer TBGRenderer;
    public Animator Animator;

    public Vector2 PeletteSourceOffset;

    public void Awake()
    {
        PlayerInput = GetComponent<PlayerInput>();
        TBGRenderer = GetComponent<TBGRenderer>();
        Animator = GetComponent<Animator>();

        TBGRenderer.PaletteID = (ushort)PlayerInput.playerIndex;

        PeletteSourceOffset = PeletteSource.position - transform.position;
    }
}
