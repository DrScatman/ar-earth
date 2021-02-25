using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CustomInputField : InputField
{
    public bool canDeactivate = true;
    public new bool Focused = false;
    public new bool Deactivated = false;


    new public void ActivateInputField()
    {
        base.ActivateInputField();
        Focused = true;
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        if (canDeactivate)
        {
            Deactivated = true;
            DeactivateInputField();
            base.OnDeselect(eventData);
        }
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        ActivateInputField();
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (Deactivated)
        {
            MoveTextEnd(true);
            Deactivated = false;
        }
        base.OnPointerClick(eventData);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (Focused)
        {
            MoveTextEnd(true);
            Focused = false;
        }
    }
}
